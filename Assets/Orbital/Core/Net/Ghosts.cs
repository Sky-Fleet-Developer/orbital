using System;
using Orbital.Core.Flight;

namespace Orbital.Core.Net
{
    /// <summary>Carries a flight state forward with the kernel and no pilot input (stage 2c.4b).</summary>
    public static class Propagation
    {
        /// <summary>
        /// Steps from <paramref name="from"/> to <paramref name="to"/> with steps of at most <paramref name="maxStep"/> (the last one shorter).
        /// No pilot input: the throttle goes nowhere (a state from the server has 0), the SAS keeps the state's switch with the gyro only
        /// (the RCS stays off, so no propellant is burned by a guess).
        /// </summary>
        public static void Advance(ref FlightState s, in FlightParams p, double from, double to, double maxStep)
        {
            if (!(maxStep > 0)) maxStep = 0.02;
            ControlInput input = default;
            input.SasOn = s.SasOn != 0;
            double t = from;
            int guard = 0;
            while (to - t > 1e-9 && guard++ < 10_000_000)
            {
                double dt = Math.Min(maxStep, to - t);
                FlightKernel.Step(ref s, input, p, t, dt, out _);
                t += dt;
            }
        }

        /// <summary>
        /// The same with the sphere of influence rule after each step (stage 3.2): on a change the state moves into the new body's frame and
        /// <paramref name="p"/> takes its gravity and ground. Without a table (or an unknown parent) it is the plain one-body advance.
        /// </summary>
        public static void Advance(ref FlightState s, ref FlightParams p, BodyFrameTable frames, ref int parent, double from, double to,
            double maxStep)
        {
            if (frames == null || parent < 0)
            {
                Advance(ref s, p, from, to, maxStep);
                return;
            }
            if (!(maxStep > 0)) maxStep = 0.02;
            ControlInput input = default;
            input.SasOn = s.SasOn != 0;
            double t = from;
            int guard = 0;
            while (to - t > 1e-9 && guard++ < 10_000_000)
            {
                double dt = Math.Min(maxStep, to - t);
                FlightKernel.Step(ref s, input, p, t, dt, out _);
                t += dt;
                SoiRule.Step(frames, ref parent, ref s, ref p, t);
            }
        }
    }

    public struct GhostConfig
    {
        /// <summary>Kernel step for the extrapolation (0.02 s like the own craft; long gaps are crossed with <see cref="CatchUpStep"/>).</summary>
        public double Step;
        /// <summary>Step for catching up a gap longer than <see cref="CatchUpAfter"/> (the kernel takes up to 0.5 s).</summary>
        public double CatchUpStep;
        public double CatchUpAfter;
        /// <summary>The shown position reaches a new server state with this time constant.</summary>
        public double ViewTau;
        /// <summary>
        /// Stage 2c.8: a burn known from a thrust tail is carried on at most this long past the end of its last segment (game seconds; the
        /// sender closes a segment every 5 s at the latest, so a longer silence means the burn stopped or the link did).
        /// </summary>
        public double ThrustTailMax;
        /// <summary>
        /// Stage 2c.8: a state from the slow channel (get_state of all, no thrust tail) is skipped this long (game seconds) after a state
        /// of the near group: it knows less about the burn and would undo the tail.
        /// </summary>
        public double DetailedHold;

        public static GhostConfig Default() => new GhostConfig
        {
            Step = 0.02, CatchUpStep = 0.25, CatchUpAfter = 2.0, ViewTau = 0.5, ThrustTailMax = 10.0, DetailedHold = 5.0,
        };
    }

    /// <summary>
    /// Another player's craft (stage 2c.4b): the last server state carried to the present by the kernel with no input. A new server state
    /// replaces the physics at once; the shown position keeps the old place and catches up (<see cref="ViewOffset"/>), so the craft does not
    /// jump on screen. One instance per craft; the caller gives the parameters of the body it is around.
    /// </summary>
    public sealed class Ghost
    {
        public string CraftId { get; private set; }
        public string Callsign { get; private set; }
        public FlightState State;
        public double Time { get; private set; } = double.NaN;
        public ViewSmoothing View;
        public bool HasState => !double.IsNaN(Time);
        /// <summary>
        /// The last server state was in the surface frame (stage 2c.8): the ghost is a point of the rotating ground, carried to any time
        /// (also back, when the state is ahead of the client clock) by <see cref="Surface.Pin"/>, never by the kernel.
        /// </summary>
        public bool OnSurface { get; private set; }

        /// <summary>
        /// The mode as the server last said it (stage 2c.8): what to show. Only the server decides a crash of another craft; the physics
        /// below may only predict a touchdown and then holds the ghost at that ground point (<see cref="FlightMode.Landed"/>).
        /// </summary>
        public int ServerMode { get; private set; }

        /// <summary>Game time up to which the ghost carries a burn from a thrust tail (NaN: no burn).</summary>
        public double ThrustUntil { get; private set; } = double.NaN;
        private double _lastDetailed = double.NaN;

        /// <summary>
        /// Stage 3.2: the world's bodies. With a table the ghost is carried around the body of its own server state (its gravity and ground,
        /// not the caller's) and changes the body by the sphere of influence rule; without one it uses the parameters the caller passes.
        /// </summary>
        public BodyFrameTable Frames;
        private int _parent = -1;
        private string _serverParentId;
        private FlightParams _params;

        /// <summary>Index of the body the ghost is around in <see cref="Frames"/> (-1 without a table or for an unknown body).</summary>
        public int Parent => _parent;
        /// <summary>The body the ghost is around: from the table after a change by the rule, else as the server said.</summary>
        public string ParentId => Frames != null && _parent >= 0 ? Frames.IdOf(_parent) : _serverParentId;
        private bool OwnBody => Frames != null && _parent >= 0;

        public Ghost(string craftId)
        {
            CraftId = craftId;
        }

        /// <summary>Position to show: physics plus the decaying offset.</summary>
        public Vec3 ShownPosition => State.Position + View.Offset;

        /// <summary>A server state of this craft arrived; <paramref name="now"/> is the client game time.</summary>
        public void OnServerState(CraftStateDto server, in FlightParams p, double now, in GhostConfig cfg)
        {
            if (server == null) return;
            if (HasState && server.Time < Time - 1e-9 && server.Time < now - 60.0) return; // much older than what we have
            if (!server.Detailed && !double.IsNaN(_lastDetailed) && Math.Abs(server.Time - _lastDetailed) < cfg.DetailedHold) return;
            if (server.Detailed) _lastDetailed = server.Time;
            if (HasState && OnSurface) AdvanceTo(now, p, cfg); // compare with where the ground point is now, not where it was shown last
            Vec3 shownBefore = ShownPosition;
            bool had = HasState;
            int parentBefore = _parent;
            _serverParentId = server.ParentId;
            if (Frames != null)
            {
                _parent = Frames.IndexOf(server.ParentId);
                _params = p;
                if (_parent >= 0) Frames.ApplyTo(_parent, ref _params);
                if (had && parentBefore >= 0 && _parent >= 0 && parentBefore != _parent)
                {
                    // the shown place in the frame of the new body (the frames differ by an offset only)
                    Vec3 dummy = Vec3.Zero;
                    Frames.Convert(parentBefore, _parent, now, ref shownBefore, ref dummy);
                }
            }
            FlightParams body = OwnBody ? _params : p;
            Callsign = server.Callsign ?? Callsign;
            State = StateMapper.ToFlightState(server);
            ServerMode = State.Mode;
            Time = server.Time;
            ThrustUntil = double.NaN;
            ThrustTailDto tail = server.Thrust;
            if (tail != null && State.Mode == FlightMode.Flying && tail.Time <= server.Time && tail.Throttle > 0 && State.MainFuel > 0)
            {
                // the server's state is ballistic since the end of the last segment: start there and carry the burn on (stage 2c.8)
                State.Position = StateMapper.Vec(tail.Position, State.Position);
                State.Velocity = StateMapper.Vec(tail.Velocity, State.Velocity);
                State.MainFuel = tail.Fuel;
                Vec3 dir = Vec3.Normalize(StateMapper.Vec(tail.Direction, DQuat.Rotate(State.Orientation, Vec3.UnitY)));
                Vec3 axis = DQuat.Rotate(State.Orientation, Vec3.UnitY);
                State.Orientation = DQuat.Normalize(DQuat.Multiply(DQuat.FromTo(axis, dir), State.Orientation));
                State.Omega = Vec3.Zero;
                State.Throttle = Math.Min(1.0, tail.Throttle);
                Time = tail.Time;
                ThrustUntil = tail.Time + cfg.ThrustTailMax;
            }
            OnSurface = StateMapper.IsSurface(server) && server.Latitude.HasValue && server.Longitude.HasValue;
            if (OnSurface)
            {
                Surface.Pin(body.Surface, ref State, Time, now);
                Time = now;
            }
            else if (now > Time) AdvanceTo(now, p, cfg);
            // a past state may be newer than the extrapolation was: in both cases the view moves smoothly from where it was
            View.Offset = had ? shownBefore - State.Position : Vec3.Zero;
        }

        /// <summary>Carries the ghost to <paramref name="now"/> and lets the view catch up by the elapsed time.</summary>
        public void AdvanceTo(double now, in FlightParams p, in GhostConfig cfg)
        {
            if (!HasState || now == Time) return;
            if (OnSurface)
            {
                Surface.Pin(OwnBody ? _params.Surface : p.Surface, ref State, Time, now);
                View.Decay(Math.Abs(now - Time), cfg.ViewTau);
                Time = now;
                return;
            }
            if (now < Time) return;
            double gap = now - Time;
            double step = gap > cfg.CatchUpAfter ? cfg.CatchUpStep : cfg.Step;
            if (!double.IsNaN(ThrustUntil))
            {
                // the burn of the tail (the kernel keeps the throttle with no input), the engine off after its horizon
                double burnTo = Math.Min(now, ThrustUntil);
                if (burnTo > Time) Carry(p, Time, burnTo, Math.Min(step, cfg.Step));
                if (now >= ThrustUntil)
                {
                    State.Throttle = 0.0;
                    ThrustUntil = double.NaN;
                    if (now > burnTo) Carry(p, burnTo, now, step);
                }
            }
            else Carry(p, Time, now, step);
            // a predicted hard touchdown is not a crash: the ghost waits on its impact point for the server's word
            if (State.Mode == FlightMode.Crashed && ServerMode != FlightMode.Crashed) State.Mode = FlightMode.Landed;
            View.Decay(gap, cfg.ViewTau);
            Time = now;
        }

        private void Carry(in FlightParams p, double from, double to, double step)
        {
            if (OwnBody) Propagation.Advance(ref State, ref _params, Frames, ref _parent, from, to, step);
            else Propagation.Advance(ref State, p, from, to, step);
        }
    }
}
