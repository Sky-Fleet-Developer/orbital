using System;
using System.Collections.Generic;
using Leopotam.Ecs;
using Orbital.Core;
using Orbital.Core.Handles;
using Orbital.Technology.Systems;
using UnityEngine;
using Zenject;

namespace Orbital.Technology
{
    [RequireComponent(typeof(IDynamicBody))]
    public class DynamicBodyAdapter : EcsAdapter, IFixedUpdateHandler, IUpdateHandler
    {
        [SerializeField] private float acceleration;
        [Inject] private DiContainer _diContainer;
        private IDynamicBody _dynamicBody;
        private void Awake()
        {
            _dynamicBody = GetComponent<IDynamicBody>();
        }

        protected override void Start()
        {
            base.Start();
            HandlesRegister.RegisterHandlers(this, _diContainer);
        }

        protected override IEnumerable<EcsSystems> Create(Func<EcsSystems> creator)
        {
            yield return creator() // systems for init only
                .Add(new VehicleCreationSystem());
            yield return creator() // systems for fixed update
                .Add(new JetForceSystem());
            yield return creator() // systems for update
                .Add(new TestInputSystem{Acceleration = acceleration});
        }

        protected override EcsSystems Inject(EcsSystems systems)
        {
            return systems.Inject(this);
        }

        public void AddForce(Vector3 force)
        {
            _dynamicBody.AddForce(force);
        }

        void IFixedUpdateHandler.FixedUpdate()
        {
            GetSystemsGroup(1).Run();
        }

        void IUpdateHandler.Update()
        {
            GetSystemsGroup(2).Run();
        }
    }
}
