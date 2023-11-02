using Ara3D;
using Orbital.Core;
using UnityEngine;

namespace Orbital.Navigation
{
    public struct OrbitEnding
    {
        public OrbitEndingType Type;
        public DVector3 Point;
        public double Time;
        public IStaticBody NextCelestial;

        public OrbitEnding(OrbitEndingType type, DVector3 point, double time, IStaticBody nextCelestial)
        {
            Type = type;
            Point = point;
            Time = time;
            NextCelestial = nextCelestial;
        }
    }
}
