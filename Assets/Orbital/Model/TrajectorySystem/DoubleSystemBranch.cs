using System;
using System.Collections.Generic;
using Ara3D;
using Newtonsoft.Json;
using Orbital.Model.Components;
using UnityEngine;

namespace Orbital.Model.TrajectorySystem
{
    [Serializable]
    public class DoubleSystemBranch : IMass
    {
        public double Mass => ChildA.Mass + ChildB.Mass;
        [SerializeField, JsonProperty] private CelestialSettings settings;
        public CelestialSettings Settings
        {
            get => settings;
            set => settings = value;
        }
        public DoubleSystemBranch()
        {
            ChildA = null;
            ChildB = null;
        }
        public DoubleSystemBranch(IMass childA, IMass childB)
        {
            ChildA = childA;
            ChildB = childB;
        }

        public IEnumerable<IMass> GetContent()
        {
            yield return ChildA;
            yield return ChildB;
        }

        [JsonProperty] public IMass ChildA;
        [JsonProperty] public IMass ChildB;
    }
}