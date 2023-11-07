using System;
using System.Collections.Generic;
using System.Linq;
using Leopotam.Ecs;
using Leopotam.Ecs.UnityIntegration;
using Orbital.Technology.Systems;
using UnityEngine;

namespace Orbital.Technology
{
    public class EcsAdapter : MonoBehaviour
    {
        private EcsWorld _world;
        private EcsSystems[] _systems;

        protected virtual void Start()
        {
            _world = new EcsWorld();
            //_systems = new EcsSystems(_world);
            
            #if UNITY_EDITOR
            EcsWorldObserver.Create(_world);
            #endif

            _systems = Create(NewSystemGroup).ToArray();
            foreach (EcsSystems group in _systems)
            {
                Inject(group.Inject(_world)).Init();   
#if UNITY_EDITOR
                EcsSystemsObserver.Create(group);
#endif
            }
        }

        private EcsSystems NewSystemGroup() => new EcsSystems(_world);
        
        protected virtual IEnumerable<EcsSystems> Create(Func<EcsSystems> creator)
        {
            yield break;
        }
        
        protected virtual EcsSystems Inject(EcsSystems systems)
        {
            return systems;
        }

        public EcsSystems GetSystemsGroup(int index) => _systems[index];
    }
}
