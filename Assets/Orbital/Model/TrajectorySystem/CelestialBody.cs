using System;
using System.Collections.Generic;
using Ara3D;
using Newtonsoft.Json;
using Orbital.Model.Components;
using UnityEngine;

namespace Orbital.Model.TrajectorySystem
{
    [Serializable]
    public class CelestialBody : IMass
    {
        public double Mass => settings.mass;
        public DVector3 Center => position;
        [SerializeField, JsonProperty] private DVector3 position;

        public IEnumerable<IMass> GetContent()
        {
            yield break;
        }
        public IEnumerable<IMass> GetRecursively()
        {
            foreach (IMass content in GetContent())
            {
                foreach (IMass mass in content.GetRecursively())
                {
                    yield return mass;
                }
            }
        }

        [SerializeField, JsonProperty] private CelestialSettings settings;
        public CelestialBody(CelestialSettings settings)
        {
            this.settings = settings;
        }
    }
}