using UnityEngine;

namespace Orbital.View.Flight
{
    /// <summary>What is drawn on the ground around the craft: the ground patch and the start pad with its poles. Relief and places will get their settings here.</summary>
    public class LocationViewSettings : ScriptableObject
    {
        [Header("Ground patch")]
        [Tooltip("Half size of the patch under the craft (a square of this half side, bent along the planet), meters.")]
        public float patchHalfSize = 4000f;
        [Tooltip("Cells along one side. Read at the start of a flight.")]
        public int patchCells = 40;

        [Header("Start pad")]
        [Tooltip("Distance between the poles around the pad, meters. Read at the start of a flight.")]
        public float poleSpacing = 250f;
        [Tooltip("The pad and the poles are drawn while the craft is closer than this, meters.")]
        public float markersVisibleDistance = 20000f;
    }
}
