using System;

namespace Orbital.Model.TrajectorySystem
{
    [Serializable]
    public struct TrajectorySettings
    {
        public float mass;
        public float pericenterSpeed;
        public float pericenterRadius;
        public float latitudeShift;
        public float longitudeShift;
        public float inclination;
        public float timeShift;
        public float period;
    }
}