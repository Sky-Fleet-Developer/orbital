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
        private StaticBodySettings _settings;
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
        IMassSystem IStaticBodyAccessor.MassSystem
        {
            get => _massSystem;
            set => _massSystem = value;
        }
        IStaticBody IStaticBodyAccessor.Parent
        {
            get => _parent;
            set => _parent = value;
        }
        StaticOrbit IStaticBodyAccessor.Orbit
        {
            get => _orbit;
            set => _orbit = value;
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
            get => _settings;
            set => _settings = value;
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
    }
}