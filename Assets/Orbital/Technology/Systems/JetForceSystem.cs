using Leopotam.Ecs;
using Orbital.Core;
using Orbital.Technology.Components;
using UnityEngine;

namespace Orbital.Technology.Systems
{
    public class JetForceSystem : IEcsRunSystem
    {
        private DynamicBodyAdapter _adapter;
        private EcsWorld _world;
        private EcsFilter<JetEngineComponent> _filter; 
        public void Run()
        {
            foreach (int i in _filter)
            {
                ref JetEngineComponent component = ref _filter.Get1(i);
                
                if (ValidateAcceleration(component.acceleration))
                {
                    _adapter.AddForce(component.acceleration * (float)TimeService.DeltaTime);
                    component.acceleration = Vector3.zero;
                }
            }
        }

        private bool ValidateAcceleration(Vector3 value)
        {
            return Mathf.Abs(value.x) + Mathf.Abs(value.y) + Mathf.Abs(value.z) > 1e-5; 
        }
    }
}
