using System;
using System.Collections.Generic;
using System.Linq;
using Ara3D;
using Newtonsoft.Json;
using Orbital.Model.SystemComponents;
using UnityEngine;

namespace Orbital.Model.TrajectorySystem
{
    [Serializable]
    public class SingleCenterBranch : IMass
    {
        public double Mass => (Central?.Mass ?? 0);
        [SerializeField, JsonProperty] private TrajectorySettings settings;
        public TrajectorySettings Settings
        {
            get => settings;
            set => settings = value;
        }
        public SingleCenterBranch()
        {
            Central = null;
            Children = new List<IMass>();
        }
        public SingleCenterBranch(IMass central, List<IMass> children)
        {
            Central = central;
            Children = children;
        }

        public IEnumerable<IMass> GetContent()
        {
            yield return Central;
            foreach (IMass child in Children)
            {
                yield return child;
            }
        }

        [JsonProperty] public IMass Central;
        [JsonProperty] public List<IMass> Children;
    }
}