using Newtonsoft.Json;
using Orbital.Core;
using Orbital.Core.TrajectorySystem;
using Sirenix.OdinInspector;

namespace Orbital.Navigation
{
    public abstract class SampleHolderNode : Element
    {
        [JsonIgnore] public abstract IStaticOrbit Orbit { get; }
        [JsonIgnore] public IStaticBody Celestial;
        [JsonIgnore, ShowInInspector] public OrbitEnding Ending { get; private set; }
        public void FindEnding()
        {
            Ending = Orbit.GetEnding(Celestial, Time);
        }
    }
}