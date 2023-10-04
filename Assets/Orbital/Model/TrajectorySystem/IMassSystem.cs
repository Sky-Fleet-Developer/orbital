using System.Collections.Generic;
using Ara3D;
using Newtonsoft.Json;
using Orbital.Model.SystemComponents;

namespace Orbital.Model.TrajectorySystem
{
    public interface IMass
    {
        [JsonIgnore] double Mass { get; }
    }

    public interface IMassContainer
    {
        IEnumerable<IMassSystem> GetContent();
    }

    public interface ITrajectorySettingsHolder
    {
        [JsonIgnore] TrajectorySettings Settings { get; set; }
    }
    
    public interface IMassSystem : IMass, IMassContainer, ITrajectorySettingsHolder
    {
    }
}