using Leopotam.Ecs;
using Orbital.Technology.Components;

namespace Orbital.Technology.Systems
{
    public class VehicleCreationSystem : IEcsInitSystem
    {
        private EcsWorld _world;
        public void Init()
        {
            EcsEntity jetEntity = _world.NewEntity();
            var jet = jetEntity.Get<JetEngineComponent>();
        }
    }
}
