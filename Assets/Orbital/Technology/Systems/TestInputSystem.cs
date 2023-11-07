using Leopotam.Ecs;
using Orbital.Technology.Components;
using UnityEngine;

namespace Orbital.Technology.Systems
{
    public class TestInputSystem : IEcsRunSystem
    {
        public float Acceleration;
        private EcsFilter<JetEngineComponent> _filter;

        public void Run()
        {
            Vector3 input = new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical")) * Acceleration;
            if (input.sqrMagnitude > 1e-5)
            {
                foreach (int i in _filter)
                {
                    ref JetEngineComponent component = ref _filter.Get1(i);
                    component.acceleration = input;
                }
            }
        }
    }
}
