using System.Collections.Generic;
using Orbital.Model.SystemComponents;

namespace Orbital.Model.TrajectorySystem
{
    public class RigidBody : IMass
    {
        public double Mass => _settings.mass;
        public TrajectorySettings Settings
        {
            get => _settings;
            set => _settings = value;
        }
        private TrajectorySettings _settings;
        
        public IEnumerable<IMass> GetContent()
        {
            yield break;
        }
    }
}
