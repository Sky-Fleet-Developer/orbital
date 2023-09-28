using System.Collections.Generic;
using Ara3D;

namespace Orbital.Model.TrajectorySystem
{
    public class DoubleSystemBranch : IMass
    {
        public double Mass => _childA.Mass + _childB.Mass;
        public DVector3 Center => _position;
        private DVector3 _position;

        public IEnumerable<IMass> GetRecursively()
        {
            foreach (IMass mass in _childA.GetRecursively())
            {
                yield return mass;
            }
            foreach (IMass mass in _childB.GetRecursively())
            {
                yield return mass;
            }
        }
        
        private IMass _childA;
        private IMass _childB;
    }
}