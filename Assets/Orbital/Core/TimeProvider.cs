using Orbital.Core.Flight;
using Orbital.Core.Handles;
using Sirenix.OdinInspector;
using UnityEngine;
using Zenject;

namespace Orbital.Core
{
    /// <summary>
    /// The one owner of the game time of the client (planets, orbits, map and the craft read it). Since stage 2b.2 the time is a
    /// <see cref="GameClock"/>: a fixed step (the engine fixed delta time at the start, 0.02 s by default) and a whole number of steps, so the
    /// world time and the craft never drift apart. Every fixed tick adds <see cref="TimeScale"/> steps through the accumulator of the clock:
    /// scale 1 is one step per tick, 10 is ten steps per tick (capped by <see cref="MaxStepsPerTick"/>), 0.25 is one step every fourth tick.
    /// The time scale is a test tool of the offline flight (there is no player time acceleration); with the server link it becomes a server-side
    /// admin setting (Docs/GameplayPrototype.md, stage 2b.2).
    /// </summary>
    public class TimeProvider : MonoBehaviour, IFixedUpdateHandler, IBindMe
    {
        private static GameClock _clock;
        private static bool _hasClock;

        [Inject]
        private void Inject(DiContainer container)
        {
            EnsureClock();
            GameClock.SetScale(ref _clock, 1.0); // every scene starts in real time
            HandlerCollection.GetOrCreateCollection<IFixedUpdateHandler>(container).AddItem(this);
        }

        int IOrderHolder.Order => -1;
        void IFixedUpdateHandler.FixedUpdate()
        {
            EnsureClock();
            GameClock.Tick(ref _clock);
        }

        private static void EnsureClock()
        {
            if (_hasClock) return;
            _clock = GameClock.Create(0.0, Time.fixedDeltaTime);
            _hasClock = true;
        }

        /// <summary>Game time after the last step, s.</summary>
        [ShowInInspector] public static double WorldTime => _hasClock ? _clock.Time : 0.0;

        /// <summary>The set time scale (1 = real time).</summary>
        [ShowInInspector] public static double TimeScale => _hasClock ? _clock.Scale : 1.0;

        /// <summary>Game time per engine frame at the set scale (legacy callers; the craft uses the fixed steps).</summary>
        public static double DeltaTime => Time.deltaTime * TimeScale;

        /// <summary>The fixed step of the game time, s.</summary>
        public static double StepSeconds
        {
            get
            {
                EnsureClock();
                return _clock.Step;
            }
        }

        /// <summary>Steps made since the start; <see cref="WorldTime"/> is the time after the last of them.</summary>
        [ShowInInspector] public static long StepCount => _hasClock ? _clock.Steps : 0;

        /// <summary>Game time of the start of the step with the given index.</summary>
        public static double TimeOfStep(long index)
        {
            EnsureClock();
            return _clock.TimeOfStep(index);
        }

        public static double MinTimeScale => _hasClock ? _clock.MinScale : GameClock.DefaultMinScale;

        public static double MaxTimeScale => _hasClock ? _clock.MaxScale : GameClock.DefaultMaxScale;

        public static int MaxStepsPerTick => _hasClock ? _clock.MaxStepsPerTick : GameClock.DefaultMaxStepsPerTick;

        /// <summary>The scale the cap allows: the smaller of the set scale and the cap (steps per tick).</summary>
        public static double EffectiveTimeScale => _hasClock ? _clock.EffectiveScale : 1.0;

        /// <summary>The cap cut the last tick: the real scale is lower than the set one.</summary>
        public static bool TimeScaleLimited => _hasClock && _clock.LastTickLimited;

        /// <summary>Test tool: sets the time scale (clamped to [MinTimeScale, MaxTimeScale]). Offline only; see the class notes.</summary>
        public static void SetTimeScale(double scale)
        {
            EnsureClock();
            GameClock.SetScale(ref _clock, scale);
        }

        /// <summary>Test tool: the most steps one fixed tick may make (at least 1).</summary>
        public static void SetMaxStepsPerTick(int steps)
        {
            EnsureClock();
            _clock.MaxStepsPerTick = Mathf.Max(1, steps);
        }

        /// <summary>Slider position 0..1 (log scale) -> time scale.</summary>
        public static double ScaleFromSlider(double position)
        {
            EnsureClock();
            return GameClock.ScaleFromSlider(_clock, position);
        }

        /// <summary>Time scale -> slider position 0..1 (log scale).</summary>
        public static double SliderFromScale(double scale)
        {
            EnsureClock();
            return GameClock.SliderFromScale(_clock, scale);
        }
    }
}
