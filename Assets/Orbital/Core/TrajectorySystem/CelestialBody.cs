using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace Orbital.Core.TrajectorySystem
{
    [Serializable]
    public class CelestialBody : IMassSystem
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


        public IEnumerable<IMassSystem> GetContent()
        {
            yield break;
        }
    }
}