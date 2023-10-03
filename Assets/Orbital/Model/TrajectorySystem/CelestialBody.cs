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
        [SerializeField, JsonProperty] private TrajectorySettings settings;
        public TrajectorySettings Settings
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