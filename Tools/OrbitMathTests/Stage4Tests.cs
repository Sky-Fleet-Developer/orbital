using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using Ara3D;
using NUnit.Framework;
using Orbital.Core.Config;
using Orbital.Core.Flight;
using Orbital.Core.TrajectorySystem;
using Orbital.Core.Worlds;

namespace Orbital.Tests
{
    /// <summary>Stage 4: the patched conics forecast of the map, and the descent instrument flown through the real kernel.</summary>
    public class Stage4Tests
    {
        private static string ConfigDir([CallerFilePath] string file = "") =>
            Path.GetFullPath(Path.Combine(Path.GetDirectoryName(file), "..", "..", "Assets", "_game", "Config"));

        private static BodyFrameTable _table;
        private static BodyFrameTable Table => _table ??= WorldFrames.Build(WorldFileLoader.Parse(File.ReadAllText(Path.Combine(ConfigDir(), WorldFileHash.FileName))));
        private static GameConfig Config => GameConfigLoader.FromDirectory(ConfigDir());

        private static Vec3 V(DVector3 v) => new Vec3(v.x, v.y, v.z);

        // A state on the conic, in the frame of the plot's frame body, the direct way: through the root.
        private static Vec3 TrueRelative(in TrajectorySegment s, int frame, double t)
        {
            Vec3 local = V(s.Orbit.GetPositionAtT(t));
            Table.GlobalState(s.Body, t, out Vec3 body, out _);
            Table.GlobalState(frame, t, out Vec3 f, out _);
            return body + local - f;
        }

        private static (Vec3 pos, Vec3 vel) LeavingEarth(double extraSpeed)
        {
            int earth = Table.IndexOf("earth");
            double r = Table[earth].Radius + 400000.0;
            return (new Vec3(r, 0, 0), new Vec3(0, 0, Math.Sqrt(2.0 * Table[earth].Nu / r) + extraSpeed));
        }

        [Test]
        public void Forecast_ClosedOrbit_OneSegment_OneRevolution()
        {
            int earth = Table.IndexOf("earth");
            double r = Table[earth].Radius + 400000.0;
            double circular = Math.Sqrt(Table[earth].Nu / r);
            TrajectorySegment[] segments = new TrajectorySegment[TrajectoryPlot.MaxSegments];
            int n = TrajectoryForecast.Build(Table, earth, new Vec3(r, 0, 0), new Vec3(0, 0, circular * 1.01), 100.0, segments);
            Assert.That(n, Is.EqualTo(1));
            Assert.That(segments[0].EndKind, Is.EqualTo(TrajectoryEnd.Closed));
            Assert.That(segments[0].End - segments[0].Start, Is.EqualTo(segments[0].Orbit.Period).Within(1e-6));
        }

        [Test]
        public void Forecast_FallingFromOrbit_EndsOnTheGround_AtTheGroundRadius()
        {
            int moon = Table.IndexOf("moon");
            double radius = Table[moon].Radius;
            double r = radius + 50000.0;
            double circular = Math.Sqrt(Table[moon].Nu / r);
            Vec3 pos = new Vec3(r, 0, 0), vel = new Vec3(0, 0, circular * 0.8);
            TrajectorySegment[] segments = new TrajectorySegment[TrajectoryPlot.MaxSegments];
            int n = TrajectoryForecast.Build(Table, moon, pos, vel, 0.0, segments);
            Assert.That(n, Is.EqualTo(1));
            Assert.That(segments[0].EndKind, Is.EqualTo(TrajectoryEnd.Impact));
            Assert.That(segments[0].End, Is.GreaterThan(0.0));
            Assert.That(segments[0].Orbit.GetPositionAtT(segments[0].End).Length(), Is.EqualTo(radius).Within(1.0));
        }

        [Test]
        public void Forecast_GoingUpOnAnArc_ComesBackToTheGround()
        {
            int moon = Table.IndexOf("moon");
            double r = Table[moon].Radius + 100.0;
            Vec3 pos = new Vec3(r, 0, 0), vel = new Vec3(200, 0, 150);
            Assert.That(SoiForecast.NextEvent(Table, moon, pos, vel, 10.0, out StaticOrbit orbit, out double when, out _), Is.EqualTo(TrajectoryEnd.Impact));
            Assert.That(when, Is.GreaterThan(100.0), "after the apocenter, not now");
            Assert.That(orbit.GetPositionAtT(when).Length(), Is.EqualTo(Table[moon].Radius).Within(1.0));
        }

        [Test]
        public void Forecast_AnEntryBeforeTheGround_IsAnEntry()
        {
            // down from the Moon's distance through the Moon's sphere, on a line that also ends on the Earth
            int earth = Table.IndexOf("earth"), moon = Table.IndexOf("moon");
            Table.LocalState(moon, 5000.0, out Vec3 moonAt, out Vec3 moonVel);
            Vec3 away = moonAt * (1.0 + 3.0 * Table[moon].SoiRadius / Vec3.Length(moonAt));
            Vec3 toMoon = Vec3.Normalize(moonAt - away);
            Vec3 pos = away, vel = toMoon * 1500.0 + moonVel;
            Assert.That(SoiForecast.TryNext(Table, earth, pos, vel, 5000.0, out double when, out int to), Is.True);
            Assert.That(to, Is.EqualTo(moon));
            Assert.That(when, Is.GreaterThan(5000.0));
        }

        [Test]
        public void Forecast_ThroughPhobosMarsDeimos_Chains_WithMatchingStates()
        {
            int phobos = Table.IndexOf("phobos"), mars = Table.IndexOf("mars");
            double r = Table[phobos].Radius + 2000.0;
            TrajectorySegment[] segments = new TrajectorySegment[TrajectoryPlot.MaxSegments];
            int n = TrajectoryForecast.Build(Table, phobos, new Vec3(r, 0, 0), new Vec3(30, 8, 0), 1000.0, segments);
            Assert.That(n, Is.GreaterThanOrEqualTo(2));
            Assert.That(segments[0].Body, Is.EqualTo(phobos));
            Assert.That(segments[0].EndKind, Is.EqualTo(TrajectoryEnd.LeaveBody));
            Assert.That(segments[1].Body, Is.EqualTo(mars));
            Assert.That(segments[1].Start, Is.EqualTo(segments[0].End));
            for (int i = 1; i < n; i++)
            {
                double t = segments[i].Start;
                (DVector3 p0, DVector3 v0) = segments[i - 1].Orbit.GetSample(t);
                (DVector3 p1, DVector3 v1) = segments[i].Orbit.GetSample(t);
                Vec3 a = V(p0), av = V(v0);
                Table.Convert(segments[i - 1].Body, segments[i].Body, t, ref a, ref av);
                Assert.That(Vec3.Length(a - V(p1)), Is.LessThan(1.0), "position at the seam of " + i);
                Assert.That(Vec3.Length(av - V(v1)), Is.LessThan(1e-3), "velocity at the seam of " + i);
            }
        }

        [Test]
        public void Forecast_EarthToSun_LongChain_LeavesThenOpenOrClosed()
        {
            int earth = Table.IndexOf("earth"), sun = Table[earth].Parent;
            (Vec3 pos, Vec3 vel) = LeavingEarth(3000.0);
            TrajectorySegment[] segments = new TrajectorySegment[TrajectoryPlot.MaxSegments];
            int n = TrajectoryForecast.Build(Table, earth, pos, vel, 0.0, segments);
            Assert.That(segments[0].EndKind, Is.EqualTo(TrajectoryEnd.LeaveBody));
            Assert.That(segments[1].Body, Is.EqualTo(sun));
            Assert.That(n, Is.GreaterThanOrEqualTo(2));
        }

        [Test]
        public void Sample_TimesGrow_EndsExact_AndAnomalyMatchesTime()
        {
            int earth = Table.IndexOf("earth");
            double r = Table[earth].Radius + 400000.0;
            double circular = Math.Sqrt(Table[earth].Nu / r);
            foreach (double factor in new[] {1.0, 1.1, 1.38, 1.5})
            {
                TrajectorySegment[] segments = new TrajectorySegment[TrajectoryPlot.MaxSegments];
                TrajectoryForecast.Build(Table, earth, new Vec3(r, 0, 0), new Vec3(0, 0, circular * factor), 50.0, segments);
                double[] times = new double[TrajectoryPlot.SamplesPerSegment], anomalies = new double[TrajectoryPlot.SamplesPerSegment];
                TrajectoryForecast.Sample(segments[0], times, anomalies);
                Assert.That(times[0], Is.EqualTo(segments[0].Start));
                Assert.That(times[times.Length - 1], Is.EqualTo(segments[0].End));
                for (int i = 1; i < times.Length; i++) Assert.That(times[i], Is.GreaterThanOrEqualTo(times[i - 1]), "time order at " + i + ", factor " + factor);
                for (int i = 0; i < times.Length; i += 13)
                {
                    Vec3 byAnomaly = V(segments[0].Orbit.GetPositionFromTrueAnomaly(anomalies[i]));
                    Vec3 byTime = V(segments[0].Orbit.GetPositionAtT(times[i]));
                    Assert.That(Vec3.Length(byAnomaly - byTime), Is.LessThan(100.0), "sample " + i + ", factor " + factor);
                }
            }
        }

        [Test]
        public void Plot_AroundTheRoot_IsTheTruePath_Continuous()
        {
            int earth = Table.IndexOf("earth"), sun = Table[earth].Parent;
            (Vec3 pos, Vec3 vel) = LeavingEarth(3000.0);
            TrajectoryPlot plot = new TrajectoryPlot();
            TrajectoryForecast.Plot(Table, earth, pos, vel, 0.0, sun, plot);
            Assert.That(plot.Count, Is.GreaterThanOrEqualTo(2));
            for (int s = 0; s < plot.Count; s++)
            {
                for (int i = 0; i < TrajectoryPlot.SamplesPerSegment; i += 31)
                {
                    Vec3 expected = TrueRelative(plot.Segments[s], sun, plot.Times[s][i]);
                    Assert.That(Vec3.Length(plot.Relative[s][i] - expected), Is.LessThan(5000.0), "segment " + s + " sample " + i);
                }
                if (s > 0)
                    Assert.That(Vec3.Length(plot.Relative[s][0] - plot.Relative[s - 1][TrajectoryPlot.SamplesPerSegment - 1]), Is.LessThan(1000.0), "seam " + s);
            }
        }

        [Test]
        public void Plot_FromTheCraftsFrame_LeavingBody_ContinuesWithoutAGap()
        {
            int phobos = Table.IndexOf("phobos");
            double r = Table[phobos].Radius + 2000.0;
            Vec3 pos = new Vec3(r, 0, 0);
            TrajectoryPlot plot = new TrajectoryPlot();
            TrajectoryForecast.Plot(Table, phobos, pos, new Vec3(30, 8, 0), 1000.0, phobos, plot);
            Assert.That(plot.Count, Is.GreaterThanOrEqualTo(2));
            Assert.That(Vec3.Length(plot.Relative[0][0] - pos), Is.LessThan(1.0), "starts at the craft");
            for (int s = 1; s < plot.Count; s++)
                Assert.That(Vec3.Length(plot.Relative[s][0] - plot.Relative[s - 1][TrajectoryPlot.SamplesPerSegment - 1]), Is.LessThan(1.0), "seam " + s);
        }

        [Test]
        public void Plot_InAnUnrelatedFrame_StandsWhereTheCraftIs()
        {
            int earth = Table.IndexOf("earth"), mars = Table.IndexOf("mars");
            double r = Table[earth].Radius + 400000.0;
            Vec3 pos = new Vec3(r, 0, 0);
            TrajectoryPlot plot = new TrajectoryPlot();
            TrajectoryForecast.Plot(Table, earth, pos, new Vec3(0, 0, Math.Sqrt(Table[earth].Nu / r)), 100.0, mars, plot);
            Table.GlobalState(earth, 100.0, out Vec3 e, out _);
            Table.GlobalState(mars, 100.0, out Vec3 m, out _);
            Assert.That(Vec3.Length(plot.Relative[0][0] - (e + pos - m)), Is.LessThan(1.0));
        }

        [Test]
        public void Plot_Events_AreOnTheLine_WithTheRightKinds()
        {
            int moon = Table.IndexOf("moon");
            double r = Table[moon].Radius + 50000.0;
            TrajectoryPlot plot = new TrajectoryPlot();
            TrajectoryForecast.Plot(Table, moon, new Vec3(r, 0, 0), new Vec3(0, 0, Math.Sqrt(Table[moon].Nu / r) * 1.1), 0.0, moon, plot);
            Assert.That(plot.Count, Is.EqualTo(1));
            Assert.That(plot.EventCount, Is.EqualTo(2), "an ellipse has one pericenter and one apocenter");
            foreach (int i in new[] {0, 1})
            {
                TrajectoryEvent e = plot.Events[i];
                Assert.That(Vec3.Length(e.Relative) - Table[moon].Radius, Is.EqualTo(e.Altitude).Within(1.0));
                Assert.That(e.Time, Is.InRange(plot.Segments[0].Start, plot.Segments[0].End));
            }
            int pe = plot.Events[0].Kind == TrajectoryEventKind.Pericenter ? 0 : 1;
            Assert.That(plot.Events[pe].Altitude, Is.LessThan(plot.Events[1 - pe].Altitude));
            Assert.That(plot.Events[pe].Altitude, Is.EqualTo(50000.0).Within(1.0), "started at the pericenter");
        }

        [Test]
        public void Plot_Entry_HasAGhostOfTheBody_AtTheSphereBorder()
        {
            int earth = Table.IndexOf("earth"), moon = Table.IndexOf("moon");
            Table.LocalState(moon, 5000.0, out Vec3 moonAt, out Vec3 moonVel);
            Vec3 away = moonAt * (1.0 + 3.0 * Table[moon].SoiRadius / Vec3.Length(moonAt));
            Vec3 pos = away, vel = Vec3.Normalize(moonAt - away) * 1500.0 + moonVel;
            TrajectoryPlot plot = new TrajectoryPlot();
            TrajectoryForecast.Plot(Table, earth, pos, vel, 5000.0, earth, plot);
            int entry = -1;
            for (int i = 0; i < plot.EventCount; i++) if (plot.Events[i].Kind == TrajectoryEventKind.EnterBody) entry = i;
            Assert.That(entry, Is.GreaterThanOrEqualTo(0));
            TrajectoryEvent e = plot.Events[entry];
            Assert.That(e.Body, Is.EqualTo(moon));
            Assert.That(e.HasBodyGhost, Is.True);
            Assert.That(Vec3.Length(e.Relative - e.BodyGhost), Is.EqualTo(Table[moon].SoiRadius).Within(10.0));
        }

        [Test]
        public void Forecaster_PublishesOnlyAFinishedRun()
        {
            int moon = Table.IndexOf("moon");
            double r = Table[moon].Radius + 50000.0;
            TrajectoryForecaster f = new TrajectoryForecaster(Table);
            Assert.That(f.Latest.Count, Is.EqualTo(0));
            Assert.That(f.Request(moon, new Vec3(r, 0, 0), new Vec3(0, 0, 1700), 0.0, moon), Is.True);
            Stopwatch watch = Stopwatch.StartNew();
            while (!f.Poll() && watch.Elapsed.TotalSeconds < 10.0) System.Threading.Thread.Sleep(1);
            Assert.That(f.LastError, Is.Null);
            Assert.That(f.Latest.Count, Is.EqualTo(1));
            f.Clear();
            Assert.That(f.Latest.Count, Is.EqualTo(0));
        }

        // ------------------------------------------------------------------ the keys, as seen on the screen

        // The engine's numbers of a simulation vector (CraftPresenter.ToVector) and the camera behind the craft: on the side of the pilot's top, turned 180 degrees about
        // local Y. In the engine a camera's right is local +X of the camera, so on the screen x = -(engine x), y = engine y, depth into the screen = -(engine z).
        private static Vec3 Screen(Vec3 body) => new Vec3(-body.X, body.Y, body.Z);

        private static Vec3 AfterHoldingKeys(KeyState keys)
        {
            // standing still in deep space, the craft's frame is the world's at the start, so the camera does not follow it in this check
            FlightConfig craft = FlightConfig.Default();
            FlightParams p = new FlightParams {Craft = craft, Surface = default, Nu = 1.0, MaxLandingSpeed = 5.0};
            FlightState s = FlightKernel.Fresh(craft);
            s.Mode = FlightMode.Flying;
            s.Position = new Vec3(1e12, 0, 0);
            s.Orientation = DQuat.Identity;
            InputLatch latch = default;
            for (int i = 0; i < 100; i++)
            {
                ControlInput c = InputMapper.Map(keys, ref latch);
                FlightKernel.Step(ref s, c, p, i * 0.02, 0.02, out _);
            }
            return s.Omega;
        }

        // A small turn by the body rate: where a point of the craft goes, in the screen coordinates of the camera that stayed behind the start attitude.
        private static Vec3 ScreenMoveOf(Vec3 point, Vec3 omega)
        {
            Vec3 moved = DQuat.Rotate(DQuat.FromRotationVector(Vec3.Normalize(omega) * 0.1), point);
            return Screen(moved) - Screen(point);
        }

        [Test]
        public void Keys_OnTheScreen_ArePilotsKeys_LikeAnAirplane()
        {
            Vec3 nose = Vec3.UnitY, top = new Vec3(0, 0, -1);
            Vec3 w = ScreenMoveOf(nose, AfterHoldingKeys(new KeyState {W = true}));
            Assert.That(w.Z, Is.GreaterThan(0.05), "W: the nose goes away from the camera, which sits at the pilot's top: nose down");
            Vec3 e = ScreenMoveOf(nose, AfterHoldingKeys(new KeyState {E = true}));
            Assert.That(e.X, Is.GreaterThan(0.05), "E: the nose goes right on the screen");
            Vec3 d = ScreenMoveOf(top, AfterHoldingKeys(new KeyState {D = true}));
            Assert.That(d.X, Is.GreaterThan(0.05), "D: the pilot's top goes right on the screen: roll right");
        }

        // ------------------------------------------------------------------ the lead to the Moon

        // A prograde burn from a 400 km circular orbit in the Moon's own plane; for each phase (the Moon ahead of the craft along the motion, degrees) does the forecast enter the Moon's sphere?
        private static void EnteringPhases(double time, double extraSpeed, out double from, out double to, out double flight)
        {
            int earth = Table.IndexOf("earth"), moon = Table.IndexOf("moon");
            StaticOrbit moonOrbit = Table[moon].Orbit;
            Table.LocalState(moon, time, out Vec3 moonAt, out Vec3 moonVel);
            Vec3 normal = Vec3.Normalize(Vec3.Cross(moonAt, moonVel));
            Vec3 toMoon = Vec3.Normalize(moonAt);
            Vec3 aside = Vec3.Cross(normal, toMoon);
            double r = Table[earth].Radius + 400000.0;
            double a = 0.5 * (r + Vec3.Length(moonAt));
            double speed = Math.Sqrt(Table[earth].Nu * (2.0 / r - 1.0 / a)) + extraSpeed;
            from = double.NaN;
            to = double.NaN;
            flight = double.NaN;
            double best = double.PositiveInfinity;
            for (int degrees = 0; degrees < 360; degrees++)
            {
                double behind = degrees * Math.PI / 180.0;
                Vec3 radial = toMoon * Math.Cos(behind) - aside * Math.Sin(behind);
                Vec3 along = Vec3.Cross(normal, radial);
                bool enters = SoiForecast.NextEvent(Table, earth, radial * r, along * speed, time, out _, out double when, out int to2) == TrajectoryEnd.EnterBody && to2 == moon;
                if (!enters) continue;
                if (double.IsNaN(from)) from = degrees;
                to = degrees;
                if (when - time < best) best = when - time;
            }
            flight = best;
        }

        [Test]
        public void MoonLead_TheTable()
        {
            int earth = Table.IndexOf("earth");
            Table.LocalState(Table.IndexOf("moon"), 0.0, out Vec3 mp, out Vec3 mv);
            double cosPlanes = Vec3.Dot(Vec3.Normalize(Vec3.Cross(mp, mv)), Vec3.Normalize(Table[earth].Surface.Axis));
            TestContext.WriteLine("extra plane: Moon's orbit to Earth's equator " + (Math.Acos(cosPlanes) * 180.0 / Math.PI).ToString("F1") + " deg; Moon's period " + (Table[Table.IndexOf("moon")].Orbit.Period / 86400.0).ToString("F2") + " d");
            foreach (double extra in new[] {0.0, 100.0, 500.0, 2000.0})
            {
                foreach (double time in new[] {0.0, 600000.0, 1200000.0, 1800000.0})
                {
                    EnteringPhases(time, extra, out double from, out double to, out double flight);
                    double dist = 0;
                    Table.LocalState(Table.IndexOf("moon"), time, out Vec3 m, out _);
                    dist = Vec3.Length(m);
                    TestContext.WriteLine("extra " + extra + " m/s, t " + time + ", Moon at " + (dist / 1e6).ToString("F0") + " Mm: enters with the Moon ahead by " + from + ".." + to + " deg, shortest flight " + (flight / 3600.0).ToString("F1") + " h");
                    Assert.That(from, Is.Not.NaN, "some phase reaches the Moon");
                }
            }
        }

        // ------------------------------------------------------------------ the descent instrument

        private static FlightParams Params(string bodyId, string craft = null)
        {
            FlightParams p = new FlightParams {Craft = Config.ResolveFlight(craft), MaxLandingSpeed = Config.MaxLandingSpeed};
            Assert.That(Table.ApplyTo(bodyId, ref p), Is.True);
            return p;
        }

        // the nose along a unit direction: the shortest turn from body +Y
        private static DQuat NoseAlong(Vec3 direction)
        {
            Vec3 axis = Vec3.Cross(Vec3.UnitY, direction);
            double sin = Vec3.Length(axis), cos = Vec3.Dot(Vec3.UnitY, direction);
            return sin < 1e-9 ? (cos > 0 ? DQuat.Identity : DQuat.FromAxisAngle(Vec3.UnitX, Math.PI)) : DQuat.FromAxisAngle(axis, Math.Atan2(sin, cos));
        }

        private static FlightState FallingFrom(in FlightParams p, double height, double down, double across)
        {
            FlightState s = SpawnBuilder.OnSurface(p.Craft, p.Surface, 0.0, 0.0, height, 0.0);
            Vec3 up = Vec3.Normalize(s.Position);
            Vec3 side = Vec3.Normalize(Vec3.Cross(up, Vec3.Normalize(new Vec3(0.3, 0.9, 0.2))));
            s.Mode = FlightMode.Flying;
            s.Velocity = Surface.GroundVelocity(p.Surface, s.Position) + up * -down + side * across;
            return s;
        }

        // Flies the descent through the kernel by the instrument: coast until the instrument says burn, full throttle against the ground velocity, cut when the speed is gone.
        private static double FlyByTheInstrument(ref FlightState s, in FlightParams p, double firstAdvice, out double lowest)
        {
            const double Dt = 0.02;
            double time = 0.0;
            bool burning = false;
            lowest = double.PositiveInfinity;
            for (int i = 0; i < 2000000; i++)
            {
                Vec3 relative = s.Velocity - Surface.GroundVelocity(p.Surface, s.Position);
                double speed = Vec3.Length(relative);
                if (!burning && i % 10 == 0)
                {
                    LandingAdvice a = LandingAdvisor.Compute(s, p, double.PositiveInfinity);
                    if (a.Falling && a.BurnIn <= Dt * 10) burning = true;
                }
                if (speed > 0.01) s.Orientation = NoseAlong(relative * (-1.0 / speed));
                s.Omega = Vec3.Zero;
                double accel = p.Craft.Engine.MaxThrust / (p.Craft.DryMass + s.MainFuel + s.RcsFuel);
                bool stop = burning && speed < accel * Dt * 2.0;
                if (stop) return speed;
                ControlInput c = burning ? new ControlInput {ThrottleFull = true} : new ControlInput {ThrottleZero = true};
                FlightKernel.Step(ref s, c, p, time, Dt, out _);
                time += Dt;
                lowest = Math.Min(lowest, Surface.Altitude(p.Surface, s.Position));
                if (s.Mode != FlightMode.Flying) return speed;
            }
            Assert.Fail("the descent never ended");
            return double.NaN;
        }

        [TestCase("moon", 20000.0, 100.0, 300.0)]
        [TestCase("moon", 5000.0, 50.0, 0.0)]
        [TestCase("mars", 200000.0, 300.0, 1000.0)]
        [TestCase("earth", 120000.0, 300.0, 500.0)]
        public void Instrument_FlownByTheKernel_StopsJustAboveTheGround(string body, double height, double down, double across)
        {
            FlightParams p = Params(body);
            FlightState s = FallingFrom(p, height, down, across);
            LandingAdvice first = LandingAdvisor.Compute(s, p, double.PositiveInfinity);
            Assert.That(first.Falling, Is.True);
            Assert.That(first.CanStop, Is.True);
            Assert.That(first.BurnIn, Is.GreaterThan(0.0));
            Assert.That(first.BurnIn, Is.LessThan(first.TimeToImpact));
            FlyByTheInstrument(ref s, p, first.BurnIn, out double lowest);
            Assert.That(s.Mode, Is.EqualTo(FlightMode.Flying), "stopped in the air by plan");
            Assert.That(lowest, Is.GreaterThan(0.0).And.LessThan(LandingAdvisor.TargetHeight * 12.0), "a late burn, not a timid one: " + lowest);
        }

        [Test]
        public void Instrument_CoastTime_MatchesTheForecastImpact()
        {
            FlightParams p = Params("moon");
            FlightState s = FallingFrom(p, 30000.0, 40.0, 500.0);
            LandingAdvice a = LandingAdvisor.Compute(s, p, double.PositiveInfinity);
            SoiForecast.NextEvent(Table, Table.IndexOf("moon"), s.Position, s.Velocity, 0.0, out _, out double when, out _);
            Assert.That(a.TimeToImpact, Is.EqualTo(when).Within(Math.Max(0.5, when * 0.002)));
        }

        [Test]
        public void Instrument_TooLate_SaysSo()
        {
            FlightParams p = Params("earth");
            FlightState s = FallingFrom(p, 200.0, 2000.0, 0.0);
            LandingAdvice a = LandingAdvisor.Compute(s, p, double.PositiveInfinity);
            Assert.That(a.Falling, Is.True);
            Assert.That(a.CanStop, Is.False);
            Assert.That(a.BurnIn, Is.EqualTo(0.0));
            Assert.That(a.SpeedAtGroundNow, Is.GreaterThan(100.0));
        }

        [Test]
        public void Instrument_NoFuel_SaysSo()
        {
            FlightParams p = Params("moon");
            FlightState s = FallingFrom(p, 20000.0, 500.0, 500.0);
            s.MainFuel = 1.0;
            LandingAdvice a = LandingAdvisor.Compute(s, p, double.PositiveInfinity);
            Assert.That(a.Falling, Is.True);
            Assert.That(a.OutOfFuel, Is.True);
            Assert.That(a.CanStop, Is.False);
        }

        [Test]
        public void Instrument_Quiet_OnOrbit_AndOnTheGround_AndLeavingTheSphere()
        {
            FlightParams p = Params("moon");
            FlightState orbit = SpawnBuilder.CircularOrbit(p.Craft, p.Surface, p.Nu, 50000.0, 0.0, 0.0);
            Assert.That(LandingAdvisor.Compute(orbit, p, double.PositiveInfinity).Falling, Is.False);
            FlightState ground = SpawnBuilder.OnSurface(p.Craft, p.Surface, 0.0, 0.0, 0.0, 0.0);
            Assert.That(LandingAdvisor.Compute(ground, p, double.PositiveInfinity).Falling, Is.False);
            int moon = Table.IndexOf("moon");
            FlightState up = FallingFrom(p, 1000.0, -2370.0, 0.0);
            Assert.That(LandingAdvisor.Compute(up, p, Table[moon].SoiRadius).Falling, Is.False, "the arc goes beyond the sphere, the Earth takes over");
        }

        [Test]
        public void Instrument_RetrogradeError_ReadsTheNose()
        {
            FlightParams p = Params("moon");
            FlightState s = FallingFrom(p, 20000.0, 200.0, 0.0);
            Vec3 relative = s.Velocity - Surface.GroundVelocity(p.Surface, s.Position);
            s.Orientation = NoseAlong(Vec3.Normalize(relative) * -1.0);
            Assert.That(LandingAdvisor.Compute(s, p, double.PositiveInfinity).RetrogradeError, Is.EqualTo(0.0).Within(0.01));
            s.Orientation = NoseAlong(Vec3.Normalize(relative));
            Assert.That(LandingAdvisor.Compute(s, p, double.PositiveInfinity).RetrogradeError, Is.EqualTo(180.0).Within(0.01));
        }

        [Test]
        public void Instrument_IsCheap()
        {
            FlightParams p = Params("earth");
            FlightState s = FallingFrom(p, 100000.0, 500.0, 5000.0);
            Stopwatch watch = Stopwatch.StartNew();
            for (int i = 0; i < 20; i++) LandingAdvisor.Compute(s, p, double.PositiveInfinity);
            TestContext.WriteLine("one advice: " + watch.Elapsed.TotalMilliseconds / 20.0 + " ms");
            Assert.That(watch.Elapsed.TotalMilliseconds / 20.0, Is.LessThan(20.0));
        }
    }
}
