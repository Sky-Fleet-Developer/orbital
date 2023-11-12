using System;

namespace Orbital.Core.TrajectorySystem
{
    [Serializable]
    public struct TrajectorySettings
    {
        public float mass;
        public float eccentricity;
        public float semiMajorAxis;
        public float inclination;
        public float argumentOfPeriapsis;
        public float longitudeAscendingNode;
        public float timeShift;
    }
}