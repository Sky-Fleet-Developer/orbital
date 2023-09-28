using System;
using System.Xml.Serialization;
using Ara3D;
using Newtonsoft.Json;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Orbital.Model.Components
{
    public class OrbitSystemComponent : System<OrbitVariables, OrbitSettings>
    {
        public Matrix4x4 LocalToWorldMatrix => MyVariables._localToWorldMatrix; 
        public DVector3 Velocity => MyVariables._velocity;

        public OrbitSystemComponent(OrbitVariables variables, OrbitSettings settings, Body myBody) : base(variables, settings, myBody)
        {
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