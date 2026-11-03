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

        public static GhostConfig Default() => new GhostConfig { Step = 0.02, CatchUpStep = 0.25, CatchUpAfter = 2.0, ViewTau = 0.5 };
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
            Vec3 shownBefore = ShownPosition;
            bool had = HasState;
            Callsign = server.Callsign ?? Callsign;
            State = StateMapper.ToFlightState(server);
            Time = server.Time;
            if (now > Time) AdvanceTo(now, p, cfg);
            // a past state may be newer than the extrapolation was: in both cases the view moves smoothly from where it was
            View.Offset = had ? shownBefore - State.Position : Vec3.Zero;
        }

        /// <summary>Carries the ghost to <paramref name="now"/> and lets the view catch up by the elapsed time.</summary>
        public void AdvanceTo(double now, in FlightParams p, in GhostConfig cfg)
        {
            if (!HasState || now <= Time) return;
            double gap = now - Time;
            double step = gap > cfg.CatchUpAfter ? cfg.CatchUpStep : cfg.Step;
            Propagation.Advance(ref State, p, Time, now, step);
            View.Decay(gap, cfg.ViewTau);
            Time = now;
        }
    }
}
