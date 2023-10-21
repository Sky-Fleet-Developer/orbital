using System;
using UnityEngine;

namespace Orbital.Core.KSPSource
{
    public partial class Planetarium : MonoBehaviour
    {
        public static CelestialFrame Zup;
        public double inverseRotAngle;

        private void Awake()
        {
            CelestialFrame.PlanetaryFrame(0.0, 90.0, inverseRotAngle, ref Zup);
        }
    }
}