using System;
using System.Collections.Generic;
using Ara3D;
using Newtonsoft.Json;
using Orbital.Model.SystemComponents;
using UnityEngine;

namespace Orbital.Model.TrajectorySystem
{
    [Serializable]
    public class CelestialBody : IMass
    {
        public double Mass => settings.mass;
        [SerializeField, JsonProperty] private CelestialSettings settings;
        public CelestialSettings Settings
        {
            get => settings;
            set => settings = value;
        }
        public CelestialBody()
        {
        }


        public IEnumerable<IMass> GetContent()
        {
            yield break;
        }
    }
}