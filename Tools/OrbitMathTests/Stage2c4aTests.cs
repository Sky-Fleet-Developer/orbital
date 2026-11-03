using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Orbital.Core.Flight;
using Orbital.Core.Net;

namespace Orbital.Tests
{
    /// <summary>Stage 2c.4a: the recorder with impulse means and attitude slices, the protocol codec, the state mapper.</summary>
    public class Stage2c4aRecorderTests
    {
        private static readonly RecorderConfig C = RecorderConfig.Online();

        private struct Flight
        {
            public List<ThrustSegment> Segments;
            public Dictionary<double, AttitudeSample> Samples;
            public double KernelImpulse;      // sum of mean thrust * dt / max thrust (throttle-seconds)
            public Vec3 KernelVector;         // the same along the thrust direction of each step
            public double Dropped;
        }

        /// <summary>
        /// Deep space, 0.02 s steps: full throttle, a pitch turn under thrust, a smooth throttle-down, the SAS switched on, then the engine cut.
        /// The recorder is fed exactly as the client system feeds it.
        /// </summary>
        private static Flight Fly(bool flushEverySecond = false)
        {
            FlightParams p = FlightFixture.DeepSpace();
            FlightState s = FlightFixture.SpaceCraft(p);
            RecorderState r = default;
            Flight f = new Flight { Segments = new List<ThrustSegment>(), Samples = new Dictionary<double, AttitudeSample>() };
            const double dt = 0.02;
            double rcsTotal = 0;
            double t = 100.0;
            // the client samples every step from the spawn on: the state before the first step is a sample too
            ThrustSegmentRecorder.Update(ref r, new RecorderSample { Time = t, ThrustDirection = Vec3.UnitY }, AttitudeSample.Of(s, t, 0), C);
            f.Samples[t] = AttitudeSample.Of(s, t, 0);
            for (int i = 0; i < 1500; i++)
            {
                ControlInput input = default;
                input.RcsOn = true;
                double phase = i * dt;
                if (phase < 0.1) input.ThrottleFull = true;
                if (phase >= 4 && phase < 6) input.Pitch = 1;
                if (phase >= 6 && phase < 7) input.Pitch = -1;
                if (phase >= 10 && phase < 13) input.ThrottleSmooth = -1;
                if (phase >= 15) input.SasOn = true;
                if (phase >= 24) input.ThrottleZero = true;
                Vec3 dir = DQuat.Rotate(s.Orientation, Vec3.UnitY);
                FlightKernel.Step(ref s, input, p, t, dt, out StepReport rep);
                t = 100.0 + (i + 1) * dt;
                rcsTotal += rep.RcsFuelUsed;
                f.KernelImpulse += rep.MeanThrust * dt / p.Craft.Engine.MaxThrust;
                f.KernelVector = f.KernelVector + dir * (rep.MeanThrust * dt / p.Craft.Engine.MaxThrust);
                RecorderSample sample = new RecorderSample
                {
                    Time = t, Throttle = s.Throttle, ThrustDirection = DQuat.Rotate(s.Orientation, Vec3.UnitY), Sas = s.SasOn,
                    Resting = s.Mode != FlightMode.Flying,
                };
                AttitudeSample att = AttitudeSample.Of(s, t, rcsTotal);
                f.Samples[t] = att;
                ThrustSegmentRecorder.Update(ref r, sample, att, C);
                if (flushEverySecond && i % 50 == 49) ThrustSegmentRecorder.Flush(ref r);
                while (ThrustSegmentRecorder.TryDequeue(ref r, t, C, out ThrustSegment seg)) f.Segments.Add(seg);
            }
            while (ThrustSegmentRecorder.TryDequeue(ref r, 1e9 + f.Segments.Count, C, out ThrustSegment seg)) f.Segments.Add(seg);
            Assert.IsFalse(r.Active, "the engine was cut");
            Assert.AreEqual(0, r.Overflowed);
            f.Dropped = r.DroppedImpulse;
            return f;
        }

        [Test]
        public void ImpulseMeans_GiveTheKernelImpulse()
        {
            Flight f = Fly();
            Assert.Greater(f.Segments.Count, 4);
            double scalar = 0;
            Vec3 vector = Vec3.Zero;
            foreach (ThrustSegment seg in f.Segments)
            {
                scalar += seg.Throttle * seg.Duration;
                vector = vector + seg.Direction * (seg.Throttle * seg.Duration);
                Assert.LessOrEqual(seg.Duration, C.MaxDuration + 1e-9);
            }
            Assert.AreEqual(f.KernelImpulse, scalar + f.Dropped, 1e-9 * f.KernelImpulse, "throttle-seconds are integrated exactly");
            // a segment keeps one direction: its vector is the mean direction times the scalar impulse, which differs from the true
            // vector sum only by the spread of the direction inside the segment (below the 1 degree threshold)
            Assert.Less(Vec3.Length(vector - f.KernelVector), 2e-4 * f.KernelImpulse);
        }

        [Test]
        public void Segments_JoinBitwise_AndCarryTheAttitudeAtBothEnds()
        {
            Flight f = Fly();
            for (int i = 0; i < f.Segments.Count; i++)
            {
                ThrustSegment seg = f.Segments[i];
                Assert.AreEqual(seg.EndTime - seg.StartTime, seg.Duration);
                if (i > 0 && f.Segments[i - 1].EndTime >= seg.StartTime - 1e-9)
                    Assert.IsTrue(f.Segments[i - 1].EndTime == seg.StartTime, "continuous burn: the end is bitwise the next start");
                Assert.IsTrue(f.Samples.ContainsKey(seg.StartTime), "a segment starts at a sample");
                Assert.IsTrue(f.Samples.ContainsKey(seg.EndTime), "and ends at one");
                AttitudeSample a = f.Samples[seg.StartTime], b = f.Samples[seg.EndTime];
                Assert.AreEqual(a.Orientation, seg.Start.Orientation);
                Assert.AreEqual(a.Omega, seg.Start.Omega);
                Assert.AreEqual(a.GyroH, seg.Start.GyroH);
                Assert.AreEqual(a.RcsUsedTotal, seg.Start.RcsUsedTotal);
                Assert.AreEqual(b.Orientation, seg.End.Orientation);
                Assert.AreEqual(b.SasOn, seg.End.SasOn);
                Assert.AreEqual(b.SasTarget, seg.End.SasTarget);
                Assert.AreEqual(seg.StartTime, seg.Start.Time);
                Assert.AreEqual(seg.EndTime, seg.End.Time);
            }
            int seams = 0;
            for (int i = 1; i < f.Segments.Count; i++) if (f.Segments[i - 1].EndTime == f.Segments[i].StartTime) seams++;
            Assert.GreaterOrEqual(seams, f.Segments.Count - 2, "one burn: all but the edges are seams");
        }

        [Test]
        public void Flush_SplitsWithoutLoss()
        {
            Flight plain = Fly();
            Flight flushed = Fly(flushEverySecond: true);
            Assert.Greater(flushed.Segments.Count, plain.Segments.Count);
            double a = 0, b = 0;
            foreach (ThrustSegment s in plain.Segments) a += s.Throttle * s.Duration;
            foreach (ThrustSegment s in flushed.Segments) b += s.Throttle * s.Duration;
            Assert.AreEqual(a + plain.Dropped, b + flushed.Dropped, 1e-9 * a);
            foreach (ThrustSegment s in flushed.Segments) Assert.LessOrEqual(s.Duration, 1.0 + 1e-9);
        }

        [Test]
        public void Clear_ForgetsEverything_AndTheBurnRestartsAtTheNextSample()
        {
            RecorderState r = default;
            Vec3 up = Vec3.UnitY;
            for (int i = 0; i <= 400; i++)
                ThrustSegmentRecorder.Update(ref r, new RecorderSample { Time = i * 0.02, Throttle = 0.8, ThrustDirection = up }, C);
            Assert.Greater(r.Count, 0);
            ThrustSegmentRecorder.Clear(ref r);
            Assert.AreEqual(0, r.Count);
            Assert.IsFalse(r.Active);
            ThrustSegmentRecorder.Update(ref r, new RecorderSample { Time = 8.04, Throttle = 0.8, ThrustDirection = up }, C);
            Assert.IsTrue(r.Active);
            Assert.AreEqual(8.04, r.Open.StartTime, "no previous sample after a clear");
        }

        [Test]
        public void OnlineConfig_FiveSecondSegments_HalfSecondSends()
        {
            Assert.AreEqual(5.0, C.MaxDuration);
            Assert.AreEqual(0.5, C.MinSendInterval);
            Assert.AreEqual(RecorderConfig.Default().MinDuration, C.MinDuration);
        }

        [Test]
        public void ThrottleRamp_MeanIsTheIntegral()
        {
            RecorderState r = default;
            // throttle 0.50 -> 0.54 linearly over 2 s (below the 0.05 threshold), then cut
            for (int i = 0; i <= 100; i++)
                ThrustSegmentRecorder.Update(ref r, new RecorderSample { Time = i * 0.02, Throttle = 0.5 + 0.0004 * i, ThrustDirection = Vec3.UnitY }, C);
            ThrustSegmentRecorder.Update(ref r, new RecorderSample { Time = 2.02, Throttle = 0, ThrustDirection = Vec3.UnitY }, C);
            Assert.IsTrue(ThrustSegmentRecorder.TryDequeue(ref r, 10, C, out ThrustSegment seg));
            Assert.AreEqual(0.0, seg.StartTime);
            Assert.AreEqual(2.0, seg.EndTime, 1e-12);
            double expected = 0;
            for (int i = 1; i <= 100; i++) expected += (0.5 + 0.0004 * i) * 0.02;
            Assert.AreEqual(expected / 2.0, seg.Throttle, 1e-12);
        }
    }

    public class Stage2c4aProtocolTests
    {
        // As the server writes it (System.Text.Json, ServerConfig.Json): camelCase, enums as camelCase strings, nulls written.
        private const string HelloOk = @"{""type"":""hello_ok"",""id"":7,""nickname"":""Alpha"",""craftId"":""c-1"",""token"":""tok"",""created"":false,
""serverTime"":12345.5,""timeScale"":{""scale"":10,""since"":12000},""clock"":{""bootId"":""2026-11-03T10:00:00Z-ab"",""resumedAt"":12000,""downtimeReal"":null,""policy"":""freeze"",""source"":""heartbeat""},
""snapshot"":{""id"":""c-1"",""callsign"":""Alpha"",""ownerAccountId"":""alpha"",""control"":""player"",
 ""spec"":{""dryMass"":6000,""maxFuel"":30000,""thrustN"":500000,""ispSeconds"":420,
   ""flight"":{""dryMass"":5000,""mainFuelCapacity"":40000,""rcsPropellantCapacity"":150,""inertia"":{""x"":1700000,""y"":50000,""z"":1700000},
     ""engine"":{""maxThrust"":600000,""isp"":450,""throttleRate"":0.25},""thrustOffset"":{""x"":0.005,""y"":0,""z"":0},""sas"":{""naturalFrequency"":0.9}}},
 ""fuel"":30000,""rcsFuel"":150,""attitude"":null,""mode"":""landed"",""parentId"":""Earth"",""snapshotTime"":12000,""version"":3,
 ""orbit"":null,""radial"":null,""nextTransition"":null,""plannedUntil"":0,""latitude"":0.1,""longitude"":0.2,""impactSpeed"":0,""burn"":null,""mass"":36150},
""state"":{""craftId"":""c-1"",""callsign"":""Alpha"",""time"":12345.5,""mode"":""landed"",""parentId"":""Earth"",""position"":[6000000.25,1.5,-2.75],
 ""velocity"":[1,2,3],""globalPosition"":[1,2,3],""mass"":36150,""fuel"":30000,""rcsFuel"":150,""orientation"":[1,0,0,0],""angularVelocity"":[0,0,0],
 ""gyroH"":[0,0,0],""inertia"":[1,2,3],""sas"":null,""burning"":false,""latitude"":0.1,""longitude"":0.2,""altitude"":0,""surfaceVelocity"":[0,0,0],
 ""velocityRelativeToSurface"":[0,0,0],""version"":3,""orbit"":null,""crashedAt"":null,""destroyAt"":null},
""lastSeq"":4,""committedUntil"":12001.25,""control"":true,""worldHash"":""d73d38e4"",""worldEpochZero"":0}";

        [Test]
        public void HelloOk_FromTheServer_IsRead()
        {
            Envelope e = ProtocolCodec.Decode(HelloOk);
            Assert.AreEqual(MessageTypes.HelloOk, e.Type);
            Assert.AreEqual(7L, e.Id);
            HelloOkMessage m = e.As<HelloOkMessage>();
            Assert.AreEqual("c-1", m.CraftId);
            Assert.AreEqual(12345.5, m.ServerTime);
            Assert.AreEqual(10.0, m.TimeScale.Scale);
            Assert.AreEqual("2026-11-03T10:00:00Z-ab", m.Clock.BootId, "no date parsing");
            Assert.IsNull(m.Clock.DowntimeReal);
            Assert.AreEqual(CraftModeDto.Landed, m.Snapshot.Mode);
            Assert.AreEqual(CraftModeDto.Landed, m.State.Mode);
            Assert.AreEqual(6000000.25, m.State.Position[0]);
            Assert.AreEqual(4L, m.LastSeq);
            Assert.AreEqual(12001.25, m.CommittedUntil);
            Assert.IsTrue(m.Control);
            Assert.AreEqual("d73d38e4", m.WorldHash);

            FlightConfig f = StateMapper.FlightConfigOf(m.Snapshot, FlightConfig.Default());
            Assert.AreEqual(6000, f.DryMass, "the spec numbers win over spec.flight (server EffectiveFlight)");
            Assert.AreEqual(30000, f.MainFuelCapacity);
            Assert.AreEqual(500000, f.Engine.MaxThrust);
            Assert.AreEqual(420, f.Engine.Isp);
            Assert.AreEqual(150, f.RcsPropellantCapacity, "from spec.flight");
            Assert.AreEqual(0.9, f.Sas.NaturalFrequency);
            Assert.AreEqual(FlightConfig.Default().Sas.Damping, f.Sas.Damping, "missing fields keep the defaults");
            Assert.AreEqual(FlightConfig.Default().Gyro.MaxTorque, f.Gyro.MaxTorque);
        }

        [Test]
        public void ManeuverResult_DuplicateAndRejection_AreRead()
        {
            const string json = @"{""type"":""maneuver_result"",""id"":9,""result"":{""accepted"":false,""reason"":""duplicate"",""message"":""seq 5 was already accepted; nothing changed"",
""startTime"":0,""endTime"":0,""fuelUsed"":0,""fuelRemaining"":0,""rcsFuelUsed"":0,""rcsFuelRemaining"":0,""deltaV"":0,""maxDeltaV"":0,""outcome"":null,
""destroyAt"":null,""stateAtEnd"":null,""state"":{""craftId"":""c-1"",""time"":50,""mode"":""flying"",""position"":[7000000,0,0],""velocity"":[0,7500,0],
""fuel"":100,""rcsFuel"":5,""orientation"":[0.5,0.5,0.5,0.5],""angularVelocity"":[0.01,0,0],""gyroH"":[1,2,3],
""sas"":{""on"":true,""phase"":0,""target"":[1,0,0,0],""integral"":[0,-3000,0]},""burning"":false,""latitude"":null,""longitude"":null},
""version"":0,""seq"":5,""lastSeq"":5,""committedUntil"":49.5}}";
            Envelope e = ProtocolCodec.Decode(json);
            ManeuverResultDto r = e.As<ResultMessage<ManeuverResultDto>>().Result;
            Assert.IsFalse(r.Accepted);
            Assert.AreEqual(Reasons.Duplicate, r.Reason);
            Assert.AreEqual(5L, r.Seq);
            Assert.AreEqual(5L, r.LastSeq);
            Assert.IsNull(r.StateAtEnd);
            FlightState s = StateMapper.ToFlightState(r.State);
            Assert.AreEqual(FlightMode.Flying, s.Mode);
            Assert.AreEqual(7500, s.Velocity.Y);
            Assert.AreEqual(100, s.MainFuel);
            Assert.AreEqual(5, s.RcsFuel);
            Assert.AreEqual(1, s.SasOn);
            Assert.AreEqual(-3000, s.SasIntegral.Y);
            Assert.AreEqual(0.01, s.Omega.X);
        }

        [Test]
        public void Pushes_HaveNoId_AndAreRead()
        {
            Envelope ts = ProtocolCodec.Decode(@"{""type"":""time_scale"",""scale"":10,""time"":1010,""previous"":1}");
            Assert.IsNull(ts.Id);
            Assert.AreEqual(10.0, ts.As<TimeScaleMessage>().Scale);
            Envelope cu = ProtocolCodec.Decode(@"{""type"":""craft_update"",""time"":5,""cause"":""maneuver"",""craftId"":""c-2"",""seq"":3,""state"":{""craftId"":""c-2"",""mode"":""crashed"",""crashedAt"":4,""destroyAt"":19}}");
            CraftUpdateMessage u = cu.As<CraftUpdateMessage>();
            Assert.AreEqual(3L, u.Seq);
            Assert.AreEqual(CraftModeDto.Crashed, u.State.Mode);
            Assert.AreEqual(19.0, u.State.DestroyAt);
            Envelope st = ProtocolCodec.Decode(@"{""type"":""states"",""id"":2,""time"":5.5,""states"":[{""craftId"":""a"",""mode"":""flying""},{""craftId"":""b"",""mode"":""landed""}]}");
            Assert.AreEqual(2, st.As<StatesMessage>().States.Count);
            Envelope pong = ProtocolCodec.Decode(@"{""type"":""pong"",""id"":3,""time"":100.125,""scale"":1,""bootId"":""x""}");
            Assert.AreEqual(100.125, pong.As<PongMessage>().Time);
        }

        [Test]
        public void Encode_PutsTypeAndId_LeavesNullsOut_UsesInvariantNumbers()
        {
            var culture = System.Threading.Thread.CurrentThread.CurrentCulture;
            try
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("ru-RU");
                string json = ProtocolCodec.Encode(MessageTypes.Attitude, 12, new AttitudeReportDto
                {
                    Time = 1.5, Orientation = new[] { 1.0, 0, 0, 0 }, RcsFuelUsed = 0.125, Seq = 3, CraftId = "c-1",
                });
                JObject o = JObject.Parse(json);
                Assert.AreEqual("attitude", (string) o["type"]);
                Assert.AreEqual(12, (long) o["id"]);
                Assert.AreEqual(0.125, (double) o["rcsFuelUsed"]);
                Assert.IsNull(o["sas"], "null fields are not written");
                Assert.IsNull(o["omega"]);
                Assert.IsTrue(json.Contains("\"rcsFuelUsed\":0.125"), json);
                Assert.IsTrue(json.StartsWith("{\"type\":\"attitude\",\"id\":12,"), json);
            }
            finally { System.Threading.Thread.CurrentThread.CurrentCulture = culture; }
        }

        [Test]
        public void Secret_IsMaskedInLogs()
        {
            string json = ProtocolCodec.Encode(MessageTypes.AdminAuth, 1, new AdminAuthRequest { Secret = "very-secret-value-123" });
            Assert.IsTrue(json.Contains("very-secret-value-123"), "the wire carries it");
            string log = ProtocolCodec.ForLog(json);
            Assert.IsFalse(log.Contains("very-secret-value-123"), log);
            Assert.IsTrue(log.Contains(ProtocolCodec.Masked));
            Assert.IsFalse(ProtocolCodec.ForLog("{\"a\":{\"Secret\":\"zzz\"},\"b\":[{\"secret\":\"yyy\"}]}").Contains("zzz"));
            Assert.IsFalse(ProtocolCodec.ForLog("{broken \"secret\":\"qqq\"").Contains("qqq"));
        }

        [Test]
        public void Decode_RejectsNonObjects()
        {
            Assert.Throws<Newtonsoft.Json.JsonReaderException>(() => ProtocolCodec.Decode("[1,2]"));
        }
    }

    public class Stage2c4aMapperTests
    {
        private static FlightState Sample()
        {
            FlightState s = default;
            s.Position = new Vec3(7.1e6 + 0.1, -2.3e5, 1.0 / 3.0);
            s.Velocity = new Vec3(12.5, 7600.0 / 7.0, -0.001);
            s.Orientation = DQuat.FromAxisAngle(new Vec3(1, 2, 3), 0.7);
            s.Omega = new Vec3(0.01, -0.02, 1e-7);
            s.GyroH = new Vec3(100, -2e4, 3);
            s.MainFuel = 1234.5678;
            s.RcsFuel = 99.75;
            s.Mode = FlightMode.Flying;
            s.SasOn = 1;
            s.SasPhase = SasPhase.Settle;
            s.SasTarget = DQuat.FromAxisAngle(new Vec3(0, 0, 1), 0.3);
            s.SasIntegral = new Vec3(0, -2999.5, 1);
            return s;
        }

        [Test]
        public void FlightState_ThroughJson_RoundTripsBitwise()
        {
            FlightState s = Sample();
            string json = ProtocolCodec.Encode(MessageTypes.States, 1, new StatesMessage
            {
                Time = 5, States = new List<CraftStateDto> { StateMapper.FromFlightState(s, 5, "c-1") },
            });
            CraftStateDto back = ProtocolCodec.Decode(json).As<StatesMessage>().States[0];
            FlightState r = StateMapper.ToFlightState(back);
            Assert.AreEqual(s.Position, r.Position);
            Assert.AreEqual(s.Velocity, r.Velocity);
            Assert.AreEqual(s.Omega, r.Omega);
            Assert.AreEqual(s.GyroH, r.GyroH);
            Assert.AreEqual(s.MainFuel, r.MainFuel);
            Assert.AreEqual(s.RcsFuel, r.RcsFuel);
            Assert.AreEqual(s.SasOn, r.SasOn);
            Assert.AreEqual(s.SasPhase, r.SasPhase);
            Assert.AreEqual(s.SasIntegral, r.SasIntegral);
            Assert.AreEqual(1.0, Math.Abs(DQuat.Multiply(DQuat.Conjugate(s.Orientation), r.Orientation).W), 1e-15);
            Assert.AreEqual(1.0, Math.Abs(DQuat.Multiply(DQuat.Conjugate(s.SasTarget), r.SasTarget).W), 1e-15);
        }

        [Test]
        public void Landed_KeepsGroundCoordinates_SasOffIsNull()
        {
            FlightState s = Sample();
            s.Mode = FlightMode.Landed;
            s.Latitude = 0.25;
            s.Longitude = -1.5;
            s.SasOn = 0;
            CraftStateDto dto = StateMapper.FromFlightState(s, 1, "c");
            Assert.IsNull(dto.Sas);
            Assert.AreEqual(CraftModeDto.Landed, dto.Mode);
            FlightState r = StateMapper.ToFlightState(dto);
            Assert.AreEqual(FlightMode.Landed, r.Mode);
            Assert.AreEqual(0.25, r.Latitude);
            Assert.AreEqual(-1.5, r.Longitude);
            Assert.AreEqual(0, r.SasOn);
        }

        [Test]
        public void ApplyAuthoritative_LeavesTheAttitudeAlone()
        {
            FlightState mine = Sample();
            FlightState server = Sample();
            server.Position = new Vec3(1, 2, 3);
            server.MainFuel = 10;
            server.Orientation = DQuat.Identity;
            server.Omega = Vec3.Zero;
            StateMapper.ApplyAuthoritative(StateMapper.FromFlightState(server, 0, "c"), ref mine);
            Assert.AreEqual(new Vec3(1, 2, 3), mine.Position);
            Assert.AreEqual(10, mine.MainFuel);
            Assert.AreEqual(Sample().Orientation, mine.Orientation);
            Assert.AreEqual(Sample().Omega, mine.Omega);
        }

        [Test]
        public void Segment_BecomesAManeuverRequest()
        {
            FlightState a = Sample(), b = Sample();
            b.Orientation = DQuat.FromAxisAngle(new Vec3(1, 2, 3), 0.72);
            b.SasOn = 0;
            ThrustSegment seg = new ThrustSegment
            {
                StartTime = 10, EndTime = 15, Duration = 5, Throttle = 0.75, Direction = DQuat.Rotate(a.Orientation, Vec3.UnitY),
                Start = AttitudeSample.Of(a, 10, 1.0), End = AttitudeSample.Of(b, 15, 1.25),
            };
            seg.Start.Orientation = new DQuat(2 * seg.Start.Orientation.W, 2 * seg.Start.Orientation.X, 2 * seg.Start.Orientation.Y, 2 * seg.Start.Orientation.Z);
            ManeuverRequestDto m = StateMapper.ToManeuver(seg, 6, "c-1", 0.25);
            Assert.AreEqual(10.0, m.StartTime);
            Assert.AreEqual(5.0, m.Duration);
            Assert.AreEqual(0.75, m.Throttle);
            Assert.AreEqual(6L, m.Seq);
            Assert.AreEqual("c-1", m.CraftId);
            Assert.AreEqual(0.25, m.RcsFuelUsed);
            double n = Math.Sqrt(m.Orientation[0] * m.Orientation[0] + m.Orientation[1] * m.Orientation[1] + m.Orientation[2] * m.Orientation[2] + m.Orientation[3] * m.Orientation[3]);
            Assert.AreEqual(1.0, n, 1e-12, "quaternions are normalised before sending");
            Assert.IsNotNull(m.Sas);
            Assert.AreEqual(SasPhase.Settle, m.Sas.Phase);
            Assert.IsNull(m.EndSas, "SAS off at the end");
            JObject o = JObject.Parse(ProtocolCodec.Encode(MessageTypes.Maneuver, 3, m));
            foreach (string key in new[] { "direction", "throttle", "startTime", "duration", "orientation", "omega", "gyroH", "endOrientation", "endOmega", "endGyroH", "rcsFuelUsed", "sas", "seq", "craftId" })
                Assert.IsNotNull(o[key], key);
            Assert.IsNull(o["endSas"]);
        }

        [Test]
        public void AttitudeSample_BecomesAReport()
        {
            FlightState s = Sample();
            AttitudeReportDto r = StateMapper.ToAttitudeReport(AttitudeSample.Of(s, 42, 3.5), 9, "c-1", -1e-15);
            Assert.AreEqual(42.0, r.Time);
            Assert.AreEqual(0.0, r.RcsFuelUsed, "never negative");
            Assert.AreEqual(9L, r.Seq);
            Assert.AreEqual(s.GyroH.Y, r.GyroH[1]);
            Assert.AreEqual(s.Omega.X, r.Omega[0]);
        }

        [Test]
        public void FlightConfig_WithoutSpecFlight_UsesTheFallbackWithTheSpecNumbers()
        {
            CraftSnapshotDto snap = new CraftSnapshotDto { Spec = new CraftSpecDto { DryMass = 1, MaxFuel = 2, ThrustN = 3, IspSeconds = 4 } };
            FlightConfig fallback = FlightConfig.Default();
            fallback.Gyro.MaxTorque = 1234;
            FlightConfig f = StateMapper.FlightConfigOf(snap, fallback);
            Assert.AreEqual(1234, f.Gyro.MaxTorque);
            Assert.AreEqual(3, f.Engine.MaxThrust);
            Assert.AreEqual(1234, StateMapper.FlightConfigOf(null, fallback).Gyro.MaxTorque);
        }
    }
}
