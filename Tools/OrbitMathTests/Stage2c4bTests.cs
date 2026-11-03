using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Orbital.Core.Flight;
using Orbital.Core.Net;

namespace Orbital.Tests
{
    /// <summary>Stage 2c.4b: clock sync and discipline.</summary>
    public class Stage2c4bClockTests
    {
        /// <summary>Server game time = g0 + scale * real; legs of 30 ms plus a uniform jitter of 0..legJitter each (RTT jitter = twice that).</summary>
        private static double SyncError(int seed, int pings, double legJitter = 0.01)
        {
            Random rnd = new Random(seed);
            const double g0 = 5000.0;
            ClockSync sync = new ClockSync();
            double local = 100.0;
            for (int i = 0; i < pings; i++)
            {
                double up = 0.03 + legJitter * rnd.NextDouble(), down = 0.03 + legJitter * rnd.NextDouble();
                sync.AddSample(local, local + up + down, g0 + (local + up), 1.0);
                local += 10.0;
            }
            return sync.ServerTime(local) - (g0 + local);
        }

        private static (double median, double p95, double max) Stats(int pings, double legJitter)
        {
            List<double> e = Enumerable.Range(0, 1000).Select(s => Math.Abs(SyncError(s, pings, legJitter))).OrderBy(x => x).ToList();
            return (e[e.Count / 2], e[(int) (0.95 * e.Count)], e[e.Count - 1]);
        }

        [Test]
        public void Jitter20ms_ErrorBelow2ms()
        {
            // RTT jitter 20 ms (each leg 0..10 ms), 8 pings: the plan's criterion
            var rtt20 = Stats(ClockSync.Window, 0.01);
            // harsher: each leg 0..20 ms (RTT jitter 40 ms)
            var leg20 = Stats(ClockSync.Window, 0.02);
            TestContext.Progress.WriteLine($"ClockSync, 8 pings, 1000 runs: RTT jitter 20 ms -> median {rtt20.median * 1000:F2} ms, 95% {rtt20.p95 * 1000:F2} ms, max {rtt20.max * 1000:F2} ms; " +
                                           $"each leg 0..20 ms -> median {leg20.median * 1000:F2} ms, 95% {leg20.p95 * 1000:F2} ms, max {leg20.max * 1000:F2} ms");
            Assert.Less(rtt20.p95, 0.002);
            Assert.Less(rtt20.max, 0.005, "the intersection always contains the truth: never worse than half the narrowest interval");
            Assert.Less(leg20.p95, 0.004);
        }

        [Test]
        public void ScaleChange_CarriesTheSamplesOver()
        {
            ClockSync sync = new ClockSync();
            // scale 1 until server time 1000 (local real 500), then 10
            double Server(double l) => l <= 500 ? 500 + l : 1000 + 10 * (l - 500);
            for (double l = 420; l < 500; l += 10) sync.AddSample(l, l + 0.06, Server(l + 0.03), 1.0);
            sync.OnScaleChange(1000, 10);
            Assert.AreEqual(10.0, sync.Scale);
            Assert.AreEqual(Server(520), sync.ServerTime(520), 0.01 * 10, "game error = real error * scale");
            int resets = sync.Resets;
            sync.AddSample(530, 530.06, Server(530.03), 10);
            Assert.AreEqual(resets, sync.Resets, "the carried samples agree with the new line");
        }

        [Test]
        public void LostPush_PongWithAnotherScale_ReAnchors()
        {
            ClockSync sync = new ClockSync();
            double Server(double l) => l <= 500 ? 500 + l : 1000 + 2 * (l - 500);
            for (double l = 420; l < 500; l += 10) sync.AddSample(l, l + 0.06, Server(l + 0.03), 1.0);
            sync.AddSample(510, 510.06, Server(510.03), 2.0);
            Assert.AreEqual(2.0, sync.Scale);
            Assert.AreEqual(Server(530), sync.ServerTime(530), 0.2, "re-anchored at the pong (the exact change time is unknown)");
        }

        [Test]
        public void TimeBaseJump_ResetsToTheNewest()
        {
            ClockSync sync = new ClockSync();
            for (double l = 0; l < 80; l += 10) sync.AddSample(l, l + 0.05, 1000 + l + 0.025, 1.0);
            sync.AddSample(90, 90.05, 500 + 90.025, 1.0); // the server restarted with the time frozen earlier
            Assert.AreEqual(1, sync.Resets);
            Assert.AreEqual(1, sync.Count);
            Assert.AreEqual(500 + 100, sync.ServerTime(100), 0.05);
        }

        private static GameClock Clock(double start) => GameClock.Create(start, 0.02, 1.0, 200, 0.05, 1000);

        [Test]
        public void Discipline_SlewsASmallError_WithinTheRateLimit()
        {
            GameClock c = Clock(-0.2); // 0.2 s behind a server at time = real
            DisciplineConfig cfg = DisciplineConfig.Default();
            double real = 0;
            double maxCorr = 0;
            for (int tick = 0; tick < 50 * 90; tick++)
            {
                real += 0.02;
                GameClock.Tick(ref c);
                DisciplineResult r = ClockDiscipline.Apply(ref c, real, 1.0, cfg);
                Assert.IsFalse(r.Jumped);
                maxCorr = Math.Max(maxCorr, Math.Abs(r.Correction));
            }
            double error = real - ClockDiscipline.ContinuousTime(c);
            Assert.Less(Math.Abs(error), 0.005, $"error after 90 s {error * 1000:F2} ms");
            Assert.LessOrEqual(maxCorr, 0.05 + 1e-12);
            Assert.AreEqual(0.02, c.Step, "the step length never changes");
        }

        [Test]
        public void Discipline_JumpsOverTheThreshold_ScaledByTheScale()
        {
            GameClock c = Clock(0);
            DisciplineResult r = ClockDiscipline.Apply(ref c, 2.0, 1.0, DisciplineConfig.Default());
            Assert.IsTrue(r.Jumped);
            Assert.AreEqual(100, r.JumpSteps);
            Assert.AreEqual(2.0, c.Time, 1e-12);

            GameClock d = Clock(0);
            GameClock.SetScale(ref d, 10);
            DisciplineResult s = ClockDiscipline.Apply(ref d, 3.0, 10.0, DisciplineConfig.Default());
            Assert.IsFalse(s.Jumped, "at scale 10 the jump threshold is 5 game seconds");
            Assert.AreEqual(0.03, s.Correction, 1e-12, "0.3 real seconds behind: gain 0.1 per second gives 3 %");
            Assert.AreEqual(10.3, d.Scale, 1e-9);
            DisciplineResult t = ClockDiscipline.Apply(ref d, 4.9, 10.0, DisciplineConfig.Default());
            Assert.AreEqual(0.049, t.Correction, 1e-12);
            GameClock e = Clock(0);
            Assert.AreEqual(-0.045, ClockDiscipline.Apply(ref e, -0.45, 1.0, DisciplineConfig.Default()).Correction, 1e-12, "ahead: slows down");
        }
    }

    /// <summary>Stage 2c.4b: history, reconciliation, ghosts.</summary>
    public class Stage2c4bReconcileTests
    {
        private const double Nu = FlightFixture.EarthNu;
        private const double R = 7.0e6;
        private static readonly double W = Math.Sqrt(Nu / (R * R * R));

        private static Vec3 Pos(double t) => new Vec3(R * Math.Cos(W * t), 0, R * Math.Sin(W * t));
        private static Vec3 Vel(double t) => new Vec3(-R * W * Math.Sin(W * t), 0, R * W * Math.Cos(W * t));

        private static FlightState Orbit(double t)
        {
            FlightState s = FlightKernel.Fresh(FlightConfig.Default());
            s.Mode = FlightMode.Flying;
            s.Position = Pos(t);
            s.Velocity = Vel(t);
            return s;
        }

        private static StateHistory Filled(double from, double to, double dt = 0.02, int capacity = 4096)
        {
            StateHistory h = new StateHistory(capacity);
            for (int i = 0; from + i * dt <= to + 1e-9; i++) h.Push(from + i * dt, Orbit(from + i * dt));
            return h;
        }

        [Test]
        public void History_Hermite_MatchesTheOrbitBetweenSteps()
        {
            StateHistory h = Filled(0, 60);
            double worstP = 0, worstV = 0;
            for (double t = 0.013; t < 60; t += 0.731)
            {
                Assert.IsTrue(h.TrySample(t, out HistorySample s));
                worstP = Math.Max(worstP, Vec3.Length(s.Position - Pos(t)));
                worstV = Math.Max(worstV, Vec3.Length(s.Velocity - Vel(t)));
            }
            Assert.Less(worstP, 1e-6);
            Assert.Less(worstV, 1e-4);
            Assert.IsFalse(h.TrySample(-1, out _));
            Assert.IsFalse(h.TrySample(61, out _));
            Assert.IsTrue(h.TrySample(60.3, out HistorySample ahead, 0.5));
            Assert.Less(Vec3.Length(ahead.Position - Pos(60.3)), 1.0, "a short linear extrapolation");
        }

        [Test]
        public void History_RingKeepsTheNewest()
        {
            StateHistory h = Filled(0, 100, 0.02, 1000);
            Assert.AreEqual(1000, h.Count);
            Assert.AreEqual(100.0, h.NewestTime, 1e-9);
            Assert.AreEqual(100.0 - 999 * 0.02, h.OldestTime, 1e-9);
            h.Push(50, Orbit(50)); // going back drops the newer entries
            Assert.AreEqual(50.0, h.NewestTime);
        }

        private static CraftStateDto Server(double t, Vec3 dp = default, Vec3 dv = default)
        {
            FlightState s = Orbit(t);
            s.Position = s.Position + dp;
            s.Velocity = s.Velocity + dv;
            CraftStateDto dto = StateMapper.FromFlightState(s, t, "c");
            dto.ParentId = "Earth";
            return dto;
        }

        private static readonly ReconcileConfig Cfg = ReconcileConfig.Default();

        [Test]
        public void Reconcile_Thresholds()
        {
            StateHistory h = Filled(0, 60);
            Assert.AreEqual(ReconcileAction.Ignore, Reconciler.Compare(Server(30, new Vec3(0.5, 0, 0)), h, "Earth", double.PositiveInfinity, Cfg).Action);
            Assert.AreEqual(ReconcileAction.Soft, Reconciler.Compare(Server(30, new Vec3(50, 0, 0)), h, "Earth", double.PositiveInfinity, Cfg).Action);
            Assert.AreEqual(ReconcileAction.Soft, Reconciler.Compare(Server(30, default, new Vec3(0, 0.2, 0)), h, "Earth", double.PositiveInfinity, Cfg).Action);
            ReconcileResult big = Reconciler.Compare(Server(30, new Vec3(3000, 0, 0)), h, "Earth", double.PositiveInfinity, Cfg);
            Assert.AreEqual(ReconcileAction.Hard, big.Action);
            Assert.AreEqual("size", big.Reason);
            Assert.AreEqual("parent", Reconciler.Compare(Server(30), h, "Moon", double.PositiveInfinity, Cfg).Reason);
            CraftStateDto landed = Server(30);
            landed.Mode = CraftModeDto.Landed;
            Assert.AreEqual("mode", Reconciler.Compare(landed, h, "Earth", double.PositiveInfinity, Cfg).Reason);
            CraftStateDto crashed = Server(30);
            crashed.Mode = CraftModeDto.Crashed;
            Assert.AreEqual("crashed", Reconciler.Compare(crashed, h, "Earth", double.PositiveInfinity, Cfg).Reason, "the server verdict wins");
            Assert.AreEqual(ReconcileAction.TooOld, Reconciler.Compare(Server(-5), h, "Earth", double.PositiveInfinity, Cfg).Action);
            Assert.AreEqual("ahead", Reconciler.Compare(Server(65), h, "Earth", double.PositiveInfinity, Cfg).Reason);
        }

        [Test]
        public void Reconcile_UnconfirmedThrustBeforeTheState_MakesItStale()
        {
            StateHistory h = Filled(0, 60);
            CraftStateDto s = Server(30, new Vec3(5000, 0, 0));
            Assert.AreEqual(ReconcileAction.Stale, Reconciler.Compare(s, h, "Earth", 29.0, Cfg).Action);
            Assert.AreEqual(ReconcileAction.Hard, Reconciler.Compare(s, h, "Earth", 30.0, Cfg).Action, "thrust from the state's time on does not matter");
        }

        [Test]
        public void Reconcile_OnTheGround_TwoMetres()
        {
            StateHistory h = new StateHistory();
            FlightState g = Orbit(0);
            g.Mode = FlightMode.Landed;
            for (int i = 0; i <= 100; i++) h.Push(i * 0.02, g);
            CraftStateDto s = StateMapper.FromFlightState(g, 1.0, "c");
            s.Position[0] += 1.5;
            Assert.AreEqual(ReconcileAction.Ignore, Reconciler.Compare(s, h, null, double.PositiveInfinity, Cfg).Action);
            s.Position[0] += 1.0;
            Assert.AreEqual("ground", Reconciler.Compare(s, h, null, double.PositiveInfinity, Cfg).Reason);
        }

        [Test]
        public void SoftCorrection_MovesPhysicsAndHistory_ViewCatchesUp()
        {
            StateHistory h = Filled(0, 60);
            Vec3 dp = new Vec3(40, -10, 5), dv = new Vec3(0, 0.3, 0);
            ReconcileResult r = Reconciler.Compare(Server(50, dp, dv), h, "Earth", double.PositiveInfinity, Cfg);
            Assert.AreEqual(ReconcileAction.Soft, r.Action);
            FlightState now = Orbit(60);
            Vec3 shownBefore = now.Position;
            ViewSmoothing view = default;
            Reconciler.ApplySoft(ref now, 60, h, r, ref view);
            Assert.Less(Vec3.Length(now.Position - (Pos(60) + dp + dv * 10)), 1e-6);
            Assert.Less(Vec3.Length(now.Position + view.Offset - shownBefore), 1e-6, "the view has not moved yet");
            Assert.AreEqual(ReconcileAction.Ignore, Reconciler.Compare(Server(50, dp, dv), h, "Earth", double.PositiveInfinity, Cfg).Action,
                "the shifted history agrees with the server");
            Vec3 start = view.Offset;
            view.Decay(0.5, Cfg.ViewTau);
            Assert.AreEqual(Math.Exp(-1), Vec3.Length(view.Offset) / Vec3.Length(start), 1e-12);
        }

        [Test]
        public void Fuel_IsTheServers_MinusWhatTheClientBurnedSince()
        {
            StateHistory h = new StateHistory();
            for (int i = 0; i <= 1000; i++)
            {
                FlightState s = Orbit(i * 0.02);
                s.MainFuel = 1000 - (i > 500 ? (i - 500) * 0.1 : 0); // a burn from t = 10 on
                s.RcsFuel = 100;
                h.Push(i * 0.02, s);
            }
            FlightState now = Orbit(20);
            now.MainFuel = 950;
            now.RcsFuel = 100;
            CraftStateDto server = Server(5);
            server.Fuel = 990;     // the server knew 10 kg less before the burn
            server.RcsFuel = 99;
            Reconciler.ApplyFuel(ref now, server, h);
            Assert.AreEqual(940, now.MainFuel, 1e-9);
            Assert.AreEqual(99, now.RcsFuel, 1e-9);
        }

        [Test]
        public void HardCorrection_CarriesTheServerStateToNow()
        {
            FlightParams p = FlightFixture.Params();
            CraftStateDto server = Server(10);
            FlightState s = Reconciler.ApplyHard(server, p, 20, 0.02, 0.7);
            Assert.Less(Vec3.Length(s.Position - Pos(20)), 1e-3);
            Assert.AreEqual(0.7, s.Throttle, "the lever stays where the pilot holds it");
        }

        [Test]
        public void Ghost_FollowsTheAnalyticOrbit_AndDoesNotJumpOnScreen()
        {
            FlightParams p = FlightFixture.Params();
            GhostConfig cfg = GhostConfig.Default();
            Ghost g = new Ghost("other");
            g.OnServerState(Server(0), p, 0, cfg);
            Assert.AreEqual(Vec3.Zero, g.View.Offset);
            for (int i = 1; i <= 3000; i++) g.AdvanceTo(i * 0.02, p, cfg);
            Assert.Less(Vec3.Length(g.State.Position - Pos(60)), 0.01, "60 s of 0.02 s steps");
            g.AdvanceTo(660, p, cfg); // a 10 min gap crossed with the catch-up step
            Assert.Less(Vec3.Length(g.State.Position - Pos(660)), 1.0);
            Vec3 shown = g.ShownPosition;
            g.OnServerState(Server(659, new Vec3(30, 0, 0)), p, 660, cfg);
            Assert.Less(Vec3.Length(g.ShownPosition - shown), 1e-6, "a new state does not move the shown craft at once");
            Assert.Less(Vec3.Length(g.State.Position - (Pos(660) + new Vec3(30, 0, 0))), 1.0, "the physics took it");
            for (int i = 1; i <= 150; i++) g.AdvanceTo(660 + i * 0.02, p, cfg);
            Assert.Less(Vec3.Length(g.View.Offset), 30 * Math.Exp(-5) + 1e-6, "caught up after 3 s");
        }
    }

    /// <summary>Stage 2c.4b: uplink and attitude policy.</summary>
    public class Stage2c4bUplinkTests
    {
        private static ThrustSegment Seg(double start, double rcsStart, double rcsEnd) => new ThrustSegment
        {
            StartTime = start, EndTime = start + 5, Duration = 5, Throttle = 1, Direction = Vec3.UnitY,
            Start = new AttitudeSample { Time = start, Orientation = DQuat.Identity, RcsUsedTotal = rcsStart },
            End = new AttitudeSample { Time = start + 5, Orientation = DQuat.Identity, RcsUsedTotal = rcsEnd },
        };

        private static ManeuverResultDto Ok(long seq) => new ManeuverResultDto { Accepted = true, Seq = seq, LastSeq = seq, CommittedUntil = 1 };

        private static Uplink Fresh(double baseline = 2.0)
        {
            Uplink u = new Uplink(UplinkConfig.Default());
            u.Reset("c-1", 0, null, baseline);
            return u;
        }

        [Test]
        public void StopAndWait_InOrder_SeqSharedByBothKinds_RcsFromTheBaseline()
        {
            Uplink u = Fresh();
            u.EnqueueSegment(Seg(10, 2.0, 2.5));
            u.EnqueueAttitude(new AttitudeSample { Time = 20, Orientation = DQuat.Identity, RcsUsedTotal = 3.0 });
            u.EnqueueSegment(Seg(30, 3.0, 3.25));
            Assert.AreEqual(10.0, u.EarliestPendingThrust);

            Assert.IsTrue(u.TryTakeToSend(0, out OutgoingMessage a));
            Assert.AreEqual(MessageTypes.Maneuver, a.Type);
            Assert.AreEqual(1L, a.Seq);
            Assert.AreEqual(0.5, ((ManeuverRequestDto) a.Body).RcsFuelUsed, 1e-12);
            Assert.AreEqual("c-1", ((ManeuverRequestDto) a.Body).CraftId);
            Assert.IsFalse(u.TryTakeToSend(0.1, out _), "one at a time");
            Assert.AreEqual(UplinkOutcome.Accepted, u.OnManeuverResult(Ok(1)));
            Assert.AreEqual(2.5, u.RcsBaseline);
            Assert.AreEqual(30.0, u.EarliestPendingThrust);

            Assert.IsTrue(u.TryTakeToSend(0.2, out OutgoingMessage b));
            Assert.AreEqual(MessageTypes.Attitude, b.Type);
            Assert.AreEqual(2L, b.Seq);
            Assert.AreEqual(0.5, ((AttitudeReportDto) b.Body).RcsFuelUsed, 1e-12);
            Assert.AreEqual(UplinkOutcome.Ignored, u.OnManeuverResult(Ok(2)), "a reply of the other kind is not the answer");
            Assert.AreEqual(UplinkOutcome.Accepted, u.OnAttitudeResult(new AttitudeResultDto { Accepted = true, Seq = 2, LastSeq = 2 }));

            Assert.IsTrue(u.TryTakeToSend(0.3, out OutgoingMessage c));
            Assert.AreEqual(3L, c.Seq);
            Assert.AreEqual(0.25, ((ManeuverRequestDto) c.Body).RcsFuelUsed, 1e-12);
            Assert.AreEqual(UplinkOutcome.Ignored, u.OnManeuverResult(Ok(2)), "a late reply to an older seq");
            Assert.AreEqual(UplinkOutcome.Accepted, u.OnManeuverResult(Ok(3)));
            Assert.AreEqual(0, u.Pending);
            Assert.AreEqual(3L, u.LastAcceptedSeq);
            Assert.AreEqual(double.PositiveInfinity, u.EarliestPendingThrust);
        }

        [Test]
        public void Duplicate_IsAnAcknowledgement()
        {
            Uplink u = Fresh();
            u.EnqueueSegment(Seg(10, 2.0, 2.5));
            u.TryTakeToSend(0, out _);
            Assert.AreEqual(UplinkOutcome.Accepted, u.OnManeuverResult(new ManeuverResultDto { Accepted = false, Reason = Reasons.Duplicate, Seq = 1, LastSeq = 1 }));
            Assert.IsFalse(u.NeedsResync);
            Assert.AreEqual(2.5, u.RcsBaseline);
        }

        [Test]
        public void Rejection_ClearsTheQueue_AndAsksForResync()
        {
            Uplink u = Fresh();
            u.EnqueueSegment(Seg(10, 2, 2));
            u.EnqueueSegment(Seg(15, 2, 2));
            u.TryTakeToSend(0, out _);
            Assert.AreEqual(UplinkOutcome.Rejected, u.OnManeuverResult(new ManeuverResultDto { Accepted = false, Reason = "insufficient_fuel", Seq = 1, LastSeq = 0 }));
            Assert.IsTrue(u.NeedsResync);
            Assert.AreEqual("insufficient_fuel", u.LastRejection);
            Assert.AreEqual(0, u.Pending);
            u.EnqueueSegment(Seg(20, 2, 2));
            Assert.IsFalse(u.TryTakeToSend(1, out _), "nothing goes out before the resync");
            u.Reset("c-1", 0, null, 7.0);
            Assert.IsTrue(u.TryTakeToSend(2, out OutgoingMessage m));
            Assert.AreEqual(1L, m.Seq, "the rejected seq was not accepted: it is used again");
        }

        [Test]
        public void Timeout_ThenTheMessageHadArrived_IsDroppedAsAccepted()
        {
            Uplink u = Fresh();
            u.EnqueueSegment(Seg(10, 2.0, 2.5));
            u.EnqueueSegment(Seg(15, 2.5, 2.75));
            u.TryTakeToSend(0, out _);
            u.Tick(4.9);
            Assert.IsFalse(u.NeedsResync);
            u.Tick(5.1);
            Assert.IsTrue(u.NeedsResync);
            Assert.AreEqual("timeout", u.LastRejection);
            u.Reset("c-1", 1, 15.0, null); // hello_ok after the reconnect: lastSeq 1
            Assert.AreEqual(1, u.Pending);
            Assert.AreEqual(2.5, u.RcsBaseline, "its propellant counts as accepted");
            Assert.IsTrue(u.TryTakeToSend(6, out OutgoingMessage m));
            Assert.AreEqual(2L, m.Seq);
            Assert.AreEqual(0.25, ((ManeuverRequestDto) m.Body).RcsFuelUsed, 1e-12);
        }

        [Test]
        public void Timeout_ThenTheMessageWasLost_IsResentUnchanged()
        {
            Uplink u = Fresh();
            u.EnqueueSegment(Seg(10, 2.0, 2.5));
            u.TryTakeToSend(0, out OutgoingMessage first);
            u.Tick(6);
            u.Reset("c-1", 0, null, null);
            Assert.IsTrue(u.TryTakeToSend(7, out OutgoingMessage again));
            Assert.AreSame(first.Body, again.Body, "the same body: a duplicate if the first did arrive");
            Assert.AreEqual(1L, again.Seq);
        }

        [Test]
        public void ControlLost_StopsSending_UntilHello()
        {
            Uplink u = Fresh();
            u.EnqueueSegment(Seg(10, 2, 2));
            u.TryTakeToSend(0, out _);
            Assert.AreEqual(UplinkOutcome.ControlLost, u.OnManeuverResult(new ManeuverResultDto { Accepted = false, Reason = Reasons.ControlLost }));
            u.EnqueueSegment(Seg(15, 2, 2));
            Assert.IsFalse(u.TryTakeToSend(1, out _));
            u.Reset("c-1", 0, null, null);
            Assert.IsTrue(u.TryTakeToSend(2, out _));
        }

        [Test]
        public void NewCraft_DropsTheQueue_SeqStartsAtOne()
        {
            Uplink u = Fresh();
            u.EnqueueSegment(Seg(10, 2, 2));
            u.TryTakeToSend(0, out _);
            u.Reset("c-2", 0, 100, 0.0);
            Assert.AreEqual(0, u.Pending);
            u.EnqueueSegment(Seg(110, 0, 0.1));
            u.TryTakeToSend(1, out OutgoingMessage m);
            Assert.AreEqual(1L, m.Seq);
            Assert.AreEqual("c-2", ((ManeuverRequestDto) m.Body).CraftId);
        }

        private static AttitudePolicyInput In(double t, bool pilot = false, double rate = 0, int sas = 0, int phase = 0, double rcs = 0, bool burning = false, bool flying = true) =>
            new AttitudePolicyInput { RealTime = t, PilotRotating = pilot, AngularRate = rate, SasOn = sas, SasPhase = phase, RcsUsedTotal = rcs, Burning = burning, Flying = flying };

        [Test]
        public void Policy_ReleaseAndSasPhase_Trigger_AtMostOncePerSecond()
        {
            AttitudePolicyConfig cfg = AttitudePolicyConfig.Default();
            AttitudePolicyState st = default;
            Assert.IsFalse(AttitudePolicy.Update(ref st, In(0), cfg), "idle: nothing to say");
            Assert.IsFalse(AttitudePolicy.Update(ref st, In(0.1, pilot: true, rate: 0.0005), cfg));
            Assert.IsTrue(AttitudePolicy.Update(ref st, In(0.2, rate: 0.0005), cfg), "the pilot released the keys");
            Assert.IsFalse(AttitudePolicy.Update(ref st, In(0.5, sas: 1, phase: SasPhase.Settle), cfg), "SAS on, but within a second");
            Assert.IsFalse(AttitudePolicy.Update(ref st, In(1.1), cfg));
            Assert.IsTrue(AttitudePolicy.Update(ref st, In(1.25, sas: 1, phase: SasPhase.Settle), cfg), "the deferred trigger goes out");
            Assert.IsTrue(AttitudePolicy.Update(ref st, In(2.5, sas: 1, phase: SasPhase.Hold), cfg), "settle -> hold");
            Assert.IsFalse(AttitudePolicy.Update(ref st, In(3.0, sas: 1, phase: SasPhase.Hold), cfg));
        }

        [Test]
        public void Policy_Periodic_OnlyWhileTurningOrBurningRcs()
        {
            AttitudePolicyConfig cfg = AttitudePolicyConfig.Default();
            AttitudePolicyState st = default;
            int reports = 0;
            for (double t = 0; t < 60; t += 0.02) if (AttitudePolicy.Update(ref st, In(t, rate: 0.01), cfg)) reports++;
            Assert.AreEqual(6, reports, "every 10 s while turning");
            reports = 0;
            for (double t = 60; t < 120; t += 0.02) if (AttitudePolicy.Update(ref st, In(t, rate: 0.0), cfg)) reports++;
            Assert.AreEqual(0, reports, "still: no periodic reports");
            for (double t = 120; t < 140; t += 0.02) if (AttitudePolicy.Update(ref st, In(t, rcs: t > 125 ? 1.0 + t : 0), cfg)) reports++;
            Assert.GreaterOrEqual(reports, 1, "RCS propellant burned");
        }

        [Test]
        public void Policy_Silent_WhileBurningOrOnTheGround()
        {
            AttitudePolicyConfig cfg = AttitudePolicyConfig.Default();
            AttitudePolicyState st = default;
            Assert.IsFalse(AttitudePolicy.Update(ref st, In(0, pilot: true, burning: true), cfg));
            Assert.IsFalse(AttitudePolicy.Update(ref st, In(5, rate: 0.1, burning: true), cfg), "the release happened during a burn");
            Assert.IsFalse(AttitudePolicy.Update(ref st, In(10, rate: 0.1, flying: false), cfg));
            Assert.IsTrue(AttitudePolicy.Update(ref st, In(20, rate: 0.1), cfg), "coasting and turning again");
        }
    }
}
