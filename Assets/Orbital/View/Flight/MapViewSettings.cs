using UnityEngine;

namespace Orbital.View.Flight
{
    /// <summary>The map of the flight (M or C): its camera, lines and markers. Read every frame unless a tooltip says otherwise.</summary>
    public sealed class MapViewSettings : ScriptableObject
    {
        [Header("Camera")]
        [Tooltip("Layer of the map objects; must differ from the flight camera's and the sky's. Read at the start of a flight.")]
        [Range(0, 31)] public int layer = 29;
        [Range(5f, 90f)] public float fieldOfView = 45f;
        public Color background = new Color(0.015f, 0.02f, 0.035f);
        [Tooltip("Degrees per pixel of a mouse drag.")]
        public float dragSpeed = 0.25f;
        [Tooltip("Zoom per wheel notch.")]
        [Range(0.5f, 0.99f)] public float zoomPerNotch = 0.85f;

        [Header("Lines")]
        [Tooltip("Width of the orbits of the bodies, pixels.")]
        public float orbitWidth = 1.5f;
        [Tooltip("Width of the craft's trajectory, pixels.")]
        public float trajectoryWidth = 2.5f;
        [Range(0f, 1f)] public float orbitOpacity = 0.55f;
        [Tooltip("An orbit smaller than this on the screen is hidden with its body's label, pixels of radius.")]
        public float minOrbitPixels = 14f;
        [Tooltip("The conics of the trajectory in order: now, after the first change of body, after the second... The last color repeats.")]
        public Color[] trajectoryColors =
        {
            new Color(0.25f, 1f, 0.55f),
            new Color(1f, 0.72f, 0.2f),
            new Color(1f, 0.4f, 0.75f),
            new Color(0.5f, 0.75f, 1f),
        };

        [Header("Markers")]
        public Color craftColor = new Color(0.25f, 1f, 0.55f);
        public Color otherCraftColor = new Color(0.45f, 0.8f, 1f);
        public Color eventColor = new Color(1f, 0.95f, 0.6f);
        public Color impactColor = new Color(1f, 0.3f, 0.2f);
        [Tooltip("A body smaller than this on the screen also gets a dot, pixels of radius.")]
        public float minBodyPixels = 3f;
        [Tooltip("How close the cursor must be to a marker or a line, pixels.")]
        public float hoverPixels = 10f;
    }
}
