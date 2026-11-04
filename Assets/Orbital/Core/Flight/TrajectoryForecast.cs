using System;
using System.Threading.Tasks;
using Ara3D;
using Orbital.Core.TrajectorySystem;

namespace Orbital.Core.Flight
{
    /// <summary>What ends one conic of a forecast.</summary>
    public enum TrajectoryEnd
    {
        None,
        /// <summary>A closed orbit that meets nothing in one revolution.</summary>
        Closed,
        /// <summary>The craft enters the sphere of a child body.</summary>
        EnterBody,
        /// <summary>The craft leaves for the parent body.</summary>
        LeaveBody,
        /// <summary>The conic reaches the ground.</summary>
        Impact,
        /// <summary>An open orbit around the root: the search horizon, nothing happens.</summary>
        OpenEnd,
    }

    /// <summary>One conic of a forecast around one body, from <see cref="Start"/> to <see cref="End"/>.</summary>
    public struct TrajectorySegment
    {
        public int Body;
        public StaticOrbit Orbit;
        public double Start;
        public double End;
        public TrajectoryEnd EndKind;
        /// <summary>The body after the end, -1 when the end changes no body.</summary>
        public int NextBody;
    }

    public enum TrajectoryEventKind { Pericenter, Apocenter, EnterBody, LeaveBody, Impact }

    /// <summary>A point of a forecast worth a marker on the map.</summary>
    public struct TrajectoryEvent
    {
        public TrajectoryEventKind Kind;
        public int Segment;
        public int Body;
        public double Time;
        /// <summary>Above the ground of the segment's body, meters (the sphere of influence events too).</summary>
        public double Altitude;
        /// <summary>The position in the frame of the plot.</summary>
        public Vec3 Relative;
        /// <summary>For an entry drawn in a frame that holds the body: where the body will be then.</summary>
        public bool HasBodyGhost;
        public Vec3 BodyGhost;
    }

    /// <summary>
    /// A forecast ready to draw: the conics, a time and a true anomaly per sample, every sample in meters relative to <see cref="FrameBody"/>. A segment in
    /// the frame body's subtree is the true path relative to that body over time (so around the root everything is exact and continuous). Any other segment,
    /// a body the frame body orbits or a neighbour, would be a meaningless curl in that frame: it keeps its own shape and is moved to continue the previous one.
    /// </summary>
    public sealed class TrajectoryPlot
    {
        public const int MaxSegments = 6;
        public const int SamplesPerSegment = 256;
        public const int MaxEvents = MaxSegments * 3;

        public int Count;
        public int FrameBody = -1;
        public int Parent = -1;
        public double Time;
        public readonly TrajectorySegment[] Segments = new TrajectorySegment[MaxSegments];
        public readonly double[][] Times = new double[MaxSegments][];
        public readonly double[][] Anomalies = new double[MaxSegments][];
        public readonly Vec3[][] Relative = new Vec3[MaxSegments][];
        public readonly TrajectoryEvent[] Events = new TrajectoryEvent[MaxEvents];
        public int EventCount;

        public TrajectoryPlot()
        {
            for (int i = 0; i < MaxSegments; i++)
            {
                Times[i] = new double[SamplesPerSegment];
                Anomalies[i] = new double[SamplesPerSegment];
                Relative[i] = new Vec3[SamplesPerSegment];
            }
        }
    }

    /// <summary>
    /// The patched conics forecast of a coasting craft (stage 4): the conic around the current body, then the next ones through every change of the sphere
    /// of influence, by the rule of <see cref="SoiForecast"/>. The thrust is not taken into account. Slow (the entry search walks the conic): run it off the
    /// main thread through <see cref="TrajectoryForecaster"/>. Everything it reads is immutable, so that is safe.
    /// </summary>
    public static class TrajectoryForecast
    {
        /// <summary>Fills <paramref name="output"/> from the state; returns the count, 0 for a state without a usable conic.</summary>
        public static int Build(BodyFrameTable table, int parent, Vec3 position, Vec3 velocity, double time, TrajectorySegment[] output)
        {
            int count = 0;
            int body = parent;
            while (count < output.Length)
            {
                TrajectoryEnd end = SoiForecast.NextEvent(table, body, position, velocity, time, out StaticOrbit orbit, out double when, out int next);
                if (end == TrajectoryEnd.None) break;
                output[count++] = new TrajectorySegment {Body = body, Orbit = orbit, Start = time, End = when, EndKind = end, NextBody = next};
                if (next < 0) break;
                (DVector3 p, DVector3 v) = orbit.GetSample(when);
                position = new Vec3(p.x, p.y, p.z);
                velocity = new Vec3(v.x, v.y, v.z);
                table.Convert(body, next, when, ref position, ref velocity);
                body = next;
                time = when;
            }
            return count;
        }

        /// <summary>Samples of a conic, uniform in the true anomaly: dense where the craft is fast and the curve is sharp. The times grow, the ends are exact.</summary>
        public static void Sample(in TrajectorySegment s, double[] times, double[] anomalies)
        {
            int n = times.Length;
            StaticOrbit orbit = s.Orbit;
            double from = orbit.TrueAnomalyAtT(s.Start);
            double to = from + 2.0 * Math.PI;
            if (s.EndKind != TrajectoryEnd.Closed)
            {
                to = orbit.TrueAnomalyAtT(s.End);
                while (to < from) to += 2.0 * Math.PI;
            }
            double period = orbit.Eccentricity < 1.0 ? orbit.Period : double.PositiveInfinity;
            for (int i = 0; i < n; i++)
            {
                anomalies[i] = from + (to - from) * i / (n - 1);
                if (i == 0)
                {
                    times[i] = s.Start;
                    continue;
                }
                double t = orbit.TimeOfTrueAnomaly(anomalies[i], times[i - 1]);
                // a rounding step just behind the previous time wraps a whole revolution ahead in TimeOfTrueAnomaly
                if (double.IsNaN(t) || t < times[i - 1] || t - times[i - 1] > 0.5 * period) t = times[i - 1];
                times[i] = t;
            }
            times[n - 1] = s.End;
        }

        /// <summary>Is <paramref name="body"/> the frame body or below it in the tree?</summary>
        public static bool InSubtree(BodyFrameTable table, int body, int frame)
        {
            for (int b = body; b >= 0; b = table[b].Parent)
            {
                if (b == frame) return true;
            }
            return false;
        }

        /// <summary>The offset of a body from the frame body at time t. The sum up the tree inside the frame's subtree; other pairs go through the root.</summary>
        public static Vec3 BodyOffset(BodyFrameTable table, int body, int frame, double t)
        {
            Vec3 sum = Vec3.Zero;
            int b = body;
            for (; b >= 0 && b != frame; b = table[b].Parent)
            {
                table.LocalState(b, t, out Vec3 p, out _);
                sum = sum + p;
            }
            if (b == frame) return sum;
            table.GlobalState(body, t, out Vec3 a, out _);
            table.GlobalState(frame, t, out Vec3 c, out _);
            return a - c;
        }

        /// <summary>The whole plot of a state in the frame of <paramref name="frameBody"/>.</summary>
        public static void Plot(BodyFrameTable table, int parent, Vec3 position, Vec3 velocity, double time, int frameBody, TrajectoryPlot plot)
        {
            plot.Parent = parent;
            plot.FrameBody = frameBody;
            plot.Time = time;
            plot.EventCount = 0;
            plot.Count = Build(table, parent, position, velocity, time, plot.Segments);
            Vec3 previousEnd = Vec3.Zero;
            for (int i = 0; i < plot.Count; i++)
            {
                ref TrajectorySegment s = ref plot.Segments[i];
                Sample(s, plot.Times[i], plot.Anomalies[i]);
                bool inFrame = InSubtree(table, s.Body, frameBody);
                Vec3 shift = Vec3.Zero;
                if (!inFrame)
                {
                    Vec3 start = LocalAt(s.Orbit, plot.Anomalies[i][0]);
                    // the first segment stands where it really is now; a later one continues the line
                    Vec3 at = i == 0 ? BodyOffset(table, s.Body, frameBody, s.Start) + start : previousEnd;
                    shift = at - start;
                }
                Vec3[] relative = plot.Relative[i];
                for (int k = 0; k < relative.Length; k++)
                {
                    relative[k] = RelativeAt(table, s, inFrame, shift, frameBody, plot.Times[i][k], plot.Anomalies[i][k]);
                }
                previousEnd = relative[relative.Length - 1];
                AddEvents(table, plot, i, inFrame, shift, frameBody);
            }
        }

        private static Vec3 RelativeAt(BodyFrameTable table, in TrajectorySegment s, bool inFrame, Vec3 shift, int frame, double t, double anomaly)
        {
            Vec3 local = LocalAt(s.Orbit, anomaly);
            return inFrame ? BodyOffset(table, s.Body, frame, t) + local : local + shift;
        }

        private static Vec3 LocalAt(in StaticOrbit orbit, double anomaly)
        {
            DVector3 p = orbit.GetPositionFromTrueAnomaly(anomaly);
            return new Vec3(p.x, p.y, p.z);
        }

        private static void AddEvents(BodyFrameTable table, TrajectoryPlot plot, int index, bool inFrame, Vec3 shift, int frame)
        {
            TrajectorySegment s = plot.Segments[index];
            double[] anomalies = plot.Anomalies[index];
            double from = anomalies[0], to = anomalies[anomalies.Length - 1];
            StaticOrbit orbit = s.Orbit;
            if (TryAnomalyInRange(0.0, from, to, out double pe))
                AddEvent(table, plot, s, index, inFrame, shift, frame, TrajectoryEventKind.Pericenter, pe, orbit.TimeOfTrueAnomaly(pe, s.Start), s.Body);
            if (orbit.Eccentricity < 1.0 && TryAnomalyInRange(Math.PI, from, to, out double ap))
                AddEvent(table, plot, s, index, inFrame, shift, frame, TrajectoryEventKind.Apocenter, ap, orbit.TimeOfTrueAnomaly(ap, s.Start), s.Body);
            if (s.EndKind == TrajectoryEnd.EnterBody) AddEvent(table, plot, s, index, inFrame, shift, frame, TrajectoryEventKind.EnterBody, to, s.End, s.NextBody);
            else if (s.EndKind == TrajectoryEnd.LeaveBody) AddEvent(table, plot, s, index, inFrame, shift, frame, TrajectoryEventKind.LeaveBody, to, s.End, s.NextBody);
            else if (s.EndKind == TrajectoryEnd.Impact) AddEvent(table, plot, s, index, inFrame, shift, frame, TrajectoryEventKind.Impact, to, s.End, s.Body);
        }

        private static void AddEvent(BodyFrameTable table, TrajectoryPlot plot, in TrajectorySegment s, int index, bool inFrame, Vec3 shift, int frame,
            TrajectoryEventKind kind, double anomaly, double time, int body)
        {
            if (plot.EventCount >= plot.Events.Length) return;
            TrajectoryEvent e = new TrajectoryEvent
            {
                Kind = kind, Segment = index, Body = body, Time = time,
                Relative = RelativeAt(table, s, inFrame, shift, frame, time, anomaly),
                Altitude = s.Orbit.RadiusAtTrueAnomaly(anomaly) - table[s.Body].Radius,
            };
            if (kind == TrajectoryEventKind.EnterBody && InSubtree(table, body, frame))
            {
                e.HasBodyGhost = true;
                e.BodyGhost = BodyOffset(table, body, frame, time);
            }
            plot.Events[plot.EventCount++] = e;
        }

        // the anomaly plus a whole number of turns that falls inside [from, to]
        private static bool TryAnomalyInRange(double anomaly, double from, double to, out double inRange)
        {
            const double Turn = 2.0 * Math.PI;
            inRange = anomaly + Math.Ceiling((from - anomaly) / Turn) * Turn;
            return inRange <= to;
        }
    }

    /// <summary>
    /// Runs <see cref="TrajectoryForecast.Plot"/> on a worker thread, one at a time. The main thread reads <see cref="Latest"/>, which the worker never
    /// touches: two plots trade places when a run is finished, in <see cref="Poll"/>. A view helper: the result never goes into the simulation.
    /// </summary>
    public sealed class TrajectoryForecaster
    {
        private readonly BodyFrameTable _table;
        private TrajectoryPlot _front = new TrajectoryPlot();
        private TrajectoryPlot _back = new TrajectoryPlot();
        private Task _running;

        public TrajectoryForecaster(BodyFrameTable table)
        {
            _table = table;
        }

        public TrajectoryPlot Latest => _front;
        /// <summary>The failure of the last finished run, null when it went well. Read it for the log.</summary>
        public Exception LastError { get; private set; }

        /// <summary>Publishes a finished run. True when <see cref="Latest"/> has changed.</summary>
        public bool Poll()
        {
            if (_running == null || !_running.IsCompleted) return false;
            bool failed = _running.IsFaulted;
            LastError = failed ? _running.Exception : null;
            _running = null;
            if (failed) return false;
            TrajectoryPlot done = _back;
            _back = _front;
            _front = done;
            return true;
        }

        /// <summary>Starts a run unless one is going. The state is copied.</summary>
        public bool Request(int parent, Vec3 position, Vec3 velocity, double time, int frameBody)
        {
            if (_running != null) return false;
            TrajectoryPlot plot = _back;
            BodyFrameTable table = _table;
            _running = Task.Run(() => TrajectoryForecast.Plot(table, parent, position, velocity, time, frameBody, plot));
            return true;
        }

        /// <summary>No trajectory to show (on the ground): an empty plot. A run in progress is thrown away when it ends.</summary>
        public void Clear()
        {
            if (_running != null)
            {
                _running.Wait(TimeSpan.FromSeconds(1));
                if (!_running.IsCompleted) return;
                _running = null;
            }
            _front.Count = 0;
            _front.EventCount = 0;
        }
    }
}
