using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace Orbital.Core.TrajectorySystem
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

        public bool IsSatellite(IMassSystem subSystem)
        {
            return Children.Contains(subSystem);
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