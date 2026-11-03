using System;

namespace Orbital.Core.Flight
{
    /// <summary>
    /// Light flux reaching the eye, independent of any camera (stage 3.5b): the reflected light of a body and the light of a
    /// self-luminous body (the Sun). Engine-free, all math double, no allocations. All fluxes are dimensionless and relative
    /// to the flux of the Sun at 1 AU: <c>1.0 = the Sun at 1 AU</c>. Positions are in the same global frame as
    /// <see cref="CelestialProjection"/>. The camera only converts a flux into pixels (<see cref="Exposure"/>).
    /// </summary>
    public static class BodyFlux
    {
        /// <summary>The IAU astronomical unit, meters: the reference distance of the flux unit.</summary>
        public const double AstronomicalUnit = 1.495978707e11;

        /// <summary>
        /// Phase function of a Lambert sphere, normalized to 1 at full phase: <c>(sin a + (pi - a) * cos a) / pi</c>.
        /// The phase angle <paramref name="phaseAngleRad"/> is measured at the body between the directions to the Sun and to
        /// the eye: 0 = full phase (1.0), pi/2 = quadrature (1/pi), pi = new (0.0). Clamped to [0, pi], monotone.
        /// </summary>
        public static double LambertPhase(double phaseAngleRad)
        {
            if (phaseAngleRad <= 0.0) return 1.0;
            if (phaseAngleRad >= Math.PI) return 0.0;
            double a = phaseAngleRad;
            return (Math.Sin(a) + (Math.PI - a) * Math.Cos(a)) / Math.PI;
        }

        /// <summary>
        /// Reflected light of a body reaching the eye, full phase. The albedo is the geometric albedo: it defines the
        /// brightness at full phase. <c>flux = albedo * (R / dSun)^2 * (AU / dEye)^2</c> (the user's decision: R^2 / (dSun^2 * dEye^2)).
        /// Distances below the body radius are clamped to it (the eye at/inside the body sees the surface value), so the
        /// result is finite; a zero radius or a non-positive albedo gives zero.
        /// </summary>
        public static double ReflectedFlux(double bodyRadius, double distanceToSun, double distanceToEye, double albedo)
        {
            if (bodyRadius <= 0.0) return 0.0;
            if (albedo <= 0.0) return 0.0;
            if (albedo > 1.0) albedo = 1.0;
            if (distanceToSun < bodyRadius) distanceToSun = bodyRadius;
            if (distanceToEye < bodyRadius) distanceToEye = bodyRadius;
            double t = bodyRadius / distanceToSun * (AstronomicalUnit / distanceToEye);
            return albedo * t * t;
        }

        /// <summary>Building block: the full-phase flux times <see cref="LambertPhase"/>.</summary>
        public static double ReflectedFlux(double bodyRadius, double distanceToSun, double distanceToEye, double albedo, double phaseAngleRad)
        {
            double full = ReflectedFlux(bodyRadius, distanceToSun, distanceToEye, albedo);
            return full == 0.0 ? 0.0 : full * LambertPhase(phaseAngleRad);
        }

        /// <summary>
        /// Reflected light of a body at the eye, from positions: the distances and the Lambert phase are computed here, so
        /// the phase cannot be computed wrong by the caller. The phase angle is at the body between the directions to the
        /// Sun and to the eye, via <c>atan2(|a x b|, a*b)</c> (robust for near-parallel directions); when either direction
        /// is degenerate (the Sun or the eye at the center of the body) the phase is taken as full.
        /// </summary>
        public static double ReflectedFlux(Vec3 body, double bodyRadius, Vec3 sun, Vec3 eye, double albedo)
        {
            if (bodyRadius <= 0.0) return 0.0;
            Vec3 toSun = sun - body;
            Vec3 toEye = eye - body;
            double dSun = Vec3.Length(toSun);
            double dEye = Vec3.Length(toEye);
            double phase = 0.0;
            if (dSun > 0.0 && dEye > 0.0)
            {
                phase = Math.Atan2(Vec3.Length(Vec3.Cross(toSun, toEye)), Vec3.Dot(toSun, toEye));
            }
            return ReflectedFlux(bodyRadius, dSun, dEye, albedo, phase);
        }

        /// <summary>
        /// Flux of a self-luminous body (the Sun) reaching the eye: <c>(AU / d)^2</c>, so exactly 1.0 at 1 AU.
        /// The distance is clamped from below to the body radius (the photosphere value), and a missing radius
        /// (<paramref name="sunRadius"/> &lt;= 0) is treated as 1 m: the inverse square law stays exact everywhere except at
        /// the very point, where the huge finite value is capped by <see cref="Exposure"/> from above.
        /// </summary>
        public static double SolarFlux(double distance, double sunRadius)
        {
            double floor = sunRadius > 1.0 ? sunRadius : 1.0;
            double d = distance > floor ? distance : floor;
            double t = AstronomicalUnit / d;
            return t * t;
        }
    }

    /// <summary>
    /// Direction of the light from the Sun (stage 3.5b): the unit vector from the eye to the Sun,
    /// <c>normalize(Sun.global - eye)</c>. When the eye and the Sun coincide there is no direction; a fixed unit direction
    /// is returned instead (<see cref="Vec3.UnitY"/>, the same convention as <see cref="CelestialProjection.Project"/>),
    /// never NaN.
    /// </summary>
    public static class SunLight
    {
        public static Vec3 Direction(Vec3 sunGlobal, Vec3 eye)
        {
            Vec3 offset = sunGlobal - eye;
            double length = Vec3.Length(offset);
            return length > 0.0 ? offset / length : Vec3.UnitY;
        }
    }

    /// <summary>
    /// Parameters of the point exposure (stage 3.5b): a log10 tone map between two flux anchors, a lower threshold that
    /// keeps every nonzero flux visible (all planets stay visible) and an upper cap that bounds the Sun from above.
    /// Fields of a struct, tuned when checking the view in the game, not constants in the code. Later HDR/exposure is
    /// tried by raising <see cref="MaxPointBrightness"/> above 1.
    /// Preconditions: <c>0 &lt; FluxFloor &lt; FluxCeiling</c> and <c>MinPointBrightness &lt;= MaxPointBrightness</c>.
    /// </summary>
    public struct ExposureParams
    {
        /// <summary>Flux of the bottom anchor: log10(FluxFloor) maps to brightness 0 (then the lower threshold applies).</summary>
        public double FluxFloor;
        /// <summary>Flux of the top anchor: log10(FluxCeiling) maps to brightness 1 (the Sun's photosphere, ~4.6e4).</summary>
        public double FluxCeiling;
        /// <summary>Lower threshold: any nonzero flux is at least this bright, so all planets stay visible.</summary>
        public double MinPointBrightness;
        /// <summary>Upper cap: the Sun and the lit planet cannot drown the points; raise above 1 for HDR later.</summary>
        public double MaxPointBrightness;

        /// <summary>Starting values to be tuned in the game: Pluto (~1e-17) ... the Sun's photosphere (~4.6e4).</summary>
        public static ExposureParams Default => new ExposureParams
        {
            FluxFloor = 1e-17,
            FluxCeiling = 4.62e4,
            MinPointBrightness = 0.05,
            MaxPointBrightness = 1.0
        };
    }

    /// <summary>
    /// Point exposure (stage 3.5b): a camera-independent flux becomes the brightness of a point to draw.
    /// <c>brightness = clamp(log10-linear(flux), MinPointBrightness, MaxPointBrightness)</c>: linear in log10 between the
    /// two anchors, the lower threshold keeps every nonzero flux visible, the cap bounds the Sun from above continuously
    /// (flux above the ceiling keeps rising logarithmically until the cap). Zero, negative and NaN flux are 0.
    /// </summary>
    public static class Exposure
    {
        public static double PointBrightness(double flux, in ExposureParams p)
        {
            if (!(flux > 0.0)) return 0.0; // zero, negative, NaN
            double range = Math.Log10(p.FluxCeiling) - Math.Log10(p.FluxFloor);
            double t = (Math.Log10(flux) - Math.Log10(p.FluxFloor)) / range;
            if (t < 0.0) t = 0.0;
            if (t < p.MinPointBrightness) t = p.MinPointBrightness;
            if (t > p.MaxPointBrightness) t = p.MaxPointBrightness;
            return t;
        }
    }
}
