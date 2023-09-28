using System.Collections.Generic;
using Ara3D;

namespace Orbital.Model.TrajectorySystem
{
    public interface IMass
    {
        double Mass { get; }
        DVector3 Center { get; }
        IEnumerable<IMass> GetRecursively();
    }
}