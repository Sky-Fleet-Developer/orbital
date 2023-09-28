using System;
using System.Collections.Generic;
using Ara3D;
using Newtonsoft.Json;
using UnityEngine;

namespace Orbital.Model.TrajectorySystem
{
    [Serializable]
    public class DoubleSystemBranch : IMass
    {
        public double Mass => ChildA.Mass + ChildB.Mass;
        public DVector3 Center => position;
        [SerializeField, JsonProperty] private DVector3 position;

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

        [JsonProperty] public IMass ChildA;
        [JsonProperty] public IMass ChildB;
    }
}