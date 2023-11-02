using System;

namespace Orbital.Core.TrajectorySystem
{
    [Serializable]
    public struct DoubleSystemTrajectorySettings
    {
        public float aMass;
        public float bMass;
        public float eccentricity;
        public float aPericenterRadius;
        public float argumentOfPeriapsis;
        public float longitudeAscendingNode;
        public float inclination;
        public float timeShift;
    }
}