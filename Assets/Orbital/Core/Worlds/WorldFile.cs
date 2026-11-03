using System.Collections.Generic;

namespace Orbital.Core.Worlds
{
    // Format of the shared world file Assets/_game/Config/world.json (stage 2c.W). It is the server format (orbital-server/OrbitServer/World.cs,
    // WorldDef/BodyDef): camelCase JSON, names matched without regard to case. These classes are plain data for any JSON reader.

    /// <summary>The whole world: bodies in an order where a parent comes before its children.</summary>
    public class WorldFileDef
    {
        public string Note { get; set; }
        /// <summary>World zero epoch (stage 2c.W2): game time from which orbit epochs and planet rotation count; no field = 0.</summary>
        public double EpochZero { get; set; }
        public List<WorldBodyDef> Bodies { get; set; } = new List<WorldBodyDef>();
    }

    public class WorldBodyDef
    {
        /// <summary>Stable id (lowercase, dashes): the server, start points and the client binding use it.</summary>
        public string Id { get; set; }
        /// <summary>Display name; the scene object of the body is named "&lt;Name&gt;[&lt;slot&gt;]".</summary>
        public string Name { get; set; }
        /// <summary>Id of the parent body; null for the root.</summary>
        public string Parent { get; set; }
        /// <summary>Gravitational parameter G*m, m^3/s^2.</summary>
        public double Nu { get; set; }
        /// <summary>Surface radius, m (0 for a barycenter).</summary>
        public double Radius { get; set; }
        /// <summary>Optional override of the Laplace sphere of influence (server only; the client computes the Laplace sphere).</summary>
        public double? SoiRadius { get; set; }
        public WorldOrbitDef Orbit { get; set; }
        public WorldRotationDef Rotation { get; set; }
        /// <summary>Set on the virtual body of a double system (barycenter): its members are its children.</summary>
        public WorldSystemDef System { get; set; }
        /// <summary>Barycenter of a double system: no surface.</summary>
        public bool Virtual { get; set; }
        /// <summary>Informational: the scene mass, kg, that nu came from.</summary>
        public double? SceneMass { get; set; }
    }

    /// <summary>Keplerian elements around the parent. Mean anomaly <see cref="MeanAnomalyDeg"/> at game time <see cref="Epoch"/>.</summary>
    public class WorldOrbitDef
    {
        public double InclinationDeg { get; set; }
        public double Eccentricity { get; set; }
        public double SemiMajorAxis { get; set; }
        public double LanDeg { get; set; }
        public double AopDeg { get; set; }
        public double MeanAnomalyDeg { get; set; }
        public double Epoch { get; set; }
        /// <summary>Gravitational parameter of this orbit; default the parent's nu. Members of a double system: G*m_other^3/(m_A+m_B)^2.</summary>
        public double? OrbitNu { get; set; }
    }

    public class WorldRotationDef
    {
        public double PeriodSeconds { get; set; }
        public double ObliquityDeg { get; set; }
        public double AxisAzimuthDeg { get; set; }
        public double InitialAngleDeg { get; set; }
        public bool TidallyLocked { get; set; }
    }

    public class WorldSystemDef
    {
        public string Kind { get; set; } = "barycentric";
        public List<string> Members { get; set; } = new List<string>();
    }
}
