using System;
using System.Collections.Generic;
using System.Linq;
using Ara3D;
using Newtonsoft.Json;
using UnityEngine;

namespace Orbital.Model.TrajectorySystem
{
    [Serializable]
    public class SingleCenterBranch : IMass
    {
        public double Mass => Central.Mass + Children.Sum(x => x.Mass);
        public DVector3 Center => position;
        [SerializeField, JsonProperty] private DVector3 position;

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
        
        [JsonProperty] public IMass Central;
        [JsonProperty] public List<IMass> Children;
    }
}