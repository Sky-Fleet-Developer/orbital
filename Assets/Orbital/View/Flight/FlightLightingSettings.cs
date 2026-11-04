using UnityEngine;

namespace Orbital.View.Flight
{
    /// <summary>
    /// The light of the flight view: all the light comes from the Sun of the world, its direction comes from the simulation, here is only how it
    /// looks. The scene needs no light of its own. Read every frame. Dimming with the distance from the Sun will get its settings here.
    /// </summary>
    public class FlightLightingSettings : ScriptableObject
    {
        [Header("Sunlight on the craft and the ground")]
        public Color sunColor = Color.white;
        public float sunIntensity = 1.1f;

        [Header("Planets and moons")]
        [Tooltip("Brightness of the lit side, a multiplier of the body's tint.")]
        public float dayBrightness = 1f;
        [Tooltip("Brightness of the night side, a multiplier of the body's tint; 0 = a black silhouette.")]
        public float nightBrightness = 0.015f;
    }
}
