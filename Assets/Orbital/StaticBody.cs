using System;
using System.Collections.Generic;
using Ara3D;
using Orbital.Core;
using Orbital.Core.TrajectorySystem;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Orbital
{
    [ExecuteInEditMode]
    public class StaticBody : SystemComponent<StaticBodyVariables, StaticBodySettings>, IStaticBody, IStaticBodyAccessor
    {
        [SerializeField] private StaticBodySettings settings;
        [ShowInInspector] private StaticBodyVariables _variables;
        private World _world;
        private IMassSystem _massSystem;
        private StaticOrbit _orbit;
        [ShowInInspector] private IStaticBody _parent;
        [ShowInInspector] private IStaticBody[] _children;
        private bool _isSatellite;
        IMassSystem IStaticBody.MassSystem => _massSystem;

        IStaticBody IStaticBody.Parent => _parent;
        IEnumerable<IStaticBody> IStaticBody.Children => _children;
        bool IStaticBody.IsSatellite => _isSatellite;
        IStaticBody IStaticBodyAccessor.Self => this;
        double IStaticBody.GravParameter => settings.mass * MassUtility.G;
        IMassSystem IStaticBodyAccessor.MassSystem
        {
            get => _massSystem;
            set
            {
                _massSystem = value;
                settings.mass = value.Mass;
            }
        }

        IStaticBody IStaticBodyAccessor.Parent
        {
            get => _parent;
            set => _parent = value;
        }
        StaticOrbit IStaticBodyAccessor.Orbit
        {
            get => _orbit;
            set
            {
                _orbit = value;
                settings.inclination = _orbit.Inclination;
                settings.longitudeAscendingNode = _orbit.LongitudeAscendingNode;
                settings.argumentOfPeriapsis = _orbit.ArgumentOfPeriapsis;
                settings.eccentricity = _orbit.Eccentricity;
                settings.semiMajorAxis = _orbit.SemiMajorAxis;
                settings.epoch = _orbit.Epoch;
            }
        }

        IStaticBody[] IStaticBodyAccessor.Children
        {
            get => _children;
            set => _children = value;
        }
        bool IStaticBodyAccessor.IsSatellite
        {
            get => _isSatellite;
            set => _isSatellite = value;
        }
        World IStaticBodyAccessor.World
        {
            set => _world = value;
        }

        public DVector3 LocalPosition => _orbit.GetPositionAtT(TimeService.WorldTime);

        public double Mass => _massSystem.Mass;
        public StaticOrbit Orbit => _orbit;

        public override StaticBodySettings Settings
        {
            get => settings;
            set => settings = value;
        }

        public override StaticBodyVariables Variables
        {
            get => _variables;
            set => _variables = value;
        }
    }

    [Serializable]
    public struct StaticBodyVariables
    {
    }

    [Serializable]
    public struct StaticBodySettings
    {
        public double inclination;
        public double longitudeAscendingNode;
        public double argumentOfPeriapsis;
        public double eccentricity;
        public double semiMajorAxis;
        public double epoch;
        public double mass;
    }
}