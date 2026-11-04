using UnityEngine;

namespace Orbital.View.Flight
{
    /// <summary>The camera the player looks through in flight: lens, zoom, mouse, its layer. Read every frame unless a tooltip says otherwise.</summary>
    public class FlightCameraSettings : ScriptableObject
    {
        [Tooltip("Layer of everything near the craft (craft, ground, other crafts); this camera draws only it. Read at the start of a flight.")]
        [Range(0, 31)] public int layer = 31;
        [Tooltip("Vertical field of view, degrees.")]
        public float fieldOfView = 60f;
        [Tooltip("Near clip plane, meters.")]
        public float nearClip = 0.3f;
        [Tooltip("The far clip plane is never below this, meters: other crafts, the ground patch, the pad.")]
        public float minFar = 200000f;

        [Header("Orbit around the craft")]
        [Tooltip("Distance from the craft at the start of a flight, meters; the mouse wheel changes it.")]
        public float distance = 45f;
        [Tooltip("Mouse wheel limits, meters.")]
        public float minDistance = 8f;
        public float maxDistance = 5000f;
        [Tooltip("Degrees per pixel of mouse drag.")]
        public float dragSpeed = 0.25f;
    }
}
