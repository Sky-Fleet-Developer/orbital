using System;
using Orbital.Core.Flight;

namespace Orbital.Core.Net
{
    /// <summary>
    /// Estimate of the server game time from ping/pong pairs (stage 2c.4b), Cristian's algorithm over the last <see cref="Window"/> samples.
    /// The server time is a line in the local real time: T(L) = G0 + scale * L. A pong sent at local real time S and received at R with
    /// server time Ts says G0 lies in [Ts - scale*R, Ts - scale*S] (the server read its clock somewhere between). The intersection of
    /// these intervals is the estimate (its middle); a late packet only widens its own interval, so the jitter of the slowest legs does
    /// not matter, only the fastest up-leg and the fastest down-leg. An interval that misses the intersection means the time base changed
    /// (server restart, freeze, drift): the older samples are dropped and the newest is trusted.
    /// Local real time is any monotonic clock in seconds (the caller's choice, e.g. a stopwatch), never the game time.
    /// </summary>
    public sealed class ClockSync
    {
        public const int Window = 8;

        private struct Sample
        {
            public double Lo, Hi;   // interval of G0
        }

        private readonly Sample[] _samples = new Sample[Window];
        private int _count;
        private int _next;
        private double _scale = 1.0;

        public int Count => _count;
        public bool HasEstimate => _count > 0;
        /// <summary>Server time scale (game seconds per real second) as last reported.</summary>
        public double Scale => _scale;
        /// <summary>Samples dropped because the time base changed (statistics).</summary>
        public int Resets { get; private set; }

        public void Reset()
        {
            _count = 0;
            _next = 0;
        }

        /// <summary>A ping sent at local real time <paramref name="sendReal"/>, its pong received at <paramref name="recvReal"/> with the server time and scale.</summary>
        public void AddSample(double sendReal, double recvReal, double serverTime, double serverScale)
        {
            if (!(recvReal >= sendReal) || double.IsNaN(serverTime) || double.IsInfinity(serverTime)) return;
            if (serverScale > 0 && serverScale != _scale)
            {
                // the pong reports a scale the client did not hear about (a lost push): re-anchor at the pong's time
                if (_count > 0) OnScaleChange(serverTime, serverScale);
                else _scale = serverScale;
            }
            Sample s = new Sample { Lo = serverTime - _scale * recvReal, Hi = serverTime - _scale * sendReal };
            if (_count > 0)
            {
                Intersect(out double lo, out double hi);
                if (s.Hi < lo || s.Lo > hi)
                {
                    Reset();
                    Resets++;
                }
            }
            _samples[_next] = s;
            _next = (_next + 1) % Window;
            if (_count < Window) _count++;
        }

        /// <summary>
        /// The server changed the scale at game time <paramref name="gameTime"/> (push time_scale, or admin_result). Each stored interval is
        /// carried over to the new line, which meets the old one at that moment: G0' = Tc + (s'/s) * (G0 - Tc).
        /// </summary>
        public void OnScaleChange(double gameTime, double newScale)
        {
            if (!(newScale > 0) || newScale == _scale) return;
            double k = newScale / _scale;
            for (int i = 0; i < _count; i++)
            {
                _samples[i].Lo = gameTime + k * (_samples[i].Lo - gameTime);
                _samples[i].Hi = gameTime + k * (_samples[i].Hi - gameTime);
            }
            _scale = newScale;
        }

        /// <summary>Server game time at local real time <paramref name="localReal"/>; NaN without samples.</summary>
        public double ServerTime(double localReal)
        {
            if (_count == 0) return double.NaN;
            Intersect(out double lo, out double hi);
            return 0.5 * (lo + hi) + _scale * localReal;
        }

        /// <summary>Half the width of the intersection, in game seconds: the estimate is within this of the truth (clocks without drift).</summary>
        public double Uncertainty
        {
            get
            {
                if (_count == 0) return double.PositiveInfinity;
                Intersect(out double lo, out double hi);
                return 0.5 * (hi - lo);
            }
        }

        private void Intersect(out double lo, out double hi)
        {
            lo = double.NegativeInfinity;
            hi = double.PositiveInfinity;
            for (int i = 0; i < _count; i++)
            {
                if (_samples[i].Lo > lo) lo = _samples[i].Lo;
                if (_samples[i].Hi < hi) hi = _samples[i].Hi;
            }
        }
    }

    public struct DisciplineConfig
    {
        /// <summary>Rate correction per second of error (real seconds): 0.1 removes an error with a time constant of about 10 s.</summary>
        public double Gain;
        /// <summary>The rate never differs from the server scale by more than this fraction (0.05).</summary>
        public double MaxRate;
        /// <summary>Errors below this (real seconds) are left alone, so the rate does not chatter.</summary>
        public double Deadband;
        /// <summary>Above this error (real seconds times max(1, scale), in game seconds) the clock jumps instead of slewing.</summary>
        public double JumpReal;

        public static DisciplineConfig Default() => new DisciplineConfig { Gain = 0.1, MaxRate = 0.05, Deadband = 0.002, JumpReal = 0.5 };
    }

    public struct DisciplineResult
    {
        /// <summary>Target minus client time before the correction, game seconds.</summary>
        public double Error;
        public bool Jumped;
        /// <summary>Steps added by a jump (negative = back).</summary>
        public long JumpSteps;
        /// <summary>Relative rate correction now in force: the clock runs at scale * (1 + Correction).</summary>
        public double Correction;
    }

    /// <summary>
    /// Keeps the client <see cref="GameClock"/> on the server time (stage 2c.4b, plan section 6): the step length never changes, only the
    /// accumulator rate does (scale * (1 + clamp(gain * error, ±maxRate))), so the craft keeps being stepped exactly like the server
    /// would step it. A large error makes the clock jump by whole steps; the caller then resynchronises the craft (the steps between were
    /// not simulated). The online clock should allow scales down to minScale * (1 - maxRate), or slowing down is clamped at the bottom.
    /// </summary>
    public static class ClockDiscipline
    {
        /// <summary>The client time including the fraction of a step accumulated but not yet made.</summary>
        public static double ContinuousTime(in GameClock c) => c.Time + (double) c.Accumulator / GameClock.Unit * c.Step;

        public static DisciplineResult Apply(ref GameClock c, double targetTime, double serverScale, in DisciplineConfig cfg)
        {
            DisciplineResult r = default;
            if (!(serverScale > 0)) serverScale = c.Scale;
            r.Error = targetTime - ContinuousTime(c);
            if (Math.Abs(r.Error) > cfg.JumpReal * Math.Max(1.0, serverScale))
            {
                long steps = (long) Math.Round((targetTime - c.StartTime) / c.Step);
                r.Jumped = true;
                r.JumpSteps = steps - c.Steps;
                c.Steps = steps;
                c.Accumulator = 0;
                GameClock.SetScale(ref c, serverScale);
                return r;
            }
            double errorReal = r.Error / serverScale;
            double corr = Math.Abs(errorReal) < cfg.Deadband ? 0.0 : Math.Max(-cfg.MaxRate, Math.Min(cfg.MaxRate, cfg.Gain * errorReal));
            r.Correction = corr;
            GameClock.SetScale(ref c, serverScale * (1.0 + corr));
            return r;
        }
    }
}
