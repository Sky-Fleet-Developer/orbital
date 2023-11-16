using System;

namespace Orbital.Core.TrajectorySystem
{
    [Serializable]
    public struct TrajectorySettings
    {
        public double mass;
        public double eccentricity;
        public double semiMajorAxis;
        public double inclination;
        public double argumentOfPeriapsis;
        public double longitudeAscendingNode;
        public double epoch;
    }
}