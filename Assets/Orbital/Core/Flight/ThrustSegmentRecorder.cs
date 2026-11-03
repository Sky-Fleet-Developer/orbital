using System;

namespace Orbital.Core.Flight
{
    /// <summary>Recorder thresholds (Docs/Stage2-Plan.md, section 3).</summary>
    public struct RecorderConfig
    {
        public double ThrottleDelta;    // close the segment when throttle moves by more than this (0.05)
        public double AngleRadians;     // ... or the thrust direction turns by more than this (1 degree)
        public double MaxDuration;      // ... or it lasts this long (10..30 s)
        public double MinDuration;      // changes are not evaluated before the segment has lasted this long (0.5 s, the server RK4 step)
        public double MinSendInterval;  // at least this long between two sends (0.5..1 s)
        public double MinThrottle;      // below this the engine counts as off

        public static RecorderConfig Default() => new RecorderConfig
        {
            ThrottleDelta = 0.05,
            AngleRadians = Math.PI / 180.0,
            MaxDuration = 20.0,
            MinDuration = 0.5,
            MinSendInterval = 1.0,
            MinThrottle = 0.01,
        };

        /// <summary>
        /// Online play (stage 2c.4): segments of at most 5 s (a segment reaches the server only after it closes, and the server keeps a
        /// limited retro window), sends at most every 0.5 s.
        /// </summary>
        public static RecorderConfig Online()
        {
            RecorderConfig c = Default();
            c.MaxDuration = 5.0;
            c.MinSendInterval = 0.5;
            return c;
        }
    }

    /// <summary>
    /// Attitude slice of the craft at <see cref="Time"/> (stage 2c.4): what the server takes at the start and at the end of a thrust
    /// segment and in an attitude report. <see cref="RcsUsedTotal"/> is the RCS propellant burned since an arbitrary origin kept by the
    /// caller (cumulative counter): the propellant of a message is a difference of two counters.
    /// </summary>
    public struct AttitudeSample
    {
        public double Time;
        public DQuat Orientation;
        public Vec3 Omega;
        public Vec3 GyroH;
        public double RcsUsedTotal;
        public int SasOn;
        public int SasPhase;
        public DQuat SasTarget;
        public Vec3 SasIntegral;

        /// <summary>The attitude part of a flight state at time t.</summary>
        public static AttitudeSample Of(in FlightState s, double time, double rcsUsedTotal) => new AttitudeSample
        {
            Time = time, Orientation = s.Orientation, Omega = s.Omega, GyroH = s.GyroH, RcsUsedTotal = rcsUsedTotal,
            SasOn = s.SasOn, SasPhase = s.SasPhase, SasTarget = s.SasTarget, SasIntegral = s.SasIntegral,
        };
    }

    /// <summary>
    /// One thrust segment in the shape the server takes: direction in the parent inertial frame, throttle, start, duration.
    /// <see cref="Direction"/> and <see cref="Throttle"/> are the impulse-weighted means over the segment (stage 2c.4), so a constant
    /// burn with these values gives the same impulse as the recorded steps. <see cref="EndTime"/> is bitwise the start of the next
    /// segment of a continuous burn; <see cref="Duration"/> = EndTime - StartTime.
    /// </summary>
    public struct ThrustSegment
    {
        public double StartTime;
        public double Duration;
        public double EndTime;
        public double Throttle;
        public Vec3 Direction;
        /// <summary>1 when the stability assist was on at the start of the segment (the server stores it with the attitude).</summary>
        public int Sas;
        /// <summary>Attitude at <see cref="StartTime"/> and at <see cref="EndTime"/> (zero when the caller gave none).</summary>
        public AttitudeSample Start;
        public AttitudeSample End;
    }

    /// <summary>One sample of the flight for the recorder, taken after a kernel step that ended at <see cref="Time"/>.</summary>
    public struct RecorderSample
    {
        public double Time;
        /// <summary>Throttle after the step: the throttle that step burned with (the kernel sets it at the start of the step).</summary>
        public double Throttle;
        /// <summary>Unit vector, body +Y in the inertial frame, after the step: the direction the next step thrusts along.</summary>
        public Vec3 ThrustDirection;
        public int Sas;                // 1 while the stability assist is on; a change closes the segment
        /// <summary>True while the craft stands on the ground and thrust does not exceed weight: nothing to record.</summary>
        public bool Resting;
    }

    /// <summary>State of the recorder. Fixed size (a small ring of closed segments), no allocations.</summary>
    public struct RecorderState
    {
        public const int Capacity = 8;
        public bool Active;
        /// <summary>The open segment: start, the throttle and direction of its first step (the thresholds compare with them), SAS, start attitude.</summary>
        public ThrustSegment Open;
        public double ImpulseThrottle;    // sum of throttle*dt over the open segment
        public Vec3 ImpulseDirection;     // sum of throttle*direction*dt over the open segment
        public double LastTime;
        public double LastSendTime;
        public int Count;                 // closed segments waiting to be sent
        public int Head;
        public ThrustSegment Q0, Q1, Q2, Q3, Q4, Q5, Q6, Q7;
        public int Overflowed;            // closed segments lost because the ring was full (the sender fell behind)
        public double DroppedImpulse;     // throttle*seconds of tails shorter than the minimum that were not sent
        // The previous sample: the step that ends at the next sample thrusts along its direction and starts at its time.
        public bool HasPrev;
        public double PrevTime;
        public double PrevThrottle;
        public Vec3 PrevDirection;
        public int PrevSas;
        public AttitudeSample PrevAttitude;
    }

    /// <summary>
    /// Turns the stream of control states into a few thrust segments. A segment is closed when the throttle or the direction moves
    /// beyond the thresholds, when it gets too long, or when the thrust stops. Changes are evaluated only after MinDuration, so a jittery
    /// throttle cannot produce a flood of tiny segments. On the ground, while thrust is below weight, nothing is recorded.
    /// <para>
    /// Timing follows <see cref="FlightKernel.Step"/>: the step from the previous sample to this one burned with this sample's throttle
    /// along the previous sample's direction. So the impulse is integrated exactly, a burn starts and a throttle change takes effect at
    /// the previous sample, a turn takes effect at this sample.
    /// </para>
    /// </summary>
    public static class ThrustSegmentRecorder
    {
        /// <summary>The recorder without attitude slices (offline flight and older callers): Start/End stay zero except their times.</summary>
        public static void Update(ref RecorderState r, in RecorderSample sample, in RecorderConfig cfg)
        {
            AttitudeSample att = default;
            att.Time = sample.Time;
            Update(ref r, sample, att, cfg);
        }

        public static void Update(ref RecorderState r, in RecorderSample sample, in AttitudeSample attitude, in RecorderConfig cfg)
        {
            bool thrusting = !sample.Resting && sample.Throttle >= cfg.MinThrottle;
            double dt = r.HasPrev ? sample.Time - r.PrevTime : 0.0;
            if (!r.Active)
            {
                if (thrusting)
                {
                    r.Active = true;
                    if (r.HasPrev && dt > 0)
                    {
                        OpenAt(ref r, r.PrevTime, sample.Throttle, r.PrevDirection, r.PrevSas, r.PrevAttitude);
                        Accumulate(ref r, sample.Throttle, r.PrevDirection, dt);
                    }
                    else OpenAt(ref r, sample.Time, sample.Throttle, sample.ThrustDirection, sample.Sas, attitude);
                }
                Remember(ref r, sample, attitude);
                return;
            }
            if (!thrusting)
            {
                // the step that ended now did not thrust: the burn ended at the previous sample
                CloseAt(ref r, r.PrevTime, r.PrevAttitude);
                r.Active = false;
                Remember(ref r, sample, attitude);
                return;
            }
            if (r.PrevTime - r.Open.StartTime >= cfg.MinDuration && Math.Abs(sample.Throttle - r.Open.Throttle) > cfg.ThrottleDelta)
            {
                // the new throttle burned in the step that started at the previous sample
                CloseAt(ref r, r.PrevTime, r.PrevAttitude);
                OpenAt(ref r, r.PrevTime, sample.Throttle, r.PrevDirection, r.PrevSas, r.PrevAttitude);
                if (dt > 0) Accumulate(ref r, sample.Throttle, r.PrevDirection, dt);
                Remember(ref r, sample, attitude);
                return;
            }
            if (dt > 0) Accumulate(ref r, sample.Throttle, r.PrevDirection, dt);
            double duration = sample.Time - r.Open.StartTime;
            bool evaluate = duration >= cfg.MinDuration;
            if (duration >= cfg.MaxDuration || (evaluate && sample.Sas != r.Open.Sas) ||
                (evaluate && Vec3.Angle(sample.ThrustDirection, r.Open.Direction) > cfg.AngleRadians))
            {
                // the turn (or the SAS switch) acts from the next step on
                CloseAt(ref r, sample.Time, attitude);
                OpenAt(ref r, sample.Time, sample.Throttle, sample.ThrustDirection, sample.Sas, attitude);
            }
            Remember(ref r, sample, attitude);
        }

        /// <summary>
        /// Closes the open segment at the last sample now (it is queued like any other) and, while the burn goes on, opens the next one at
        /// the same moment, so nothing is lost and the seam is exact. For a sender that must not wait for the thresholds.
        /// </summary>
        public static void Flush(ref RecorderState r)
        {
            if (!r.Active || !r.HasPrev) return;
            CloseAt(ref r, r.PrevTime, r.PrevAttitude);
            OpenAt(ref r, r.PrevTime, r.PrevThrottle, r.PrevDirection, r.PrevSas, r.PrevAttitude);
        }

        /// <summary>
        /// Forgets the queued segments, the open one and the previous sample (after a hard correction the recorded history is no longer
        /// the craft's). A burn that goes on starts a new segment at the next sample. The loss counters stay.
        /// </summary>
        public static void Clear(ref RecorderState r)
        {
            r.Active = false;
            r.Count = 0;
            r.Head = 0;
            r.ImpulseThrottle = 0;
            r.ImpulseDirection = Vec3.Zero;
            r.HasPrev = false;
            r.Open = default;
        }

        private static void OpenAt(ref RecorderState r, double time, double throttle, Vec3 direction, int sas, in AttitudeSample start)
        {
            r.Open = new ThrustSegment { StartTime = time, Throttle = throttle, Direction = direction, Sas = sas, Start = start };
            r.ImpulseThrottle = 0;
            r.ImpulseDirection = Vec3.Zero;
        }

        private static void Accumulate(ref RecorderState r, double throttle, Vec3 direction, double dt)
        {
            r.ImpulseThrottle += throttle * dt;
            r.ImpulseDirection = r.ImpulseDirection + direction * (throttle * dt);
        }

        private static void Remember(ref RecorderState r, in RecorderSample sample, in AttitudeSample attitude)
        {
            r.HasPrev = true;
            r.PrevTime = sample.Time;
            r.PrevThrottle = sample.Throttle;
            r.PrevDirection = sample.ThrustDirection;
            r.PrevSas = sample.Sas;
            r.PrevAttitude = attitude;
            r.LastTime = sample.Time;
        }

        private static void CloseAt(ref RecorderState r, double end, in AttitudeSample endAttitude)
        {
            double duration = end - r.Open.StartTime;
            if (duration < 0.1) // too short to matter for the server step
            {
                r.DroppedImpulse += r.ImpulseThrottle;
                return;
            }
            ThrustSegment seg = r.Open;
            seg.Duration = duration;
            seg.EndTime = end;
            seg.End = endAttitude;
            seg.Throttle = r.ImpulseThrottle / duration;
            double len = Vec3.Length(r.ImpulseDirection);
            if (len > 0) seg.Direction = r.ImpulseDirection / len;
            if (r.Count >= RecorderState.Capacity)
            {
                r.Overflowed++;
                return;
            }
            int slot = (r.Head + r.Count) % RecorderState.Capacity;
            Set(ref r, slot, seg);
            r.Count++;
        }

        /// <summary>The next closed segment, if the send interval has passed. The caller sends it and the recorder forgets it.</summary>
        public static bool TryDequeue(ref RecorderState r, double now, in RecorderConfig cfg, out ThrustSegment segment)
        {
            segment = default;
            if (r.Count == 0) return false;
            if (r.LastSendTime > 0 && now - r.LastSendTime < cfg.MinSendInterval) return false;
            segment = Get(in r, r.Head);
            r.Head = (r.Head + 1) % RecorderState.Capacity;
            r.Count--;
            r.LastSendTime = now > 0 ? now : 1e-12;
            return true;
        }

        private static ThrustSegment Get(in RecorderState r, int i)
        {
            switch (i)
            {
                case 0: return r.Q0;
                case 1: return r.Q1;
                case 2: return r.Q2;
                case 3: return r.Q3;
                case 4: return r.Q4;
                case 5: return r.Q5;
                case 6: return r.Q6;
                default: return r.Q7;
            }
        }

        private static void Set(ref RecorderState r, int i, ThrustSegment s)
        {
            switch (i)
            {
                case 0: r.Q0 = s; break;
                case 1: r.Q1 = s; break;
                case 2: r.Q2 = s; break;
                case 3: r.Q3 = s; break;
                case 4: r.Q4 = s; break;
                case 5: r.Q5 = s; break;
                case 6: r.Q6 = s; break;
                default: r.Q7 = s; break;
            }
        }
    }
}
