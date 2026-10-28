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
    }

    /// <summary>One thrust segment in the shape the server takes: direction in the parent inertial frame, throttle, start, duration.</summary>
    public struct ThrustSegment
    {
        public double StartTime;
        public double Duration;
        public double Throttle;
        public Vec3 Direction;
        /// <summary>1 when the stability assist was on at the start of the segment (the server stores it with the attitude).</summary>
        public int Sas;
    }

    /// <summary>One sample of the flight for the recorder.</summary>
    public struct RecorderSample
    {
        public double Time;
        public double Throttle;
        public Vec3 ThrustDirection;   // unit vector, body +Y in the inertial frame
        public int Sas;                // 1 while the stability assist is on; a change closes the segment
        /// <summary>True while the craft stands on the ground and thrust does not exceed weight: nothing to record.</summary>
        public bool Resting;
    }

    /// <summary>State of the recorder. Fixed size (a small ring of closed segments), no allocations.</summary>
    public struct RecorderState
    {
        public const int Capacity = 8;
        public bool Active;
        public ThrustSegment Open;        // direction/throttle at the start of the open segment, duration grows
        public double LastTime;
        public double LastSendTime;
        public int Count;                 // closed segments waiting to be sent
        public int Head;
        public ThrustSegment Q0, Q1, Q2, Q3, Q4, Q5, Q6, Q7;
        public int Overflowed;            // closed segments lost because the ring was full (the sender fell behind)
        public double DroppedImpulse;     // throttle*seconds of tails shorter than the minimum that were not sent
    }

    /// <summary>
    /// Turns the stream of control states into a few thrust segments. A segment is closed when the throttle or the direction moves
    /// beyond the thresholds, when it gets too long, or when the thrust stops. Changes are evaluated only after MinDuration, so a jittery
    /// throttle cannot produce a flood of tiny segments. On the ground, while thrust is below weight, nothing is recorded.
    /// </summary>
    public static class ThrustSegmentRecorder
    {
        public static void Update(ref RecorderState r, in RecorderSample sample, in RecorderConfig cfg)
        {
            bool thrusting = !sample.Resting && sample.Throttle >= cfg.MinThrottle;
            if (!r.Active)
            {
                if (thrusting)
                {
                    r.Active = true;
                    r.Open = new ThrustSegment { StartTime = sample.Time, Duration = 0, Throttle = sample.Throttle, Direction = sample.ThrustDirection, Sas = sample.Sas };
                    r.LastTime = sample.Time;
                }
                return;
            }
            double duration = sample.Time - r.Open.StartTime;
            if (!thrusting)
            {
                Close(ref r, duration, cfg);
                r.Active = false;
                return;
            }
            bool evaluate = duration >= cfg.MinDuration;
            if (duration >= cfg.MaxDuration || (evaluate && sample.Sas != r.Open.Sas) ||
                (evaluate && (Math.Abs(sample.Throttle - r.Open.Throttle) > cfg.ThrottleDelta ||
                              Vec3.Angle(sample.ThrustDirection, r.Open.Direction) > cfg.AngleRadians)))
            {
                Close(ref r, duration, cfg);
                r.Open = new ThrustSegment { StartTime = sample.Time, Duration = 0, Throttle = sample.Throttle, Direction = sample.ThrustDirection, Sas = sample.Sas };
            }
            r.LastTime = sample.Time;
        }

        private static void Close(ref RecorderState r, double duration, in RecorderConfig cfg)
        {
            if (duration < 0.1) // too short to matter for the server step
            {
                r.DroppedImpulse += r.Open.Throttle * (duration > 0 ? duration : 0);
                return;
            }
            ThrustSegment seg = r.Open;
            seg.Duration = duration;
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
