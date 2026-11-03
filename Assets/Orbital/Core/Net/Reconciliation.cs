using System;
using Orbital.Core.Flight;

namespace Orbital.Core.Net
{
    /// <summary>One recorded moment of the own craft: what a server state is compared with.</summary>
    public struct HistorySample
    {
        public double Time;
        public Vec3 Position;
        public Vec3 Velocity;
        public int Mode;
        public double MainFuel;
        public double RcsFuel;
        public double Latitude;
        public double Longitude;
        /// <summary>Stage 3.2: index of the body whose frame the position is in (<see cref="StateHistory.Frames"/>); -1 = the one body.</summary>
        public int Parent;
    }

    /// <summary>
    /// The own craft after each step (stage 2c.4b): a ring of about 4096 steps (82 s at 0.02 s). Positions between two entries come from
    /// the cubic Hermite through both positions and velocities, velocities from its derivative, fuel linearly. Preallocated, no
    /// allocations per step.
    /// </summary>
    public sealed class StateHistory
    {
        private readonly HistorySample[] _ring;
        private int _head;   // index of the oldest
        private int _count;

        public StateHistory(int capacity = 4096)
        {
            _ring = new HistorySample[Math.Max(2, capacity)];
        }

        /// <summary>
        /// Stage 3.2: the world's bodies, for entries in different frames (a sphere of influence change inside the history): two neighbours
        /// are blended in the frame of the older one. Null = one body.
        /// </summary>
        public BodyFrameTable Frames;

        public int Count => _count;
        public int Capacity => _ring.Length;
        public double OldestTime => _count > 0 ? _ring[_head].Time : double.NaN;
        public double NewestTime => _count > 0 ? At(_count - 1).Time : double.NaN;

        public void Clear()
        {
            _head = 0;
            _count = 0;
        }

        /// <summary>Adds the state after a step ending at <paramref name="time"/>. Times must grow; an older or equal time drops the newer entries first.</summary>
        public void Push(double time, in FlightState s, int parent = -1)
        {
            while (_count > 0 && At(_count - 1).Time >= time) _count--;
            HistorySample h = new HistorySample
            {
                Time = time, Position = s.Position, Velocity = s.Velocity, Mode = s.Mode, MainFuel = s.MainFuel, RcsFuel = s.RcsFuel,
                Latitude = s.Latitude, Longitude = s.Longitude, Parent = parent,
            };
            if (_count < _ring.Length)
            {
                _ring[(_head + _count) % _ring.Length] = h;
                _count++;
            }
            else
            {
                _ring[_head] = h;
                _head = (_head + 1) % _ring.Length;
            }
        }

        public HistorySample At(int i) => _ring[(_head + i) % _ring.Length];

        /// <summary>
        /// The state at <paramref name="t"/>: interpolated inside the history, extrapolated linearly at most <paramref name="maxAhead"/>
        /// past the newest entry (the server clock may be a hair ahead). False before the oldest entry or further ahead.
        /// </summary>
        public bool TrySample(double t, out HistorySample h, double maxAhead = 0.0)
        {
            h = default;
            if (_count == 0 || double.IsNaN(t)) return false;
            HistorySample newest = At(_count - 1);
            if (t >= newest.Time)
            {
                if (t - newest.Time > maxAhead) return false;
                // ahead of the newest entry: carried on with the acceleration of the last step (under thrust a constant velocity is off by
                // a*dt: 0.8 m/s for 0.08 s, enough for a false soft correction)
                double ahead = t - newest.Time;
                Vec3 acc = Vec3.Zero;
                if (_count >= 2)
                {
                    HistorySample prev = InFrameOf(At(_count - 2), newest.Parent);
                    double span0 = newest.Time - prev.Time;
                    if (span0 > 0 && prev.Mode == newest.Mode) acc = (newest.Velocity - prev.Velocity) * (1.0 / span0);
                }
                h = newest;
                h.Position = newest.Position + newest.Velocity * ahead + acc * (0.5 * ahead * ahead);
                h.Velocity = newest.Velocity + acc * ahead;
                h.Time = t;
                return true;
            }
            if (t < At(0).Time) return false;
            int lo = 0, hi = _count - 1; // At(lo).Time <= t < At(hi).Time
            while (hi - lo > 1)
            {
                int mid = (lo + hi) / 2;
                if (At(mid).Time <= t) lo = mid; else hi = mid;
            }
            HistorySample a = At(lo), b = InFrameOf(At(hi), At(lo).Parent);
            double span = b.Time - a.Time;
            double u = span > 0 ? (t - a.Time) / span : 0.0;
            double u2 = u * u, u3 = u2 * u;
            double h00 = 2 * u3 - 3 * u2 + 1, h10 = u3 - 2 * u2 + u, h01 = -2 * u3 + 3 * u2, h11 = u3 - u2;
            double d00 = (6 * u2 - 6 * u) / span, d10 = 3 * u2 - 4 * u + 1, d01 = (-6 * u2 + 6 * u) / span, d11 = 3 * u2 - 2 * u;
            h = a;
            h.Time = t;
            h.Position = a.Position * h00 + a.Velocity * (h10 * span) + b.Position * h01 + b.Velocity * (h11 * span);
            h.Velocity = a.Position * d00 + a.Velocity * d10 + b.Position * d01 + b.Velocity * d11;
            h.MainFuel = a.MainFuel + (b.MainFuel - a.MainFuel) * u;
            h.RcsFuel = a.RcsFuel + (b.RcsFuel - a.RcsFuel) * u;
            if (a.Mode != b.Mode && u > 0.5) h.Mode = b.Mode; // a touchdown or lift-off inside the step: the nearer side
            return true;
        }

        /// <summary>The entry moved into the frame of body <paramref name="parent"/> at its own time (as is without a table or the same body).</summary>
        private HistorySample InFrameOf(HistorySample h, int parent)
        {
            if (Frames == null || h.Parent == parent || h.Parent < 0 || parent < 0) return h;
            Frames.Convert(h.Parent, parent, h.Time, ref h.Position, ref h.Velocity);
            h.Parent = parent;
            return h;
        }

        /// <summary>A fuel correction: every entry from <paramref name="fromTime"/> on moves by the same amounts (never below zero).</summary>
        public void ShiftFuel(double fromTime, double dMain, double dRcs)
        {
            if (dMain == 0 && dRcs == 0) return;
            for (int i = 0; i < _count; i++)
            {
                int k = (_head + i) % _ring.Length;
                if (_ring[k].Time < fromTime) continue;
                _ring[k].MainFuel = Math.Max(0.0, _ring[k].MainFuel + dMain);
                _ring[k].RcsFuel = Math.Max(0.0, _ring[k].RcsFuel + dRcs);
            }
        }

        /// <summary>
        /// A soft correction found at <paramref name="fromTime"/>: every entry from then on moves by dp + dv*(t - fromTime) and dv. A difference
        /// of two states is the same in every body frame (the frames share the axes and differ by an offset), so entries in other frames move
        /// by the same amounts.
        /// </summary>
        public void Shift(double fromTime, Vec3 dp, Vec3 dv)
        {
            for (int i = 0; i < _count; i++)
            {
                int k = (_head + i) % _ring.Length;
                if (_ring[k].Time < fromTime) continue;
                _ring[k].Position = _ring[k].Position + dp + dv * (_ring[k].Time - fromTime);
                _ring[k].Velocity = _ring[k].Velocity + dv;
            }
        }
    }

    public struct ReconcileConfig
    {
        public double IgnorePosition;   // m: a smaller difference is left alone (1)
        public double IgnoreVelocity;   // m/s (0.05)
        public double SoftPosition;     // m: up to this the physics is corrected at once and the view catches up (2000)
        public double SoftVelocity;     // m/s (20)
        public double ViewTau;          // s: the view catches up with this time constant (0.5)
        public double GroundIgnore;     // m: on the ground a smaller difference is left alone (2)
        public double MaxAhead;         // s: a server state up to this far past the newest history entry is compared by extrapolation

        public static ReconcileConfig Default() => new ReconcileConfig
        {
            IgnorePosition = 1.0, IgnoreVelocity = 0.05, SoftPosition = 2000.0, SoftVelocity = 20.0, ViewTau = 0.5, GroundIgnore = 2.0,
            MaxAhead = 0.5,
        };
    }

    public enum ReconcileAction
    {
        /// <summary>The client recorded thrust before this state that the server has not accepted yet: the state says nothing yet.</summary>
        Stale,
        /// <summary>Older than the history: nothing to compare with (the fuel is still taken).</summary>
        TooOld,
        Ignore,
        Soft,
        Hard,
    }

    public struct ReconcileResult
    {
        public ReconcileAction Action;
        public double Time;
        /// <summary>Server minus client at <see cref="Time"/>.</summary>
        public Vec3 Dp;
        public Vec3 Dv;
        /// <summary>For Hard: mode, parent, ahead, size.</summary>
        public string Reason;
    }

    /// <summary>The view lags behind a soft correction and catches up exponentially.</summary>
    public struct ViewSmoothing
    {
        /// <summary>Add this to the physics position to get the shown position.</summary>
        public Vec3 Offset;

        public void Decay(double dt, double tau)
        {
            Offset = tau > 0 ? Offset * Math.Exp(-Math.Max(0.0, dt) / tau) : Vec3.Zero;
        }
    }

    /// <summary>
    /// Compares a server state of the own craft with the client history and corrects the client (stage 2c.4b). The server is the
    /// authority for position, velocity, mode, parent and fuel; the client for attitude. A state is used only when no thrust the client
    /// recorded before its time is still unconfirmed (the caller passes the earliest such start).
    /// </summary>
    public static class Reconciler
    {
        public static ReconcileResult Compare(CraftStateDto server, StateHistory history, string parentId, double pendingThrustStart,
            in ReconcileConfig cfg) => Compare(server, history, parentId, pendingThrustStart, cfg, null);

        /// <summary>
        /// Stage 3.2: with the world's bodies (<paramref name="frames"/>) a server state around another body than the client's is converted
        /// into the client's frame at its time and compared as usual (both sides apply the same sphere of influence rule, so near a border
        /// they may differ by a step). A body the table does not know, or a different body on the ground, is a hard correction.
        /// </summary>
        public static ReconcileResult Compare(CraftStateDto server, StateHistory history, string parentId, double pendingThrustStart,
            in ReconcileConfig cfg, BodyFrameTable frames)
        {
            ReconcileResult r = new ReconcileResult { Time = server.Time };
            if (pendingThrustStart < server.Time)
            {
                r.Action = ReconcileAction.Stale;
                return r;
            }
            int serverMode = StateMapper.ModeOf(server.Mode);
            if (serverMode == FlightMode.Crashed)
            {
                // both agree on the crash: nothing to correct (else every state of the wreck until craft_destroyed is a hard correction)
                if (history.Count > 0 && history.At(history.Count - 1).Mode == FlightMode.Crashed)
                {
                    r.Action = ReconcileAction.Ignore;
                    return r;
                }
                return Hard(r, "crashed");
            }
            int serverParent = -1;
            if (frames != null)
            {
                serverParent = frames.IndexOf(server.ParentId);
                if (serverParent < 0) return Hard(r, "parent");
            }
            else if (parentId != null && server.ParentId != null && server.ParentId != parentId) return Hard(r, "parent");
            if (!history.TrySample(server.Time, out HistorySample mine, cfg.MaxAhead))
            {
                if (history.Count > 0 && server.Time < history.OldestTime)
                {
                    r.Action = ReconcileAction.TooOld;
                    return r;
                }
                return Hard(r, "ahead");
            }
            if (mine.Mode != serverMode) return Hard(r, "mode");
            Vec3 sp = StateMapper.Vec(server.Position, mine.Position), sv = StateMapper.Vec(server.Velocity, mine.Velocity);
            if (frames != null && serverParent != mine.Parent)
            {
                if (mine.Parent < 0 || serverMode != FlightMode.Flying) return Hard(r, "parent");
                frames.Convert(serverParent, mine.Parent, server.Time, ref sp, ref sv);
            }
            r.Dp = sp - mine.Position;
            r.Dv = sv - mine.Velocity;
            double dp = Vec3.Length(r.Dp), dv = Vec3.Length(r.Dv);
            if (serverMode == FlightMode.Landed)
            {
                r.Action = dp <= cfg.GroundIgnore ? ReconcileAction.Ignore : ReconcileAction.Hard;
                if (r.Action == ReconcileAction.Hard) r.Reason = "ground";
                return r;
            }
            if (dp <= cfg.IgnorePosition && dv <= cfg.IgnoreVelocity) r.Action = ReconcileAction.Ignore;
            else if (dp <= cfg.SoftPosition && dv <= cfg.SoftVelocity) r.Action = ReconcileAction.Soft;
            else return Hard(r, "size");
            return r;
        }

        /// <summary>
        /// Both sides have the own craft crashed (stage 2c.8): the client's mode is crashed and the server's last state of this craft is
        /// crashed with its crash time. A crashed server state while the client flies is a ballistic guess a pending segment may still
        /// cancel (the retro window), not a confirmation.
        /// </summary>
        public static bool CrashConfirmed(int clientMode, CraftStateDto lastServer, string craftId) =>
            clientMode == FlightMode.Crashed && lastServer != null && craftId != null && lastServer.CraftId == craftId &&
            lastServer.Mode == CraftModeDto.Crashed && lastServer.CrashedAt.HasValue;

        private static ReconcileResult Hard(ReconcileResult r, string reason)
        {
            r.Action = ReconcileAction.Hard;
            r.Reason = reason;
            return r;
        }

        /// <summary>
        /// Soft correction: the physics moves at once (the difference carried to now linearly), the history shifts with it, the view keeps
        /// showing the old place and catches up through <paramref name="view"/>.
        /// </summary>
        public static void ApplySoft(ref FlightState now, double nowTime, StateHistory history, in ReconcileResult r, ref ViewSmoothing view)
        {
            Vec3 shift = r.Dp + r.Dv * (nowTime - r.Time);
            now.Position = now.Position + shift;
            now.Velocity = now.Velocity + r.Dv;
            history.Shift(r.Time, r.Dp, r.Dv);
            view.Offset = view.Offset - shift;
        }

        /// <summary>
        /// Fuel is always the server's: its value at the state's time minus what the client burned since (recorded thrust the server does
        /// not know yet). Not for a Stale state (the server value then misses thrust before its time). Never below zero.
        /// </summary>
        public static void ApplyFuel(ref FlightState now, CraftStateDto server, StateHistory history, double maxAhead = 0.5)
        {
            double usedMain = 0, usedRcs = 0;
            if (history.TrySample(server.Time, out HistorySample then, maxAhead))
            {
                usedMain = Math.Max(0.0, then.MainFuel - now.MainFuel);
                usedRcs = Math.Max(0.0, then.RcsFuel - now.RcsFuel);
            }
            now.MainFuel = Math.Max(0.0, server.Fuel - usedMain);
            now.RcsFuel = Math.Max(0.0, server.RcsFuel - usedRcs);
        }

        /// <summary>
        /// Hard correction: the server state (attitude included) carried from its time to <paramref name="nowTime"/> by the kernel with no
        /// pilot input. The caller clears the history, the recorder and the send queue (the recorded past is no longer the craft's).
        /// The throttle stays at the client's lever position.
        /// </summary>
        /// <summary>
        /// Stage 3.2: the hard correction around the server's body: <paramref name="p"/> takes that body's gravity and ground and the carry to
        /// now applies the sphere of influence rule, so <paramref name="parent"/> is the body at <paramref name="nowTime"/>. An unknown body
        /// (-1) leaves <paramref name="p"/> as is.
        /// </summary>
        public static FlightState ApplyHard(CraftStateDto server, ref FlightParams p, BodyFrameTable frames, out int parent, double nowTime,
            double step, double throttle)
        {
            parent = frames != null ? frames.IndexOf(server.ParentId) : -1;
            if (parent < 0) return ApplyHard(server, p, nowTime, step, throttle);
            frames.ApplyTo(parent, ref p);
            FlightState s = StateMapper.ToFlightState(server);
            if (nowTime > server.Time) Propagation.Advance(ref s, ref p, frames, ref parent, server.Time, nowTime, step);
            s.Throttle = s.MainFuel > 0 ? throttle : 0.0;
            return s;
        }

        public static FlightState ApplyHard(CraftStateDto server, in FlightParams p, double nowTime, double step, double throttle)
        {
            FlightState s = StateMapper.ToFlightState(server);
            if (nowTime > server.Time) Propagation.Advance(ref s, p, server.Time, nowTime, step);
            s.Throttle = s.MainFuel > 0 ? throttle : 0.0;
            return s;
        }
    }
}
