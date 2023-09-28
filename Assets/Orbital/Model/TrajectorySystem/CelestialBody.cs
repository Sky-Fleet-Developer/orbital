using System.Collections.Generic;
using Ara3D;
using Orbital.Model.Components;

namespace Orbital.Model.TrajectorySystem
{
    public class CelestialBody : IMass
    {
        public double Mass => _settings.mass;
        public DVector3 Center => _position;
        private DVector3 _position;

        public IEnumerable<IMass> GetRecursively()
        {
            yield return this;
        }

        private readonly CelestialSettings _settings;
        public CelestialBody(CelestialSettings settings)
        {
            _settings = settings;
        }
    }
}