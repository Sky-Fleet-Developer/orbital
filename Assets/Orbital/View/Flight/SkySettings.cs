using Orbital.Core.Flight;
using UnityEngine;

namespace Orbital.View.Flight
{
    /// <summary>
    /// How the sky is drawn: the bodies as disks and points, their exposure, the background. Belongs to the sky and not to the flight camera, so a
    /// telescope can use the same asset or its own. Read every frame unless a tooltip says otherwise. A star field will get its settings here.
    /// </summary>
    public class SkySettings : ScriptableObject
    {
        [Tooltip("Layer of the sky objects; must differ from the flight camera's layer. Read at the start of a flight.")]
        [Range(0, 31)] public int layer = 30;
        [Tooltip("Color behind the bodies.")]
        public Color background = new Color(0.004f, 0.005f, 0.01f);
        [Tooltip("Distance of the bodies from the sky camera, its own units; the picture does not depend on it, only the precision does.")]
        public float scaledDistance = 100000f;

        [Header("Points and disks")]
        [Tooltip("A body with a smaller angular diameter in the camera's pixels is drawn as a point.")]
        public float minDiskPixels = 3f;
        [Tooltip("Size of a point on the screen, pixels.")]
        public float pointPixels = 4f;

        [Header("Brightness of the points (flux: 1 = the Sun at 1 AU)")]
        [Tooltip("Flux at brightness 0.")]
        public double fluxFloor = 1e-17;
        [Tooltip("Flux at brightness 1.")]
        public double fluxCeiling = 4.62e4;
        [Tooltip("Any lit body is at least this bright, so all planets stay visible.")]
        public float minPointBrightness = 0.05f;
        [Tooltip("Upper limit of a point's brightness; above 1 shows only with HDR and bloom.")]
        public float maxPointBrightness = 1f;

        public ExposureParams Exposure => new ExposureParams
        {
            FluxFloor = fluxFloor,
            FluxCeiling = fluxCeiling,
            MinPointBrightness = minPointBrightness,
            MaxPointBrightness = maxPointBrightness,
        };
    }
}
