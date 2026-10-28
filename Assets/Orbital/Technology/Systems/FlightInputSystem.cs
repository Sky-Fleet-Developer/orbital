using Leopotam.Ecs;
using Orbital.Core.Flight;
using Orbital.Technology.Components;

namespace Orbital.Technology.Systems
{
    /// <summary>Keys of this step -> control state (<see cref="InputMapper.Map"/>): axes, throttle commands, the RCS switch on an R press.</summary>
    public class FlightInputSystem : IEcsRunSystem
    {
        private EcsFilter<KeyStateComponent, InputLatchComponent, ControlInputComponent> _filter;

        public void Run()
        {
            foreach (int i in _filter)
            {
                ref KeyStateComponent keys = ref _filter.Get1(i);
                ref InputLatchComponent latch = ref _filter.Get2(i);
                ref ControlInputComponent control = ref _filter.Get3(i);
                control.Input = InputMapper.Map(keys.Keys, ref latch.Latch);
            }
        }
    }
}
