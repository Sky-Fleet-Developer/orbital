using System.Collections.Generic;
using Ara3D;
using Newtonsoft.Json;

namespace Orbital.Model.TrajectorySystem
{
    public interface IMass
    {
        [JsonIgnore] double Mass { get; }
        [JsonIgnore] DVector3 Center { get; }
        IEnumerable<IMass> GetContent();
        IEnumerable<IMass> GetRecursively();
    }
}