using System;

namespace Orbital.Core.Flight
{
    /// <summary>
    /// Scaled game time made of fixed steps (stage 2b.2). The step length never changes, so a craft is stepped exactly like the server would step it
    /// at any time scale: a faster scale means more steps per engine tick, a slower one means ticks without a step. Game time is the start time plus
    /// the number of steps times the step length, so it does not drift with the scale or with the run length (no running sum of floats).
    /// The scale is kept as an integer number of thousandths and the accumulator counts thousandths of a step, so the step pattern is exact:
    /// scale 0.25 gives one step every fourth tick forever, scale 0.1 one step every tenth tick.
    /// Engine-free plain data and static functions (shared with the server sources, covered by Tools/OrbitMathTests).
    /// Product rule: there is no player time acceleration. The client uses this for offline tests only; with the server link (stage 2c) the scale
    /// becomes a server-side admin setting (see Docs/GameplayPrototype.md, stage 2b.2).
    /// </summary>
    public struct GameClock
    {
        /// <summary>Resolution of the scale and of the accumulator: thousandths.</summary>
        public const long Unit = 1000;
        public const double DefaultMinScale = 0.1;
        public const double DefaultMaxScale = 1000.0;
        /// <summary>
        /// Steps per tick at most; above it the real scale is lower than the set one (no backlog is kept: no spiral of death). 200 steps cost about
        /// 0.8 ms per tick in .NET 8 (3.9 us per craft step without the ecs, Stage2b2Tests.MeasureTheCostOfAStep), i.e. 200x at 50 ticks per second.
        /// </summary>
        public const int DefaultMaxStepsPerTick = 200;

        public double StartTime;      // s, game time of step 0
        public double Step;           // s, fixed step length
        public long Steps;            // steps done since StartTime
        public long Ticks;            // engine ticks seen
        public long ScaleUnits;       // scale * Unit
        public long Accumulator;      // thousandths of a step not yet done, 0 .. Unit-1 after a tick
        public int MaxStepsPerTick;
        public double MinScale;
        public double MaxScale;
        public int LastTickSteps;     // steps made by the last tick
        public long LastTickFirstStep; // index of the first step of the last tick
        public bool LastTickLimited;  // the cap cut the last tick
        public long LimitedTicks;     // ticks cut by the cap since the start

        /// <summary>Game time after the last step.</summary>
        public double Time => StartTime + Steps * Step;

        /// <summary>The set scale (after rounding to thousandths and clamping).</summary>
        public double Scale => (double) ScaleUnits / Unit;

        /// <summary>The scale really reached when the cap limits it: the smaller of the set scale and the cap.</summary>
        public double EffectiveScale => Math.Min(Scale, MaxStepsPerTick);

        /// <summary>Game time of the start of the step with the given index.</summary>
        public double TimeOfStep(long index) => StartTime + index * Step;

        public static GameClock Create(double startTime, double step, double scale = 1.0, int maxStepsPerTick = DefaultMaxStepsPerTick,
            double minScale = DefaultMinScale, double maxScale = DefaultMaxScale)
        {
            if (!(step > 0) || double.IsInfinity(step)) throw new ArgumentOutOfRangeException(nameof(step), "the step must be positive");
            GameClock c = new GameClock
            {
                StartTime = startTime,
                Step = step,
                MaxStepsPerTick = Math.Max(1, maxStepsPerTick),
                MinScale = Math.Max(1.0 / Unit, Math.Min(minScale, maxScale)),
                MaxScale = Math.Max(1.0 / Unit, Math.Max(minScale, maxScale)),
            };
            SetScale(ref c, scale);
            return c;
        }

        /// <summary>Sets the scale, clamped to [MinScale, MaxScale] and rounded to thousandths. The fraction of a step already accumulated is kept.</summary>
        public static void SetScale(ref GameClock c, double scale)
        {
            if (double.IsNaN(scale)) scale = 1.0;
            scale = Math.Max(c.MinScale, Math.Min(c.MaxScale, scale));
            c.ScaleUnits = Math.Max(1, (long) Math.Round(scale * Unit));
        }

        /// <summary>
        /// One engine tick (one fixed update): adds the scale to the accumulator and returns the number of whole steps to make now
        /// (0 in slow motion between steps). Advances <see cref="Steps"/> by that number, so <see cref="Time"/> is already the time after them;
        /// the steps of this tick start at <see cref="LastTickFirstStep"/>.
        /// </summary>
        public static int Tick(ref GameClock c)
        {
            c.Ticks++;
            c.Accumulator += c.ScaleUnits;
            long n = c.Accumulator / Unit;
            if (n > c.MaxStepsPerTick)
            {
                n = c.MaxStepsPerTick;
                c.Accumulator %= Unit; // drop the backlog, keep the fraction: the pattern stays deterministic
                c.LastTickLimited = true;
                c.LimitedTicks++;
            }
            else
            {
                c.Accumulator -= n * Unit;
                c.LastTickLimited = false;
            }
            c.LastTickFirstStep = c.Steps;
            c.LastTickSteps = (int) n;
            c.Steps += n;
            return (int) n;
        }

        /// <summary>Slider position 0..1 on a log scale between MinScale and MaxScale -> scale.</summary>
        public static double ScaleFromSlider(in GameClock c, double position)
        {
            position = Math.Max(0.0, Math.Min(1.0, position));
            return Math.Exp(Math.Log(c.MinScale) + position * (Math.Log(c.MaxScale) - Math.Log(c.MinScale)));
        }

        /// <summary>Inverse of <see cref="ScaleFromSlider"/>.</summary>
        public static double SliderFromScale(in GameClock c, double scale)
        {
            double span = Math.Log(c.MaxScale) - Math.Log(c.MinScale);
            if (!(span > 0)) return 0.0;
            scale = Math.Max(c.MinScale, Math.Min(c.MaxScale, scale));
            return (Math.Log(scale) - Math.Log(c.MinScale)) / span;
        }
    }
}
