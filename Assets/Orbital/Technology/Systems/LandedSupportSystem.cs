using Leopotam.Ecs;
using Orbital.Core.Flight;
using Orbital.Technology.Components;

namespace Orbital.Technology.Systems
{
    /// <summary>Holds a landed craft upright after the step (<see cref="LandedSupport"/>), unless <see cref="FlightClock.GroundSupport"/> is off.</summary>
    public class LandedSupportSystem : IEcsRunSystem
    {
        private FlightClock _clock;
        private EcsFilter<FlightStateComponent, FlightParamsComponent> _filter;

        public void Run()
        {
            if (!_clock.GroundSupport) return;
            foreach (int i in _filter)
            {
                ref FlightStateComponent state = ref _filter.Get1(i);
                ref FlightParamsComponent flightParams = ref _filter.Get2(i);
                LandedSupport.HoldUpright(ref state.State, flightParams.Params.Surface);
            }
        }
    }
}
