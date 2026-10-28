using Leopotam.Ecs;
using Orbital.Core.Flight;
using Orbital.Technology.Components;

namespace Orbital.Technology.Systems
{
    /// <summary>
    /// Feeds the thrust-segment recorder after every step and takes out the segments that are due, as the network sender will do in stage 2c.
    /// Here they are only counted, which gives the number of segments per minute for manual tests (the load on the synchronization).
    /// </summary>
    public class ThrustRecorderSystem : IEcsRunSystem
    {
        private FlightClock _clock;
        private EcsFilter<FlightStateComponent, ThrustRecorderComponent> _filter;

        public void Run()
        {
            double now = _clock.Time + _clock.Dt; // the state is already advanced by one step
            foreach (int i in _filter)
            {
                ref FlightStateComponent state = ref _filter.Get1(i);
                ref ThrustRecorderComponent recorder = ref _filter.Get2(i);
                RecorderSample sample;
                sample.Time = now;
                sample.Throttle = state.State.Throttle;
                sample.ThrustDirection = DQuat.Rotate(state.State.Orientation, Vec3.UnitY);
                sample.Sas = state.State.SasOn;
                sample.Resting = state.State.Mode != FlightMode.Flying;
                ThrustSegmentRecorder.Update(ref recorder.State, sample, _clock.Recorder);
                while (ThrustSegmentRecorder.TryDequeue(ref recorder.State, now, _clock.Recorder, out ThrustSegment segment))
                {
                    recorder.SentCount++;
                    recorder.SentDuration += segment.Duration;
                    recorder.LastSent = segment;
                }
            }
        }
    }
}
