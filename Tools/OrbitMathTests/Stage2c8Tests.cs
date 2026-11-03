using System;
using Newtonsoft.Json;
using NUnit.Framework;
using Orbital.Core.Flight;
using Orbital.Core.Net;

namespace Orbital.Tests
{
    /// <summary>Stage 2c.8: crafts on the ground (landed, crashed) are points of the rotating surface; the state frame is explicit.</summary>
    public class Stage2c8Tests
    {
        private static readonly FlightParams P = FlightFixture.Params();

        [Test]
        public void SurfaceRotation_MovesAGroundPointAsTheGroundDoes()
        {
            Vec3 a = Surface.Point(P.Surface, 0.3, -1.1, 100), b = Surface.Point(P.Surface, 0.3, -1.1, 5100);
            Vec3 turned = DQuat.Rotate(Surface.Rotation(P.Surface, 100, 5100), a);
            Assert.Less(Vec3.Length(turned - b), 1e-6);
            Vec3 back = DQuat.Rotate(Surface.Rotation(P.Surface, 5100, 100), b);
            Assert.Less(Vec3.Length(back - a), 1e-6);
        }

        [Test]
        public void Crashed_StaysAtTheImpactPoint_AndTurnsWithTheGround()
        {
            double t = 1000;
            FlightState s = FlightKernel.Fresh(P.Craft);
            s.Mode = FlightMode.Flying;
            s.Position = Surface.Point(P.Surface, 0.0, 0.0, t) * (1 + 1.0 / FlightFixture.EarthRadius);
            s.Velocity = Vec3.Zero; // at rest in space: the ground (465 m/s at the equator) hits it
            for (int i = 0; i < 20 && s.Mode == FlightMode.Flying; i++, t += 0.1) FlightKernel.Step(ref s, default, P, t, 0.1, out _);
            Assert.AreEqual(FlightMode.Crashed, s.Mode);
            FlightKernel.Step(ref s, default, P, t, 0.1, out _);
            t += 0.1;
            double lat = s.Latitude, lon = s.Longitude;
            Vec3 upInBody = DQuat.RotateInverse(s.Orientation, Surface.Normal(P.Surface, s.Position));
            for (int i = 0; i < 6000; i++, t += 0.1)
            {
                ControlInput full = default;
                full.ThrottleFull = true; // a wreck has no engine
                FlightKernel.Step(ref s, full, P, t, 0.1, out _);
            }
            Assert.AreEqual(FlightMode.Crashed, s.Mode);
            Assert.AreEqual(lat, s.Latitude);
            Assert.AreEqual(lon, s.Longitude);
            Assert.Less(Vec3.Length(s.Position - Surface.Point(P.Surface, lat, lon, t)), 1e-6, "on its ground point after 10 min");
            Assert.Less(Surface.SpeedRelativeToGround(P.Surface, s.Position, s.Velocity), 1e-9, "moves with the ground");
            Assert.AreEqual(0.0, s.Throttle);
            Vec3 upNow = DQuat.RotateInverse(s.Orientation, Surface.Normal(P.Surface, s.Position));
            Assert.Less(Vec3.Length(upNow - upInBody), 1e-9, "the attitude turns with the ground");
        }

        private static CraftStateDto Landed(double time, double lat, double lon)
        {
            FlightState f = FlightKernel.Fresh(P.Craft);
            f.Mode = FlightMode.Landed;
            f.Latitude = lat;
            f.Longitude = lon;
            f.Position = Surface.Point(P.Surface, lat, lon, time);
            f.Velocity = Surface.GroundVelocity(P.Surface, f.Position);
            f.Orientation = DQuat.FromTo(Vec3.UnitY, Surface.Normal(P.Surface, f.Position));
            return StateMapper.FromFlightState(f, time, "other");
        }

        [Test]
        public void LandedGhost_StaysOnTheGround_WhenStatesAreAheadOrBehindTheClock()
        {
            GhostConfig cfg = GhostConfig.Default();
            Ghost g = new Ghost("other");
            const double lat = 0.0, lon = 0.5; // the equator: the ground moves at 465 m/s
            double worst = 0, worstTilt = 0;
            int k = 0;
            for (double now = 0; now < 120; now += 0.02)
            {
                if (k++ % 50 == 0) // a state each second, its time off the client clock by up to 0.3 s either way
                    g.OnServerState(Landed(now + 0.3 * Math.Sin(now), lat, lon), P, now, cfg);
                else g.AdvanceTo(now, P, cfg);
                Assert.IsTrue(g.OnSurface);
                Vec3 ground = Surface.Point(P.Surface, lat, lon, now);
                worst = Math.Max(worst, Vec3.Length(g.ShownPosition - ground));
                Vec3 up = DQuat.Rotate(g.State.Orientation, Vec3.UnitY);
                worstTilt = Math.Max(worstTilt, Vec3.Length(up - Surface.Normal(P.Surface, ground)));
            }
            Assert.Less(worst, 1e-3, "no back-and-forth along the rotation");
            Assert.Less(worstTilt, 1e-9, "upright on its ground point");
        }

        [Test]
        public void Frame_OnTheWire_AndTheFallbackForAnOlderServer()
        {
            CraftStateDto landed = Landed(10, 0.1, 0.2);
            Assert.AreEqual(CraftFrameDto.Surface, landed.Frame);
            string json = JsonConvert.SerializeObject(landed, ProtocolCodec.Settings);
            StringAssert.Contains("\"frame\":\"surface\"", json);
            CraftStateDto back = JsonConvert.DeserializeObject<CraftStateDto>("{\"mode\":\"flying\",\"frame\":\"surface\"}", ProtocolCodec.Settings);
            Assert.AreEqual(CraftFrameDto.Surface, back.Frame);
            Assert.IsTrue(StateMapper.IsSurface(back), "the server's word wins over the mode");
            CraftStateDto old = JsonConvert.DeserializeObject<CraftStateDto>("{\"mode\":\"crashed\"}", ProtocolCodec.Settings);
            Assert.IsNull(old.Frame);
            Assert.IsTrue(StateMapper.IsSurface(old));
            Assert.IsFalse(StateMapper.IsSurface(new CraftStateDto { Mode = CraftModeDto.Flying }));
        }

        [Test]
        public void FlyingGhost_NeverCrashesByPrediction_OnlyByTheServer()
        {
            GhostConfig cfg = GhostConfig.Default();
            Ghost g = new Ghost("other");
            FlightState f = FlightKernel.Fresh(P.Craft);
            f.Mode = FlightMode.Flying;
            f.Position = Surface.Point(P.Surface, 0.0, 0.0, 0) * (1 + 50.0 / FlightFixture.EarthRadius);
            f.Velocity = Surface.GroundVelocity(P.Surface, f.Position) - Vec3.Normalize(f.Position) * 30.0; // 30 m/s down: a hard impact
            g.OnServerState(StateMapper.FromFlightState(f, 0, "other"), P, 0, cfg);
            for (double now = 0.02; now < 20; now += 0.02) g.AdvanceTo(now, P, cfg);
            Assert.AreEqual(FlightMode.Flying, g.ServerMode, "shown as the server said");
            Assert.AreNotEqual(FlightMode.Crashed, g.State.Mode, "no crash by prediction");
            Assert.Less(Math.Abs(Surface.Altitude(P.Surface, g.State.Position)), 1e-3, "waits on its impact point");
            Assert.Less(Surface.SpeedRelativeToGround(P.Surface, g.State.Position, g.State.Velocity), 1e-6);
            CraftStateDto crashed = StateMapper.FromFlightState(g.State, 20, "other");
            crashed.Mode = CraftModeDto.Crashed;
            g.OnServerState(crashed, P, 20, cfg);
            Assert.AreEqual(FlightMode.Crashed, g.ServerMode, "the server's word");
            g.AdvanceTo(30, P, cfg);
            Assert.AreEqual(FlightMode.Crashed, g.State.Mode);
        }

        [Test]
        public void CrashConfirmed_OnlyWhenBothSidesHaveItCrashed()
        {
            CraftStateDto server = new CraftStateDto { CraftId = "c1", Mode = CraftModeDto.Crashed, CrashedAt = 100, DestroyAt = 165 };
            Assert.IsFalse(Reconciler.CrashConfirmed(FlightMode.Flying, server, "c1"), "the client flies: a guess in the retro window");
            Assert.IsFalse(Reconciler.CrashConfirmed(FlightMode.Landed, server, "c1"));
            Assert.IsTrue(Reconciler.CrashConfirmed(FlightMode.Crashed, server, "c1"));
            Assert.IsFalse(Reconciler.CrashConfirmed(FlightMode.Crashed, server, "c2"), "another craft's state");
            Assert.IsFalse(Reconciler.CrashConfirmed(FlightMode.Crashed, null, "c1"));
            server.CrashedAt = null;
            Assert.IsFalse(Reconciler.CrashConfirmed(FlightMode.Crashed, server, "c1"));
            server.CrashedAt = 100;
            server.Mode = CraftModeDto.Flying;
            Assert.IsFalse(Reconciler.CrashConfirmed(FlightMode.Crashed, server, "c1"), "the server has it flying: awaiting");
        }

        [Test]
        public void Recorder_EndThrottle_TellsWhetherTheBurnGoesOn()
        {
            RecorderConfig cfg = RecorderConfig.Online(); // segments of at most 5 s
            RecorderState r = default;
            int i = 0;
            for (; i <= 400; i++) ThrustSegmentRecorder.Update(ref r, new RecorderSample { Time = i * 0.02, Throttle = 0.7, ThrustDirection = Vec3.UnitY }, cfg);
            Assert.IsTrue(ThrustSegmentRecorder.TryDequeue(ref r, 100, cfg, out ThrustSegment split));
            Assert.AreEqual(0.7, split.EndThrottle, 1e-12, "split by length: the burn goes on");
            for (; i <= 450; i++) ThrustSegmentRecorder.Update(ref r, new RecorderSample { Time = i * 0.02, Throttle = 0.7, ThrustDirection = Vec3.UnitY }, cfg);
            ThrustSegmentRecorder.Update(ref r, new RecorderSample { Time = i * 0.02, Throttle = 0.0, ThrustDirection = Vec3.UnitY }, cfg);
            Assert.IsTrue(ThrustSegmentRecorder.TryDequeue(ref r, 200, cfg, out ThrustSegment cut));
            Assert.AreEqual(9.0, cut.EndTime, 1e-9);
            Assert.AreEqual(0.0, cut.EndThrottle, "the engine stopped");
            Assert.AreEqual(0.0, StateMapper.ToManeuver(cut, 1, "c", 0).EndThrottle);
            Assert.AreEqual(0.7, StateMapper.ToManeuver(split, 1, "c", 0).EndThrottle.Value, 1e-12);
        }

        private static FlightState Climbing()
        {
            FlightState s = FlightKernel.Fresh(P.Craft);
            s.Mode = FlightMode.Flying;
            s.Position = Surface.Point(P.Surface, 0.2, 0.3, 0) * (1 + 2000.0 / FlightFixture.EarthRadius);
            s.Velocity = Surface.GroundVelocity(P.Surface, s.Position) + Vec3.Normalize(s.Position) * 50.0;
            s.Orientation = DQuat.FromTo(Vec3.UnitY, Vec3.Normalize(s.Position));
            s.SasOn = 1;
            return s;
        }

        [Test]
        public void Ghost_CarriesTheBurnOn_FromTheThrustTail()
        {
            GhostConfig cfg = GhostConfig.Default();
            // the other craft burns at 0.8 from t = 0; its last accepted segment ended at 3 s, the server's state at 8 s is ballistic after it
            FlightState truth = Climbing();
            truth.Throttle = 0.8;
            Propagation.Advance(ref truth, P, 0, 3, 0.02);
            FlightState atTail = truth;
            FlightState ballistic = truth;
            ballistic.Throttle = 0;
            Propagation.Advance(ref ballistic, P, 3, 8, 0.02);
            Propagation.Advance(ref truth, P, 3, 8, 0.02);
            CraftStateDto server = StateMapper.FromFlightState(ballistic, 8, "other");
            server.Detailed = true;
            server.Thrust = new ThrustTailDto
            {
                Time = 3, Throttle = 0.8, Direction = StateMapper.Arr(DQuat.Rotate(atTail.Orientation, Vec3.UnitY)),
                Position = StateMapper.Arr(atTail.Position), Velocity = StateMapper.Arr(atTail.Velocity), Fuel = atTail.MainFuel,
            };
            double lag = Vec3.Length(ballistic.Position - truth.Position);
            Assert.Greater(lag, 50, "without the tail the ghost lags behind");
            Ghost g = new Ghost("other");
            g.OnServerState(server, P, 8, cfg);
            double err = Vec3.Length(g.State.Position - truth.Position);
            TestContext.Progress.WriteLine($"thrust tail: ballistic lag {lag:F1} m, ghost error {err:F2} m");
            // the rest is the attitude: the ghost starts the tail without the body rate the pilot's craft had
            Assert.Less(err, Math.Min(10.0, 0.05 * lag), "with the tail it is close to where the craft is");
            Assert.AreEqual(13.0, g.ThrustUntil, 1e-9);
            // the slow channel right after says less (no tail): skipped
            CraftStateDto slow = StateMapper.FromFlightState(ballistic, 8.5, "other");
            g.OnServerState(slow, P, 8.5, cfg);
            Assert.AreEqual(13.0, g.ThrustUntil, 1e-9, "the tail is kept");
            // past the horizon the engine is off
            g.AdvanceTo(20, P, cfg);
            Assert.IsTrue(double.IsNaN(g.ThrustUntil));
            Assert.AreEqual(0.0, g.State.Throttle);
            // a near-group state without a tail (the burn stopped) ends it at once
            Ghost h = new Ghost("other");
            h.OnServerState(server, P, 8, cfg);
            CraftStateDto stopped = StateMapper.FromFlightState(ballistic, 9, "other");
            stopped.Detailed = true;
            h.OnServerState(stopped, P, 9, cfg);
            Assert.IsTrue(double.IsNaN(h.ThrustUntil));
            Assert.AreEqual(0.0, h.State.Throttle);
        }
    }
}
