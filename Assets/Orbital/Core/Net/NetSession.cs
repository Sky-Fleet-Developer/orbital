using System;
using System.Collections.Generic;

namespace Orbital.Core.Net
{
    public enum SessionState
    {
        Disconnected,
        Connecting,
        /// <summary>Socket open, first ping for the clock (Syncing).</summary>
        Syncing,
        LoggingIn,
        /// <summary>Logged in, the account has no craft: spawn needed.</summary>
        NoCraft,
        Ready,
        /// <summary>The server refused hello (e.g. a taken nickname with a wrong token): no automatic retries.</summary>
        Refused,
    }

    public struct SessionConfig
    {
        public double PingInterval;      // 10 s
        public double GetStateInterval;  // 15 s
        public double RequestTimeout;    // 10 s
        public double ReconnectMin;      // 1 s
        public double ReconnectMax;      // 30 s

        public static SessionConfig Default() => new SessionConfig
        {
            PingInterval = 10, GetStateInterval = 15, RequestTimeout = 10, ReconnectMin = 1, ReconnectMax = 30,
        };
    }

    /// <summary>
    /// The client's connection to the server (stage 2c.5): connect, clock sync, hello (token saved per url and callsign), admin_auth when a
    /// secret is given, ping every 10 s (clock samples), get_state of the own craft every 15 s, reconnect after 1..30 s, request timeouts,
    /// and the send queue of the own craft (<see cref="Uplink"/>). Single-threaded: the game loop calls <see cref="Update"/> with the real
    /// time in seconds; everything it reports comes back through the events inside that call. The physics of the own craft (history,
    /// reconciliation) is the caller's: the session hands it the server states.
    /// </summary>
    public sealed class NetSession : IDisposable
    {
        private sealed class Pending
        {
            public long Id;
            public string Type;
            public double SentAt;
            public object Tag;
        }

        private readonly Func<ITransport> _factory;
        private readonly SessionConfig _cfg;
        private readonly Dictionary<long, Pending> _pending = new Dictionary<long, Pending>();
        private ITransport _t;
        private long _nextId;
        private double _now;
        private double _reconnectAt = double.PositiveInfinity;
        private double _reconnectDelay;
        private double _lastPing = double.NegativeInfinity, _lastGetState = double.NegativeInfinity;
        private bool _pingInFlight;
        private bool _resyncRequested;

        public NetSession(string url, string callsign, Func<ITransport> factory = null, SessionConfig? cfg = null)
        {
            Url = url;
            Callsign = callsign;
            _factory = factory ?? (() => new WsTransport());
            _cfg = cfg ?? SessionConfig.Default();
            _reconnectDelay = _cfg.ReconnectMin;
        }

        public string Url { get; }
        public string Callsign { get; }
        /// <summary>The account token: set before connecting to log in, updated from hello_ok (save it then: <see cref="TokenChanged"/>).</summary>
        public string Token { get; set; }
        /// <summary>Admin secret or null. Never logged.</summary>
        public string AdminSecret { get; set; }

        public SessionState State { get; private set; } = SessionState.Disconnected;
        public string LastError { get; private set; }
        public string CraftId { get; private set; }
        public bool IsAdmin { get; private set; }
        public bool HasControl { get; private set; }
        public double TimeScale { get; private set; } = 1.0;
        public string BootId { get; private set; }
        public string WorldHash { get; private set; }
        public double LastRtt { get; private set; } = double.NaN;
        public int Reconnects { get; private set; }
        public HelloOkMessage LastHello { get; private set; }
        public readonly ClockSync Clock = new ClockSync();
        public readonly Uplink Uplink = new Uplink(UplinkConfig.Default());

        public event Action<string> TokenChanged;
        /// <summary>hello_ok: the session is ready or needs a spawn. The caller takes the state (hard), resets the uplink is done here.</summary>
        public event Action<HelloOkMessage> LoggedIn;
        public event Action<SpawnResultDto> SpawnResult;
        /// <summary>A server state of the own craft (get_state, craft_update, accepted maneuver/attitude replies carry no state).</summary>
        public event Action<CraftStateDto> OwnState;
        /// <summary>States of other crafts (get_state of all, proximity_update).</summary>
        public event Action<CraftStateDto> OtherState;
        public event Action<TimeScaleMessage> TimeScaleChanged;
        public event Action<CraftDestroyedMessage> CraftDestroyed;
        public event Action<ControlTakenMessage> ControlTaken;
        public event Action<AdminResultMessage> AdminResult;
        public event Action<AdminAuthResultMessage> AdminAuthResult;
        /// <summary>A maneuver or attitude reply after the uplink took it.</summary>
        public event Action<UplinkOutcome, string> UplinkResult;
        /// <summary>The uplink needs a resynchronisation: the session fetched the state; the caller decides (hard correction).</summary>
        public event Action<string> Log;

        /// <summary>Start (or restart now) the connection.</summary>
        public void Connect()
        {
            _reconnectAt = _now;
            if (State == SessionState.Refused) State = SessionState.Disconnected;
            if (_t == null) Open();
        }

        /// <summary>Drop the connection; it comes back after the reconnect delay. For tests: a server restart looks like this.</summary>
        public void Drop(string why)
        {
            Close(why);
        }

        public void Update(double realNow)
        {
            _now = realNow;
            if (_t == null)
            {
                if (State == SessionState.Disconnected && realNow >= _reconnectAt) Open();
                return;
            }
            if (_t.State == TransportState.Closed)
            {
                Close(_t.Error ?? "closed");
                return;
            }
            if (_t.State == TransportState.Open && State == SessionState.Connecting)
            {
                State = SessionState.Syncing;
                SendPing();
            }
            int guard = 0;
            while (guard++ < 1000 && _t != null && _t.TryReceive(out string text)) Handle(text);
            if (_t == null) return;
            Timeouts();
            if (State == SessionState.NoCraft || State == SessionState.Ready)
            {
                if (!_pingInFlight && realNow - _lastPing >= _cfg.PingInterval) SendPing();
                if (State == SessionState.Ready && realNow - _lastGetState >= _cfg.GetStateInterval) RequestOwnState();
            }
            if (State == SessionState.Ready && HasControl)
            {
                Uplink.Tick(realNow);
                if (Uplink.NeedsResync && !_resyncRequested)
                {
                    Emit("uplink: resync after " + Uplink.LastRejection);
                    if (Uplink.LastRejection == "timeout")
                    {
                        // the ledger is unknown: a new hello brings lastSeq and the state
                        Close("uplink timeout");
                        return;
                    }
                    _resyncRequested = true; // a rejection carries lastSeq: the next own state resets the uplink
                    RequestOwnState();
                }
                if (Uplink.TryTakeToSend(realNow, out OutgoingMessage m)) Request(m.Type, m.Body, m.Seq);
            }
        }

        public long Spawn(string startPointId = null) =>
            State == SessionState.NoCraft || State == SessionState.Ready
                ? Request(MessageTypes.Spawn, new SpawnRequestDto { StartPointId = startPointId })
                : -1;

        public long RequestOwnState()
        {
            _lastGetState = _now;
            if (CraftId == null) return -1;
            return Request(MessageTypes.GetState, new GetStateRequest { CraftIds = new List<string> { CraftId } });
        }

        /// <summary>After a hard correction: the client's cumulative RCS counter now is what the server's state already includes.</summary>
        public void ResetRcsBaseline(double rcsUsedTotal)
        {
            if (CraftId != null) Uplink.Reset(CraftId, Uplink.LastAcceptedSeq, Uplink.CommittedUntil, rcsUsedTotal);
        }

        public long RequestWrecks() => Request(MessageTypes.GetWrecks, null);

        /// <summary>A wreck the server told about (get_wrecks, wreck_created, the own craft_destroyed).</summary>
        public event Action<WreckDto> WreckSeen;

        public long RequestAllStates() => Request(MessageTypes.GetState, new GetStateRequest());

        public long SetTimeScale(double scale) => IsAdmin ? Request(MessageTypes.AdminSetTimeScale, new AdminSetTimeScaleRequest { Scale = scale }) : -1;

        /// <summary>Server time now by the clock samples (NaN before the first pong).</summary>
        public double ServerTime(double realNow) => Clock.ServerTime(realNow);

        // ------------------------------------------------------------------ internals

        private void Open()
        {
            State = SessionState.Connecting;
            _pending.Clear();
            _pingInFlight = false;
            _t = _factory();
            try { _t.Connect(Url); }
            catch (Exception e) { Close(e.Message); }
        }

        private void Close(string why)
        {
            LastError = why;
            if (_t != null)
            {
                Emit("disconnected: " + why);
                try { _t.Dispose(); } catch (Exception) { }
                _t = null;
                Reconnects++;
            }
            _pending.Clear();
            _pingInFlight = false;
            _resyncRequested = false;
            IsAdmin = false;
            HasControl = false;
            if (State != SessionState.Refused) State = SessionState.Disconnected;
            _reconnectAt = _now + _reconnectDelay;
            _reconnectDelay = Math.Min(_cfg.ReconnectMax, _reconnectDelay * 2);
        }

        private bool HasPending(string type)
        {
            foreach (Pending p in _pending.Values) if (p.Type == type) return true;
            return false;
        }

        private long Request(string type, object body, object tag = null)
        {
            if (_t == null || _t.State != TransportState.Open) return -1;
            long id = ++_nextId;
            _pending[id] = new Pending { Id = id, Type = type, SentAt = _now, Tag = tag };
            _t.Send(ProtocolCodec.Encode(type, id, body));
            return id;
        }

        private void SendPing()
        {
            _lastPing = _now;
            _pingInFlight = Request(MessageTypes.Ping, null) > 0;
        }

        private void Timeouts()
        {
            List<long> late = null;
            foreach (Pending p in _pending.Values)
                if (_now - p.SentAt > _cfg.RequestTimeout) (late = late ?? new List<long>()).Add(p.Id);
            if (late == null) return;
            foreach (long id in late)
            {
                string type = _pending[id].Type;
                _pending.Remove(id);
                if (type == MessageTypes.Ping) _pingInFlight = false;
                Emit("timeout: " + type);
                if (type == MessageTypes.Hello || type == MessageTypes.Ping && State == SessionState.Syncing)
                {
                    Close("timeout " + type);
                    return;
                }
            }
        }

        private void Handle(string text)
        {
            Envelope e;
            try { e = ProtocolCodec.Decode(text); }
            catch (Exception ex)
            {
                Emit("bad message: " + ex.Message);
                return;
            }
            Pending req = null;
            if (e.Id.HasValue && _pending.TryGetValue(e.Id.Value, out req)) _pending.Remove(e.Id.Value);
            try
            {
                Dispatch(e, req);
            }
            catch (Exception ex)
            {
                Emit("cannot handle " + e.Type + ": " + ex.Message + " | " + ProtocolCodec.ForLog(text));
            }
        }

        private void Dispatch(Envelope e, Pending req)
        {
            switch (e.Type)
            {
                case MessageTypes.Pong:
                    OnPong(e.As<PongMessage>(), req);
                    break;
                case MessageTypes.HelloOk:
                    OnHelloOk(e.As<HelloOkMessage>());
                    break;
                case MessageTypes.HelloError:
                    LastError = "hello_error: " + e.As<HelloErrorMessage>().Reason;
                    Emit(LastError);
                    State = SessionState.Refused;
                    try { _t.Dispose(); } catch (Exception) { }
                    _t = null;
                    break;
                case MessageTypes.AdminAuthResult:
                {
                    AdminAuthResultMessage r = e.As<AdminAuthResultMessage>();
                    IsAdmin = r.Ok;
                    Emit("admin_auth: " + (r.Ok ? "ok" : r.Reason));
                    AdminAuthResult?.Invoke(r);
                    break;
                }
                case MessageTypes.AdminResult:
                    AdminResult?.Invoke(e.As<AdminResultMessage>());
                    break;
                case MessageTypes.SpawnResult:
                    OnSpawn(e.As<ResultMessage<SpawnResultDto>>().Result);
                    break;
                case MessageTypes.States:
                    OnStates(e.As<StatesMessage>(), req);
                    break;
                case MessageTypes.CraftUpdate:
                {
                    CraftUpdateMessage m = e.As<CraftUpdateMessage>();
                    if (m.State != null) Deliver(m.State);
                    break;
                }
                case "proximity_update":
                {
                    StatesMessage m = e.As<StatesMessage>();
                    if (m.States != null) foreach (CraftStateDto s in m.States) Deliver(s);
                    break;
                }
                case MessageTypes.ManeuverResult:
                {
                    UplinkOutcome o = Uplink.OnManeuverResult(e.As<ResultMessage<ManeuverResultDto>>().Result);
                    AfterUplink(o);
                    break;
                }
                case MessageTypes.AttitudeResult:
                {
                    UplinkOutcome o = Uplink.OnAttitudeResult(e.As<ResultMessage<AttitudeResultDto>>().Result);
                    AfterUplink(o);
                    break;
                }
                case MessageTypes.TimeScale:
                {
                    TimeScaleMessage m = e.As<TimeScaleMessage>();
                    TimeScale = m.Scale;
                    Clock.OnScaleChange(m.Time, m.Scale);
                    TimeScaleChanged?.Invoke(m);
                    break;
                }
                case MessageTypes.Wrecks:
                {
                    WrecksMessage m = e.As<WrecksMessage>();
                    if (m.Wrecks != null) foreach (WreckDto w in m.Wrecks) WreckSeen?.Invoke(w);
                    break;
                }
                case MessageTypes.WreckCreated:
                {
                    WreckDto w = e.As<WreckCreatedMessage>().Wreck;
                    if (w != null) WreckSeen?.Invoke(w);
                    break;
                }
                case MessageTypes.ControlTaken:
                    HasControl = false;
                    ControlTaken?.Invoke(e.As<ControlTakenMessage>());
                    break;
                case MessageTypes.CraftDestroyed:
                {
                    CraftDestroyedMessage m = e.As<CraftDestroyedMessage>();
                    if (m.CraftId == CraftId)
                    {
                        CraftId = null;
                        Uplink.Reset(null, 0, null, 0);
                        State = SessionState.NoCraft;
                    }
                    CraftDestroyed?.Invoke(m);
                    if (m.Wreck != null) WreckSeen?.Invoke(m.Wreck);
                    break;
                }
                case MessageTypes.Error:
                {
                    ErrorMessage m = e.As<ErrorMessage>();
                    Emit("error " + (req?.Type ?? "") + ": " + m.Reason + " " + m.Message);
                    if (m.Reason == Reasons.NoCraft)
                    {
                        CraftId = null;
                        State = SessionState.NoCraft;
                    }
                    break;
                }
            }
        }

        private void OnPong(PongMessage p, Pending req)
        {
            _pingInFlight = false;
            if (req == null) return;
            if (BootId != null && p.BootId != null && p.BootId != BootId) Clock.Reset(); // the server restarted: old samples are void
            BootId = p.BootId;
            LastRtt = _now - req.SentAt;
            Clock.AddSample(req.SentAt, _now, p.Time, p.Scale);
            TimeScale = p.Scale;
            if (State == SessionState.Syncing)
            {
                State = SessionState.LoggingIn;
                Request(MessageTypes.Hello, new HelloRequest { Nickname = Callsign, Token = Token });
            }
        }

        private void OnHelloOk(HelloOkMessage h)
        {
            LastHello = h;
            _reconnectDelay = _cfg.ReconnectMin;
            if (!string.IsNullOrEmpty(h.Token) && h.Token != Token)
            {
                Token = h.Token;
                TokenChanged?.Invoke(Token);
            }
            WorldHash = h.WorldHash;
            HasControl = h.Control;
            if (h.TimeScale != null) TimeScale = h.TimeScale.Scale;
            CraftId = h.CraftId;
            if (CraftId != null)
            {
                // the caller takes the server state (hard): the queue keeps only what can still be resent
                Uplink.Reset(CraftId, h.LastSeq ?? 0, h.CommittedUntil, null);
                State = SessionState.Ready;
                _lastGetState = _now;
            }
            else State = SessionState.NoCraft;
            if (!string.IsNullOrEmpty(AdminSecret)) Request(MessageTypes.AdminAuth, new AdminAuthRequest { Secret = AdminSecret });
            Emit("hello_ok " + h.Nickname + (CraftId != null ? " craft " + CraftId : " (no craft)"));
            LoggedIn?.Invoke(h);
        }

        private void OnSpawn(SpawnResultDto r)
        {
            if (r != null && r.Ok)
            {
                CraftId = r.CraftId;
                HasControl = true;
                Uplink.Reset(CraftId, 0, null, 0);
                State = SessionState.Ready;
                _lastGetState = _now;
            }
            Emit("spawn: " + (r == null ? "?" : r.Ok ? "ok " + r.CraftId : r.Reason));
            SpawnResult?.Invoke(r);
        }

        private void OnStates(StatesMessage m, Pending req)
        {
            if (m.States == null) return;
            foreach (CraftStateDto s in m.States) Deliver(s);
        }

        private void Deliver(CraftStateDto s)
        {
            if (s == null) return;
            if (s.CraftId != null && s.CraftId == CraftId)
            {
                if (_resyncRequested)
                {
                    // the caller corrects hard on this state and sets the RCS baseline (ResetRcsBaseline); the queue starts from the ledger
                    _resyncRequested = false;
                    Uplink.Clear();
                    Uplink.Reset(CraftId, Uplink.LastAcceptedSeq, Uplink.CommittedUntil, null);
                    Resynced?.Invoke(s);
                }
                OwnState?.Invoke(s);
            }
            else OtherState?.Invoke(s);
        }

        /// <summary>The uplink was reset to the server ledger after a rejection or timeout; the caller corrects hard on this state and resets the RCS baseline.</summary>
        public event Action<CraftStateDto> Resynced;

        private void AfterUplink(UplinkOutcome o)
        {
            if (o == UplinkOutcome.ControlLost) HasControl = false;
            if (o == UplinkOutcome.Rejected || o == UplinkOutcome.ControlLost)
                Emit("uplink " + o + ": " + Uplink.LastRejection + " " + Uplink.LastRejectionMessage);
            UplinkResult?.Invoke(o, Uplink.LastRejection);
        }

        private void Emit(string line) => Log?.Invoke(line);

        public void Dispose()
        {
            _reconnectAt = double.PositiveInfinity;
            if (_t != null)
            {
                try { _t.Dispose(); } catch (Exception) { }
                _t = null;
            }
            State = SessionState.Disconnected;
        }
    }
}
