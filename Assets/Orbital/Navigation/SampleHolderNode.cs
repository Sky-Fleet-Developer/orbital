using Newtonsoft.Json;
using Orbital.Core;
using Orbital.Core.TrajectorySystem;

namespace Orbital.Navigation
{
    public abstract class SampleHolderNode : Element
    {
        [JsonIgnore] public abstract IStaticTrajectory Trajectory { get; }
        [JsonIgnore] public IStaticBody Celestial;
    }
}