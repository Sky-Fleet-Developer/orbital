using System;
using System.Collections.Generic;
using Orbital.Core.Flight;
using Orbital.Core.Net;
using UnityEngine;

namespace Orbital.Craft
{
    /// <summary>
    /// The Unity side of the online client (stage 2c.6, plain class): settings, <see cref="NetSession"/> and <see cref="OwnCraftLink"/>, wired
    /// the way the e2e tests wire them (<c>OrbitServer.Tests/Stage2cNetTests.Pilot</c>). The spawner calls <see cref="Update"/> every frame
    /// with the unscaled real time and steps the craft through <see cref="Link"/>; the flight parameters of a server snapshot come from
    /// the spawner (body surface and gravity of the world). Logs go through <see cref="ProtocolCodec.ForLog"/>-safe text only.
    /// </summary>
    public sealed class CraftNetLink : IDisposable
    {
        private readonly ClientSettings _settings;
        private readonly Func<CraftSnapshotDto, FlightParams> _paramsOf;

        public readonly NetSession Session;
        public readonly OwnCraftLink Link;
        public string LastCorrection { get; private set; } = "-";
        public int Rejections { get; private set; }
        public string LastRejection { get; private set; }
        public CraftDestroyedMessage Destroyed { get; private set; }

        /// <summary>Another player's craft (stage 2c.7): the extrapolated ghost, its body and when the server last spoke of it.</summary>
        public sealed class Other
        {
            public Ghost Ghost;
            public string ParentId;
            public double LastHeardReal;
        }

        private readonly Dictionary<string, Other> _others = new Dictionary<string, Other>();
        public IReadOnlyDictionary<string, Other> Others => _others;
        private readonly Dictionary<string, WreckDto> _wrecks = new Dictionary<string, WreckDto>();
        public IReadOnlyDictionary<string, WreckDto> Wrecks => _wrecks;
        public GhostConfig GhostCfg = GhostConfig.Default();
        /// <summary>Real seconds between get_state of all crafts (15) and after which a silent craft is forgotten (45).</summary>
        public double OthersPoll = 15, OthersForget = 45;
        private double _lastAllStates = double.NegativeInfinity;
        private double _real;
        /// <summary>Body of the own craft by the last server word (spawn, hello, state).</summary>
        public string OwnParent { get; private set; }

        public CraftNetLink(ClientSettings settings, string callsignFallback, Func<CraftSnapshotDto, FlightParams> paramsOf)
        {
            _settings = settings;
            _paramsOf = paramsOf;
            string callsign = string.IsNullOrWhiteSpace(settings.Callsign) ? callsignFallback : settings.Callsign;
            Session = new NetSession(settings.ServerUrl, callsign)
            {
                Token = settings.TokenFor(settings.ServerUrl, callsign),
                AdminSecret = settings.ReadAdminSecret(),
            };
            Link = new OwnCraftLink(Session);
            Session.Log += line => Debug.Log("[net] " + line);
            Session.TokenChanged += token =>
            {
                _settings.SetToken(Session.Url, Session.Callsign, token);
                try { _settings.Save(); }
                catch (Exception e) { Debug.LogWarning("[net] settings not saved: " + e.Message); }
            };
            Session.SpawnResult += r =>
            {
                if (r == null || !r.Ok || r.State == null) return;
                Destroyed = null;
                OwnParent = r.State.ParentId;
                Link.Params = _paramsOf(r.Snapshot);
                Link.TakeServerState(r.State, r.State.Time, "spawn");
            };
            Session.LoggedIn += h =>
            {
                if (h.State == null) return;
                OwnParent = h.State.ParentId;
                Link.Params = _paramsOf(h.Snapshot);
                // after a reconnect the past is the server's: hard correction to its state carried to the client time
                Link.TakeServerState(h.State, Link.HasCraft ? Math.Max(Link.Time, h.State.Time) : h.State.Time, "hello");
                LastCorrection = "hard (hello)";
            };
            Session.Resynced += s =>
            {
                Link.TakeServerState(s, Link.Time, "resync");
                LastCorrection = "hard (resync)";
            };
            Session.OwnState += s =>
            {
                if (s.ParentId != null) OwnParent = s.ParentId;
                if (_hardNext)
                {
                    _hardNext = false;
                    Link.TakeServerState(s, Link.HasCraft ? Math.Max(Link.Time, s.Time) : s.Time, "catch-up");
                    LastCorrection = "hard (catch-up)";
                    return;
                }
                ReconcileAction a = Link.OnServerState(s, s.ParentId);
                if (a == ReconcileAction.Soft || a == ReconcileAction.Hard)
                {
                    ReconcileResult r = Link.LastReconcile;
                    LastCorrection = a + (r.Reason != null ? " " + r.Reason : "") + " " + Vec3.Length(r.Dp).ToString("F1") + " m";
                    Debug.Log($"[net] correction {a} {r.Reason}: dp {Vec3.Length(r.Dp):F2} m, dv {Vec3.Length(r.Dv):F3} m/s, state t {s.Time:F2} ({s.Mode}), " +
                              $"client t {Link.Time:F2}, history {Link.History.OldestTime:F2}..{Link.History.NewestTime:F2}");
                }
            };
            Session.UplinkResult += (o, reason) =>
            {
                if (o != UplinkOutcome.Rejected && o != UplinkOutcome.ControlLost) return;
                Rejections++;
                LastRejection = reason;
            };
            Session.CraftDestroyed += m => Destroyed = m;
            Session.OtherState += OnOther;
            Session.WreckSeen += w =>
            {
                if (w?.Id == null) return;
                _wrecks[w.Id] = w;
                if (w.CraftId != null) _others.Remove(w.CraftId); // the craft became this wreck: its ghost goes at once
            };
            Session.LoggedIn += _ => _lastAllStates = double.NegativeInfinity; // the others at once after (re)login
        }

        private bool _hardNext;

        /// <summary>The craft fell too far behind the world time (a clock jump, a long frame): take the next server state as is.</summary>
        public void RequestHardSync()
        {
            if (_hardNext) return;
            _hardNext = Session.RequestOwnState() > 0;
        }

        public void Connect() => Session.Connect();

        public void Update(double realNow)
        {
            _real = realNow;
            Session.Update(realNow);
            if ((Session.State == SessionState.Ready || Session.State == SessionState.NoCraft) && realNow - _lastAllStates >= OthersPoll)
            {
                _lastAllStates = realNow;
                Session.RequestAllStates();
                if (_wrecks.Count == 0) Session.RequestWrecks(); // then wreck_created keeps the list current
            }
        }

        private void OnOther(CraftStateDto s)
        {
            if (s?.CraftId == null) return;
            if (!_others.TryGetValue(s.CraftId, out Other o))
            {
                o = new Other { Ghost = new Ghost(s.CraftId) };
                _others[s.CraftId] = o;
            }
            o.ParentId = s.ParentId;
            o.LastHeardReal = _real;
            double now = Link.HasCraft ? Link.Time : s.Time;
            o.Ghost.OnServerState(s, Link.Params, now, GhostCfg);
        }

        /// <summary>Carries every ghost to the client time and forgets those the server has not mentioned for a while (destroyed, gone).</summary>
        public void AdvanceOthers(double now)
        {
            List<string> gone = null;
            foreach (KeyValuePair<string, Other> kv in _others)
            {
                if (_real - kv.Value.LastHeardReal > OthersForget) (gone = gone ?? new List<string>()).Add(kv.Key);
                else kv.Value.Ghost.AdvanceTo(now, Link.Params, GhostCfg);
            }
            if (gone != null) foreach (string id in gone) _others.Remove(id);
        }

        /// <summary>One HUD line: state, RTT, scale, rejections, queue, last correction (no secrets).</summary>
        public string StatusLine()
        {
            string rtt = double.IsNaN(Session.LastRtt) ? "-" : (Session.LastRtt * 1000).ToString("F0") + " ms";
            return $"Net {Session.State}{(Session.IsAdmin ? " admin" : "")}  RTT {rtt}  x{Session.TimeScale:G3}  rej {Rejections}" +
                   $"{(LastRejection != null ? " (" + LastRejection + ")" : "")}  queue {Session.Uplink.Pending}  corr {LastCorrection}";
        }

        public void Dispose()
        {
            try { Link.Flush(); } catch (Exception) { }
            Session.Dispose();
        }
    }
}
