using System;
using UnityEngine;

namespace Orbital.Model.SystemComponents
{
    [ExecuteInEditMode]
    public class CelestialSystemComponent : SystemComponent<CelestialVariables, CelestialSettings>
    {
        private CelestialSettings _settings;
        [SerializeField] private CelestialVariables variables;

        public override CelestialVariables GetVariables()
        {
            return variables;
        }

        public override CelestialSettings GetSettings()
        {
            return _settings;
        }

        public override void SetSettings(CelestialSettings value)
        {
            _settings = value;
        }

        public override void SetVariables(CelestialVariables value)
        {
            variables = value;
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
        public float inclination;
        public float timeShift;
        public float period;
    }
    
    [Serializable]
    public struct DoubleSystemSettings
    {
        public float aMass;
        public float bMass;
        public float period;
        public float aPericenterRadius;
        public float latitudeShift;
        public float longitudeShift;
        public float inclination;
        public float timeShift;
    }
}
