using System;
using Ara3D;
using Newtonsoft.Json;
using Orbital.Model;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Orbital.Model.Components
{
    public class CelestialSystemComponent : System<CelestialVariables, CelestialSettings>
    {
        public float Mass => MySettings.mass;
        public CelestialSystemComponent(CelestialVariables variables, CelestialSettings settings, Body myBody) : base(variables, settings, myBody)
        {
        }
    }
    
    [Serializable]
    public struct CelestialVariables
    {
        public float time;
    }
    
    [Serializable]
    public struct CelestialSettings
    {
        public float mass;
        public float pericenterSpeed;
        public float pericenterRadius;
        public float latitudeShift;
        public float longitudeShift;
        
    }
}
