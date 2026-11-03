using System;
using NUnit.Framework;
using Orbital.Core.Flight;
using Orbital.Core.Net;

namespace Orbital.Tests
{
    /// <summary>Stage 2c.6: fixes found by the online smoke run of the player against the real server.</summary>
    public class Stage2c6Tests
    {
        private static FlightState Burning(double t, double a)
        {
            FlightState s = FlightKernel.Fresh(FlightConfig.Default());
            s.Mode = FlightMode.Flying;
            s.Position = new Vec3(0, 7.0e6 + 0.5 * a * t * t, 0);
            s.Velocity = new Vec3(0, a * t, 0);
            return s;
        }

        [Test]
        public void History_AheadOfTheNewest_KeepsTheAcceleration()
        {
            // under thrust (10 m/s^2) a server state 0.08 s past the newest entry must not look like a 0.8 m/s velocity error
            const double a = 10.0;
            StateHistory h = new StateHistory();
            for (int i = 0; i <= 500; i++) h.Push(i * 0.02, Burning(i * 0.02, a));
            double t = 10.0 + 0.08;
            Assert.IsTrue(h.TrySample(t, out HistorySample s, 0.5));
            FlightState truth = Burning(t, a);
            Assert.Less(Vec3.Length(s.Velocity - truth.Velocity), 1e-9);
            Assert.Less(Vec3.Length(s.Position - truth.Position), 1e-6);
            CraftStateDto server = StateMapper.FromFlightState(truth, t, "c");
            Assert.AreEqual(ReconcileAction.Ignore, Reconciler.Compare(server, h, null, double.PositiveInfinity, ReconcileConfig.Default()).Action);
        }

        [Test]
        public void Reconcile_BothCrashed_IsNotACorrection()
        {
            StateHistory h = new StateHistory();
            FlightState wreck = Burning(0, 0);
            wreck.Mode = FlightMode.Crashed;
            h.Push(5.0, wreck);
            CraftStateDto server = StateMapper.FromFlightState(wreck, 5.0, "c");
            server.Mode = CraftModeDto.Crashed;
            Assert.AreEqual(ReconcileAction.Ignore, Reconciler.Compare(server, h, null, double.PositiveInfinity, ReconcileConfig.Default()).Action);
            // the client still flies: the server verdict wins
            FlightState flying = Burning(0, 0);
            h.Push(6.0, flying);
            server.Time = 6.0;
            Assert.AreEqual("crashed", Reconciler.Compare(server, h, null, double.PositiveInfinity, ReconcileConfig.Default()).Reason);
        }
    }
}
