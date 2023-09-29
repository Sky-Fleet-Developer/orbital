using System.Collections.Generic;
using Ara3D;
using Newtonsoft.Json;
using Orbital.Model.Components;

namespace Orbital.Model.TrajectorySystem
{
    public interface IMass
    {
        [JsonIgnore] double Mass { get; }
        [JsonIgnore] CelestialSettings Settings { get; set; }

        IEnumerable<IMass> GetContent();
    }
}