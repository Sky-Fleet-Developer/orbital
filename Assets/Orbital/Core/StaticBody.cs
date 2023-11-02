using System;
using System.Collections.Generic;
using Ara3D;
using Orbital.Core.Handles;
using Orbital.Core.TrajectorySystem;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Orbital.Core
{
    [ExecuteInEditMode]
    public class StaticBody : SystemComponent<StaticBodyVariables, StaticBodySettings>, IFixedUpdateHandler,
        IStaticBody, IStaticBodyAccessor
    {
        private StaticBodySettings _settings;
        [ShowInInspector] private StaticBodyVariables _variables;
        private World _world;
        private IMassSystem _massSystem;
        private IStaticOrbit _orbit;
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
        IStaticOrbit IStaticBodyAccessor.Orbit
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

        public DVector3 Position => _parent == null
            ? DVector3.Zero
            : _parent.Position + _orbit.GetPositionAtT(TimeService.WorldTime);

        public DVector3 LocalPosition => _orbit.GetPositionAtT(TimeService.WorldTime);

        public double Mass => _massSystem.Mass;
        public IStaticOrbit Orbit => _orbit;

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

        void IFixedUpdateHandler.FixedUpdate()
        {
            _variables.localPosition =
                _orbit?.GetSample(TimeService.WorldTime, true, false).position ?? DVector3.Zero;
        }
    }

    [Serializable]
    public struct StaticBodyVariables
    {
        public DVector3 localPosition;
    }

    [Serializable]
    public struct StaticBodySettings
    {
    }
}