using Leopotam.Ecs;
using Orbital.Core.Flight;
using Orbital.Technology.Components;

namespace Orbital.Technology.Systems
{
    /// <summary>
    /// Stage 3.2: the sphere of influence rule after the kernel step (<see cref="SoiRule.Step"/>): a craft in flight that left its body's
    /// sphere or entered a child's moves into the new body's frame and takes its gravity and ground. Does nothing without
    /// <see cref="FlightClock.Frames"/>.
    /// </summary>
    public class SoiSystem : IEcsRunSystem
    {
        private FlightClock _clock;
        private EcsFilter<FlightStateComponent, FlightParamsComponent, SoiComponent> _filter;

        public void Run()
        {
            BodyFrameTable frames = _clock.Frames;
            double now = _clock.Time + _clock.Dt; // the state is already advanced by one step
            foreach (int i in _filter)
            {
                ref SoiComponent soi = ref _filter.Get3(i);
                soi.ChangedThisStep = SoiRule.Step(frames, ref soi.Parent, ref _filter.Get1(i).State, ref _filter.Get2(i).Params, now);
                if (soi.ChangedThisStep) soi.Changes++;
            }
        }
    }
}
