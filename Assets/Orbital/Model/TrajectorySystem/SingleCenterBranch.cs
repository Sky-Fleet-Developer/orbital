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
    public class SingleCenterBranch : IMassSystem
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
            Children = new List<IMassSystem>();
        }
        public SingleCenterBranch(IMassSystem central, List<IMassSystem> children)
        {
            Central = central;
            Children = children;
        }

        public IEnumerable<IMassSystem> GetContent()
        {
            yield return Central;
            foreach (IMassSystem child in Children)
            {
                yield return child;
            }
        }

        [JsonProperty] public IMassSystem Central;
        [JsonProperty] public List<IMassSystem> Children;
    }
}