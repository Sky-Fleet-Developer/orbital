using System;
using System.Xml.Serialization;
using Newtonsoft.Json;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Orbital.Model.Components
{
    public class OrbitComponent : Component
    {
        [JsonProperty, ShowInInspector] private Matrix4x4 _localToWorldMatrix;
        [JsonProperty, ShowInInspector] private Vector3 _velocity;
        public Matrix4x4 LocalToWorldMatrix => _localToWorldMatrix; 
        public Vector3 Velocity => _velocity; 

        public OrbitComponent(Body myBody) : base(myBody)
        {
            _localToWorldMatrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one);
            _velocity = Vector3.zero;
        }
    }
}