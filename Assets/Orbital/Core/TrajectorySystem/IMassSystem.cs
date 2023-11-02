using System.Collections.Generic;
using Newtonsoft.Json;

namespace Orbital.Core.TrajectorySystem
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
        bool IsSatellite(IMassSystem subSystem);
    }
}