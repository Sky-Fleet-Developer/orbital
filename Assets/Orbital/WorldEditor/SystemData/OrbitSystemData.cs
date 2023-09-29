using System;
using Ara3D;
using Orbital.Model.Components;
using UnityEngine;

namespace Orbital.WorldEditor.SystemData
{
    [ExecuteInEditMode]
    public class OrbitSystemData : SystemData<OrbitVariables, OrbitSettings>
    {
        [SerializeField] private DVector3 velocity;
        [SerializeField] private OrbitSettings settings;

        public override OrbitVariables GetVariables()
        {
            return new OrbitVariables()
            {
                _localToWorldMatrix = transform.localToWorldMatrix,
                _velocity = velocity
            };
        }

        public override OrbitSettings GetSettings()
        {
            return settings;
        }

        public override void SetSettings(OrbitSettings value)
        {
            settings = value;
        }

        public override void SetVariables(OrbitVariables value)
        {
            velocity = value._velocity;
            transform.position = value._localToWorldMatrix.GetPosition();
            transform.rotation = value._localToWorldMatrix.rotation;
        }
    }
}
