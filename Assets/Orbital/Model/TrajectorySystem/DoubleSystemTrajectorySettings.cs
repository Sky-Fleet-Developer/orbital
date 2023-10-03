using System;

namespace Orbital.Model.TrajectorySystem
{
    [Serializable]
    public struct DoubleSystemTrajectorySettings
    {
        public float aMass;
        public float bMass;
        public float period;
        public float aPericenterRadius;
        public float latitudeShift;
        public float longitudeShift;
        public float inclination;
        public float timeShift;
    }
}