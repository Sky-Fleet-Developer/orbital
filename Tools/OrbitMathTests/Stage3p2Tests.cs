using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using Ara3D;
using NUnit.Framework;
using Orbital.Core.Flight;
using Orbital.Core.Navigation;
using Orbital.Core.Net;
using Orbital.Core.TrajectorySystem;
using Orbital.Core.Worlds;

namespace Orbital.Tests
{
    /// <summary>
    /// Stage 3.2: the client changes the body of its own craft and of the ghosts by the sphere of influence rule (Core/Flight/SoiFrames.cs):
    /// the kernel steps around one body, the rule runs after the step; the history, the reconciliation and the recorder know the body.
    /// </summary>
    public class Stage3p2Tests
    {
        private static string RepoDir([CallerFilePath] string file = "") => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(file), "..", ".."));

        private static BodyFrameTable _table;
        private static BodyFrameTable Table => _table ??= WorldFrames.Build(WorldFileLoader.Parse(
            File.ReadAllText(Path.Combine(RepoDir(), "Assets", "_game", "Config", WorldFileHash.FileName))));

        private static int Id(string id)
        {
            int i = Table.IndexOf(id);
            Assert.That(i, Is.GreaterThanOrEqualTo(0), id);
            return i;
        }

        private static FlightParams ParamsAround(int body)
        {
            FlightParams p = FlightFixture.Params();
            p.Craft.ThrustOffset = Vec3.Zero;
            Table.ApplyTo(body, ref p);
            return p;
        }

        private static FlightState Flying(Vec3 pos, Vec3 vel)
        {
            FlightState s = FlightKernel.Fresh(FlightFixture.Params().Craft);
            s.Mode = FlightMode.Flying;
            s.Position = pos;
            s.Velocity = vel;
            return s;
        }

        private static DVector3 D(Vec3 v) => new DVector3(v.X, v.Y, v.Z);
        private static Vec3 V(DVector3 v) => new Vec3(v.x, v.y, v.z);

        [Test]
        public void Ballistic_LeavesPhobos_WithinAStepOfTheAnalyticExit()
        {
            int phobos = Id("phobos"), mars = Id("mars");
            ref readonly BodyFrame pb = ref Table[phobos];
            double r0 = pb.Radius + 800.0, t0 = 1.0e5;
            Vec3 pos = new Vec3(r0, 0, 0), vel = new Vec3(0, 0, 1.5 * Math.Sqrt(2 * pb.Nu / r0));
            StaticOrbit kepler = new StaticOrbit {Nu = pb.Nu};
            kepler.Calculate(D(pos), D(vel), t0);
            Assert.That(kepler.TryFindExit(pb.SoiRadius * SoiRule.ExitFactor, t0, out double exit), Is.True);

            FlightState s = Flying(pos, vel);
            FlightParams p = ParamsAround(phobos);
            int parent = phobos;
            double t = t0;
            while (parent == phobos && t < t0 + 3600)
            {
                Propagation.Advance(ref s, ref p, Table, ref parent, t, t + 0.02, 0.02);
                t += 0.02;
            }
            Assert.That(parent, Is.EqualTo(mars));
            Assert.That(t - exit, Is.InRange(-1e-6, 0.02 + 1e-6), "the change is the first step past the analytic exit");
            Assert.That(p.Nu, Is.EqualTo(Table[mars].Nu), "the gravity of the new body");
            Assert.That(p.Surface.Radius, Is.EqualTo(Table[mars].Radius), "the ground of the new body");
            // the global state across the change: the kernel around Phobos to the step before, the Kepler orbit there
            (DVector3 kp, DVector3 kv) = kepler.GetSample(t);
            Table.GlobalState(phobos, t, out Vec3 gp, out Vec3 gv);
            Table.GlobalState(mars, t, out Vec3 gm, out Vec3 gvm);
            Assert.That(Vec3.Length(s.Position + gm - (V(kp) + gp)), Is.LessThan(1.0), "position after the change");
            Assert.That(Vec3.Length(s.Velocity + gvm - (V(kv) + gv)), Is.LessThan(1e-2), "velocity after the change");
        }

        [Test]
        public void Ballistic_EntersPhobos_AtItsSphere()
        {
            int phobos = Id("phobos"), mars = Id("mars");
            double soi = Table[phobos].SoiRadius, t = 2.0e5;
            Table.LocalState(phobos, t, out Vec3 pp, out Vec3 pv);
            Vec3 dir = Vec3.Normalize(new Vec3(0.3, 0.2, 1.0));
            FlightState s = Flying(pp + dir * (soi * 1.2), pv - dir * 20.0);
            FlightParams p = ParamsAround(mars);
            int parent = mars;
            double distBefore = double.NaN;
            for (int k = 0; k < 100_000 && parent == mars; k++)
            {
                Table.LocalState(phobos, t, out Vec3 ppNow, out _);
                distBefore = Vec3.Length(s.Position - ppNow);
                Propagation.Advance(ref s, ref p, Table, ref parent, t, t + 0.02, 0.02);
                t += 0.02;
            }
            Assert.That(parent, Is.EqualTo(phobos));
            Assert.That(distBefore, Is.GreaterThanOrEqualTo(soi), "outside before the step");
            Assert.That(Vec3.Length(s.Position), Is.LessThan(soi).And.GreaterThan(soi - 2.0), "just inside after it");
            Assert.That(p.Nu, Is.EqualTo(Table[phobos].Nu));
        }

        private static NetSession Offline() => new NetSession("ws://127.0.0.1:9", "soi-test");

        /// <summary>A craft in the Moon's frame just inside the exit radius, flying straight out.</summary>
        private static CraftStateDto LeavingTheMoon(double time, out int moon)
        {
            moon = Id("moon");
            double soi = Table[moon].SoiRadius;
            FlightState f = Flying(new Vec3(soi * 1.004, 0, 0), new Vec3(3000, 0, 0));
            f.Orientation = DQuat.FromTo(Vec3.UnitY, Vec3.UnitX);
            CraftStateDto dto = StateMapper.FromFlightState(f, time, "own");
            dto.ParentId = "moon";
            return dto;
        }

        [Test]
        public void OwnCraft_BurnAcrossTheBorder_ClosesTheSegment_WithAnExactSeam()
        {
            NetSession session = Offline();
            OwnCraftLink link = new OwnCraftLink(session) {Frames = Table, Params = ParamsAround(Id("earth"))};
            const double t0 = 3.0e5;
            link.TakeServerState(LeavingTheMoon(t0, out int moon), t0, "spawn");
            Assert.That(link.ParentId, Is.EqualTo("moon"));
            Assert.That(link.Params.Nu, Is.EqualTo(Table[moon].Nu), "the parameters of the server's body, not the caller's");
            ControlInput full = default;
            full.ThrottleFull = true;
            double change = double.NaN;
            for (int k = 0; k < 2000; k++)
            {
                link.Step(full, 0.02, link.Time);
                if (double.IsNaN(change) && link.ParentChanges > 0) change = link.Time;
            }
            link.Flush();
            Assert.That(link.ParentId, Is.EqualTo("earth"));
            Assert.That(link.ParentChanges, Is.EqualTo(1));
            List<ThrustSegment> segs = new List<ThrustSegment>();
            session.Uplink.CopySegments(segs);
            Assert.That(segs.Count, Is.GreaterThanOrEqualTo(2));
            int atBorder = segs.FindIndex(x => x.EndTime == change);
            Assert.That(atBorder, Is.GreaterThanOrEqualTo(0).And.LessThan(segs.Count - 1), "a segment ends at the step of the change");
            for (int k = 0; k + 1 < segs.Count; k++)
            {
                Assert.That(segs[k + 1].StartTime, Is.EqualTo(segs[k].EndTime), $"seam {k}");
                Assert.That(segs[k + 1].Start.Orientation.W, Is.EqualTo(segs[k].End.Orientation.W), $"attitude seam {k}");
            }
        }

        [Test]
        public void OwnCraft_ServerStateAroundTheOtherBody_IsNotAHardCorrection()
        {
            NetSession session = Offline();
            OwnCraftLink link = new OwnCraftLink(session) {Frames = Table, Params = ParamsAround(Id("earth"))};
            const double t0 = 3.0e5;
            link.TakeServerState(LeavingTheMoon(t0, out int moon), t0, "spawn");
            int earth = Id("earth");
            double change = double.NaN;
            for (int k = 0; k < 1500; k++)
            {
                link.Step(default, 0.02, link.Time);
                if (double.IsNaN(change) && link.ParentChanges > 0) change = link.Time;
            }
            Assert.That(link.ParentId, Is.EqualTo("earth"));
            // two server states taken from the client's own history: before the change told around the Earth (the server was a step faster)
            // and after it still around the Moon (the server was a step slower), plus one between two entries of different bodies
            foreach ((double dt, int serverBody) in new[] {(-1.0, earth), (1.0, moon), (-0.01, earth), (0.01, moon)})
            {
                double ts = change + dt;
                Assert.That(link.History.TrySample(ts, out HistorySample h), Is.True);
                Vec3 p = h.Position, v = h.Velocity;
                Table.Convert(h.Parent, serverBody, ts, ref p, ref v);
                FlightState f = Flying(p, v);
                f.MainFuel = h.MainFuel;
                f.RcsFuel = h.RcsFuel;
                CraftStateDto dto = StateMapper.FromFlightState(f, ts, "own");
                dto.ParentId = Table.IdOf(serverBody);
                ReconcileResult r = Reconciler.Compare(dto, link.History, null, double.PositiveInfinity, link.ReconcileCfg, Table);
                Assert.That(r.Action, Is.EqualTo(ReconcileAction.Ignore), $"dt {dt}: dp {Vec3.Length(r.Dp)} dv {Vec3.Length(r.Dv)}");
                if (dt == 1.0)
                {
                    // the old rule (no table) calls a different body a hard correction; an unknown body still is one
                    Assert.That(Reconciler.Compare(dto, link.History, "earth", double.PositiveInfinity, link.ReconcileCfg).Reason, Is.EqualTo("parent"));
                    dto.ParentId = "nowhere";
                    Assert.That(Reconciler.Compare(dto, link.History, null, double.PositiveInfinity, link.ReconcileCfg, Table).Reason, Is.EqualTo("parent"));
                }
            }
            Assert.That(link.Hard, Is.Zero);
        }

        [Test]
        public void Ghost_AroundAnotherBody_IsCarriedWithItsOwnGravity()
        {
            int moon = Id("moon");
            ref readonly BodyFrame mb = ref Table[moon];
            double r = mb.Radius + 100e3, t0 = 4.0e5;
            Vec3 pos = new Vec3(0, 0, r), vel = new Vec3(Math.Sqrt(mb.Nu / r), 0, 0);
            CraftStateDto dto = StateMapper.FromFlightState(Flying(pos, vel), t0, "other");
            dto.ParentId = "moon";
            FlightParams callers = ParamsAround(Id("earth")); // the caller's parameters: the own craft around the Earth
            Ghost g = new Ghost("other") {Frames = Table};
            GhostConfig cfg = GhostConfig.Default();
            g.OnServerState(dto, callers, t0, cfg);
            for (double t = t0 + 10; t <= t0 + 600; t += 10) g.AdvanceTo(t, callers, cfg);
            StaticOrbit kepler = new StaticOrbit {Nu = mb.Nu};
            kepler.Calculate(D(pos), D(vel), t0);
            (DVector3 kp, _) = kepler.GetSample(t0 + 600);
            Assert.That(g.ParentId, Is.EqualTo("moon"));
            Assert.That(Vec3.Length(g.State.Position - V(kp)), Is.LessThan(5.0), "around the Moon with the Moon's nu");

            // and one leaving the Moon changes its body by the rule
            Ghost h = new Ghost("leaving") {Frames = Table};
            h.OnServerState(LeavingTheMoon(t0, out _), callers, t0, cfg);
            h.AdvanceTo(t0 + 40, callers, cfg);
            Assert.That(h.ParentId, Is.EqualTo("earth"));
        }

        [Test]
        public void Rk4Drift_AgainstKepler_OverADayOfBallistics()
        {
            // the measurement for the journal (stage 3.2): the client's kernel (RK4, 0.02 s) against the server's Kepler on a ballistic day
            FlightParams p = FlightFixture.Params(0.0);
            p.Craft.ThrustOffset = Vec3.Zero;
            double nu = p.Nu;
            var cases = new (string name, Vec3 pos, Vec3 vel)[]
            {
                ("LEO 400 km", new Vec3(6.771e6, 0, 0), new Vec3(0, 0, Math.Sqrt(nu / 6.771e6))),
                ("transfer to the Moon", new Vec3(6.671e6, 0, 0),
                    new Vec3(0, 0, Math.Sqrt(nu * (2 / 6.671e6 - 2 / (6.671e6 + 3.84e8))))),
            };
            const double day = 86400, dt = 0.02;
            foreach (var c in cases)
            {
                FlightState s = Flying(c.pos, c.vel);
                Stopwatch sw = Stopwatch.StartNew();
                double t = 0;
                for (long k = 0; k < (long) (day / dt); k++)
                {
                    FlightKernel.Step(ref s, default, p, t, dt, out _);
                    t += dt;
                }
                sw.Stop();
                StaticOrbit kepler = new StaticOrbit {Nu = nu};
                kepler.Calculate(D(c.pos), D(c.vel), 0);
                (DVector3 kp, DVector3 kv) = kepler.GetSample(t);
                double dp = Vec3.Length(s.Position - V(kp)), dv = Vec3.Length(s.Velocity - V(kv));
                TestContext.Progress.WriteLine($"RK4 drift, {c.name}: {dp:F3} m, {dv * 1000:F3} mm/s after a day ({sw.Elapsed.TotalSeconds:F1} s)");
                Assert.That(dp, Is.LessThan(1000.0), c.name);
            }
        }
    }
}
