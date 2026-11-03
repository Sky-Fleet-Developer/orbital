using Orbital.Core.Bodies;

namespace Orbital.Core.Worlds
{
    /// <summary>
    /// Record of a body of a world loaded from the world file (stage 2c.W2): the model facets (<see cref="PhysicalFacet"/> radius,
    /// <see cref="RotationFacet"/> spin) come from the file, the same numbers the server uses; every other facet (the view: appearance, surface,
    /// atmosphere) comes from the body table record. The table record is not changed (it is an asset), a new record is made.
    /// </summary>
    public static class WorldBodyRecords
    {
        /// <param name="def">Body of the world file.</param>
        /// <param name="view">Record of the body table for the view facets; null = no view facets.</param>
        public static BodyRecord Make(WorldBodyDef def, BodyRecord view)
        {
            BodyRecord result = new BodyRecord {name = view?.name ?? def.Name ?? def.Id};
            if (view?.facets != null)
            {
                foreach (IBodyFacet facet in view.facets)
                {
                    if (facet == null || facet.Id == PhysicalFacet.TypeId || facet.Id == RotationFacet.TypeId) continue;
                    result.facets.Add(facet);
                }
            }

            if (!def.Virtual && def.Radius > 0)
            {
                result.facets.Add(new PhysicalFacet {radiusMeters = def.Radius});
            }

            WorldRotationDef rotation = def.Rotation;
            if (!def.Virtual && rotation != null && (rotation.PeriodSeconds > 0 || rotation.TidallyLocked))
            {
                result.facets.Add(new RotationFacet
                {
                    rotationPeriodSeconds = rotation.PeriodSeconds,
                    axialTiltDegrees = rotation.ObliquityDeg,
                    axisAzimuthDegrees = rotation.AxisAzimuthDeg,
                    initialAngleDegrees = rotation.InitialAngleDeg,
                    tidallyLocked = rotation.TidallyLocked,
                });
            }

            result.Rebuild();
            return result;
        }
    }
}
