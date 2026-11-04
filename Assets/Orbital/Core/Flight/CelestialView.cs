using System;

namespace Orbital.Core.Flight
{
    /// <summary>
    /// Per-body view data for the whole-table projection (stage 3.5c): a parallel array indexed by the body table index,
    /// so no classes or interfaces are needed. The geometric albedo feeds <see cref="BodyFlux"/>; the star flag marks a
    /// self-luminous body (the Sun), whose flux is <see cref="BodyFlux.SolarFlux"/>. The first flagged body of the table
    /// is the light source for every reflected flux.
    /// </summary>
    public struct BodyViewParams
    {
        /// <summary>Geometric albedo of the reflected light; <see cref="BodyFlux"/> clamps it to [0, 1].</summary>
        public double Albedo;
        /// <summary>Self-luminous body: the flux is the solar flux and the first flagged body is the light source.</summary>
        public bool Star;
    }

    /// <summary>
    /// One entry of the whole-table projection buffer (stage 3.5c). <see cref="TableIndex"/> links the entry to its body
    /// unambiguously; entries are written in ascending table index order and virtual bodies (barycenters) are skipped.
    /// Blittable, no references.
    /// </summary>
    public struct BodyViewEntry
    {
        /// <summary>Index of the body in the <see cref="BodyFrameTable"/>.</summary>
        public int TableIndex;
        /// <summary>The body projected for the camera (direction, angular radius, scaled impostor, point/disk mode).</summary>
        public ProjectedBody Projection;
        /// <summary>Light flux reaching the eye in <see cref="BodyFlux"/> units; zero when the table has no star.</summary>
        public double Flux;
        /// <summary>True for the near planet: drawn in Local as a real sphere, its scaled impostor is hidden.</summary>
        public bool NearPlanet;
    }

    /// <summary>
    /// The near planet state of the Local camera, stored by the caller across frames (stage 3.5c): the body drawn in
    /// Local as a real sphere while the craft is low, and whether the mode is on. The default value means "no body"
    /// (<see cref="Body"/> == -1) because index 0 is a real body (the Sun); the property stores body + 1 internally so
    /// that <c>default</c> and the field initializer <c>new NearPlanetState { Body = i, Near = ... }</c> both work.
    /// </summary>
    public struct NearPlanetState
    {
        private int _bodyPlus1;

        /// <summary>Index of the body the mode belongs to in the <see cref="BodyFrameTable"/>; -1 = no body.</summary>
        public int Body
        {
            get => _bodyPlus1 - 1;
            set => _bodyPlus1 = value + 1;
        }

        /// <summary>The mode: the body of <see cref="Body"/> is drawn in Local as a real sphere.</summary>
        public bool Near;
    }

    /// <summary>
    /// The near planet thresholds and the far plane of the Local camera (stage 3.5c), all pure functions of the altitude
    /// and the body radius; the mode state lives in <see cref="NearPlanetState"/> and is passed through
    /// <see cref="Update"/> so nothing is stored here.
    /// </summary>
    public static class NearPlanetMode
    {
        /// <summary>
        /// Maximum far plane of the Local camera, meters: the depth the Local layer still holds at near 0.3 (the plan's
        /// "above about 1e7 m only the Scaled layer holds the depth"). A parameter so the view can tune it down if the
        /// depth buffer (24-bit) z-fights.
        /// </summary>
        public const double DefaultMaxFar = 1e7;

        /// <summary>The plan's visual cap: the near planet exits at 2 radii when the far budget allows it.</summary>
        public const double MaxAltitudeFactor = 2.0;

        /// <summary>Hysteresis: the enter altitude is the exit altitude times this factor (2:1 band in altitude).</summary>
        public const double HysteresisFactor = 0.5;

        /// <summary>
        /// The exit altitude, meters: the mode stays on while the craft is below it. It is <c>2R</c> when the far plane
        /// of <see cref="LocalFar"/> fits into the budget, otherwise the altitude where <c>LocalFar(h, R) == maxFar</c>:
        /// <c>hFar = -R + sqrt(R^2 + (maxFar - R)^2)</c>. Zero when there is nothing to draw (<c>R &lt;= 0</c>) or the
        /// body cannot fit into the budget (<c>R &gt;= maxFar</c>, the Sun): such a body is never a near planet.
        /// </summary>
        public static double ExitAltitude(double bodyRadius, double maxFar = DefaultMaxFar)
        {
            if (!(bodyRadius > 0.0) || bodyRadius >= maxFar) return 0.0;
            double twoRadii = MaxAltitudeFactor * bodyRadius;
            double hFar = Math.Sqrt(bodyRadius * bodyRadius + (maxFar - bodyRadius) * (maxFar - bodyRadius)) - bodyRadius;
            return twoRadii < hFar ? twoRadii : hFar;
        }

        /// <summary>The enter altitude, meters: below it a craft that is not yet near becomes near. Half the exit.</summary>
        public static double EnterAltitude(double bodyRadius, double maxFar = DefaultMaxFar) =>
            ExitAltitude(bodyRadius, maxFar) * HysteresisFactor;

        /// <summary>
        /// The near planet mode after this frame, written into <paramref name="s"/>. On a parent change the state is
        /// reset and decided from the enter threshold (an old near flag does not survive a body change); on the same
        /// body the hysteresis of <see cref="ExitAltitude"/>/<see cref="EnterAltitude"/> applies: while near the mode
        /// holds up to the exit, while far it needs the enter altitude. No body (<paramref name="parent"/> &lt; 0), a
        /// virtual parent (<c>R &lt;= 0</c>) and a NaN altitude give <c>Near = false</c>. Returns the new
        /// <see cref="NearPlanetState.Near"/>.
        /// </summary>
        public static bool Update(ref NearPlanetState s, int parent, double altitude, double bodyRadius,
            double maxFar = DefaultMaxFar)
        {
            if (parent < 0)
            {
                s.Body = -1;
                s.Near = false;
                return false;
            }
            if (parent != s.Body)
            {
                s.Body = parent;
                double enter = EnterAltitude(bodyRadius, maxFar);
                s.Near = enter > 0.0 && altitude < enter;
                return s.Near;
            }
            double exit = ExitAltitude(bodyRadius, maxFar);
            if (exit <= 0.0)
            {
                s.Near = false;
                return false;
            }
            s.Near = s.Near ? altitude < exit : altitude < exit * HysteresisFactor;
            return s.Near;
        }

        /// <summary>
        /// The far plane of the Local camera from the altitude, meters: the distance to the horizon
        /// <c>sqrt(2Rh + h^2)</c> plus the radius (the plan's formula; the horizon distance alone is the exact bound of
        /// the visible surface, the radius is the margin), clamped to <c>[R, maxFar]</c>. The altitude is clamped to
        /// zero from below (a craft on or under the ground sees far = R, no NaN), and a zero or negative radius gives
        /// the budget itself (there is no sphere to cover).
        /// </summary>
        public static double LocalFar(double altitude, double bodyRadius, double maxFar = DefaultMaxFar)
        {
            if (!(bodyRadius > 0.0)) return maxFar;
            double h = altitude > 0.0 ? altitude : 0.0;
            double far = Math.Sqrt(h * (2.0 * bodyRadius + h)) + bodyRadius;
            return far > maxFar ? maxFar : far;
        }
    }

    /// <summary>
    /// The projection of the whole body table in one call (stage 3.5c): for the game time and the camera, every real
    /// (non-virtual) body of the table gets one <see cref="BodyViewEntry"/> in the caller's buffer — the exact direction
    /// and angular radius via <see cref="CelestialProjection.Project"/>, the flux via <see cref="BodyFlux"/>, and the
    /// near planet flag for the Local camera. Engine-free, no allocations, one pass.
    /// </summary>
    public static class CelestialView
    {
        /// <summary>
        /// Fills <paramref name="buffer"/>[0..n) with the real bodies of the table in ascending table index order
        /// (barycenters skipped; the entries carry their <see cref="BodyViewEntry.TableIndex"/>) and returns n. The
        /// view data <paramref name="view"/> is parallel to the table and indexed by the table index; the first
        /// <see cref="BodyViewParams.Star"/> body is the Sun: its flux is <see cref="BodyFlux.SolarFlux"/>, the other
        /// fluxes are <see cref="BodyFlux.ReflectedFlux"/> against it, and without a star every flux is zero. The entry
        /// of the body <paramref name="nearBody"/> (-1 = none) is marked as the near planet; a virtual near body marks
        /// nothing. Preconditions (violations throw <see cref="ArgumentException"/>):
        /// <c>view.Length &gt;= table.Count</c>, <c>buffer.Length &gt;=</c> the number of non-virtual bodies (so
        /// <c>table.Count</c> is always enough), <c>-1 &lt;= nearBody &lt; table.Count</c>.
        /// </summary>
        public static int Fill(
            BodyFrameTable table,
            double t,
            in ProjectionCamera cam,
            ReadOnlySpan<BodyViewParams> view,
            double scaledDistance,
            double minPixels,
            int nearBody,
            Span<BodyViewEntry> buffer)
        {
            if (table == null) throw new ArgumentNullException(nameof(table));
            if (view.Length < table.Count)
                throw new ArgumentException($"view must have at least table.Count = {table.Count} entries, got {view.Length}", nameof(view));
            if (nearBody < -1 || nearBody >= table.Count)
                throw new ArgumentException($"nearBody = {nearBody} is outside [-1, {table.Count})", nameof(nearBody));

            int sun = -1;
            for (int i = 0; i < table.Count; i++)
            {
                if (view[i].Star)
                {
                    sun = i;
                    break;
                }
            }
            Vec3 sunPos = default;
            if (sun >= 0) table.GlobalState(sun, t, out sunPos, out _);

            int real = 0;
            for (int i = 0; i < table.Count; i++)
            {
                if (!table[i].Virtual) real++;
            }
            if (buffer.Length < real)
                throw new ArgumentException($"buffer must have at least {real} entries (the non-virtual bodies), got {buffer.Length}", nameof(buffer));

            int n = 0;
            for (int i = 0; i < table.Count; i++)
            {
                ref readonly BodyFrame body = ref table[i];
                if (body.Virtual) continue;
                table.GlobalState(i, t, out Vec3 pos, out _);
                ProjectedBody projection = CelestialProjection.Project(in cam, pos, body.Radius, scaledDistance, minPixels);
                BodyViewEntry entry;
                entry.TableIndex = i;
                entry.Projection = projection;
                entry.Flux = sun < 0 ? 0.0
                    : i == sun ? BodyFlux.SolarFlux(projection.Distance, table[sun].Radius)
                    : BodyFlux.ReflectedFlux(pos, body.Radius, sunPos, cam.Eye, view[i].Albedo);
                entry.NearPlanet = i == nearBody;
                buffer[n++] = entry;
            }
            return n;
        }

        /// <summary>
        /// The painter's order of filled entries (stage 3.6): writes into <paramref name="order"/>[0..n) the positions of the entries from the farthest
        /// real distance to the nearest (ties keep the entry order, so the result is deterministic). All impostors sit at one scaled distance, so their
        /// depth says nothing about who covers whom; the real distance does. No allocations. <c>order.Length</c> must be at least <c>entries.Length</c>.
        /// </summary>
        public static void FarToNearOrder(ReadOnlySpan<BodyViewEntry> entries, Span<int> order)
        {
            if (order.Length < entries.Length)
                throw new ArgumentException($"order must have at least {entries.Length} entries, got {order.Length}", nameof(order));
            for (int i = 0; i < entries.Length; i++)
            {
                double distance = entries[i].Projection.Distance;
                int j = i;
                while (j > 0 && entries[order[j - 1]].Projection.Distance < distance)
                {
                    order[j] = order[j - 1];
                    j--;
                }
                order[j] = i;
            }
        }
    }
}
