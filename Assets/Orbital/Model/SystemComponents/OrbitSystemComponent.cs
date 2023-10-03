using System;
using Ara3D;
using Newtonsoft.Json;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Orbital.Model.SystemComponents
{
    [ExecuteInEditMode]
    public class OrbitSystemComponent : SystemComponent<OrbitVariables, OrbitSettings>
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
    
    [Serializable]
    public struct OrbitVariables
    {
        [JsonProperty, ShowInInspector] public Matrix4x4 _localToWorldMatrix;
        [JsonProperty, ShowInInspector] public DVector3 _velocity;
    }
        
    [Serializable]
    public struct OrbitSettings
    {
        
    }
}
