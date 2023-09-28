using System.Collections.Generic;
using System.Linq;
using Ara3D;

namespace Orbital.Model.TrajectorySystem
{
    public class SingleCenterBranch : IMass
    {
        public double Mass => _central.Mass + _children.Sum(x => x.Mass);
        public DVector3 Center => _position;
        private DVector3 _position;

        public IEnumerable<IMass> GetRecursively()
        {
            foreach (IMass mass in _central.GetRecursively())
            {
                yield return mass;
            }
            
            foreach (IMass child in _children)
            {
                foreach (IMass mass in child.GetRecursively())
                {
                    yield return mass;
                }
            }
        }
        
        private IMass _central;
        private List<IMass> _children;
    }
}