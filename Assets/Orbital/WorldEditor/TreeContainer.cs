using System;
using Newtonsoft.Json;
using Orbital.Model.TrajectorySystem;

namespace Orbital.WorldEditor
{
    [Serializable]
    public class TreeContainer
    {
        [JsonProperty] public IMass Root;
    }
}
