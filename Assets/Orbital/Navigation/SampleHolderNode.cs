using Newtonsoft.Json;
using Orbital.Core;
using Orbital.Core.TrajectorySystem;

namespace Orbital.Navigation
{
    public abstract class SampleHolderNode : Element
    {
        [JsonIgnore] public abstract ITrajectoryRefSampler TrajectorySampler { get; }
        [JsonIgnore] public IStaticBody Celestial;
    }
}