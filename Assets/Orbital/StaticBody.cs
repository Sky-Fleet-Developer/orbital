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
    public class StaticBody : MonoBehaviour, IStaticBody, IHierarchyElement, IStaticBodyAccessor
    {
        [SerializeField] private TrajectorySettings settings;
        private World _world;
        private IMassSystem _massSystem;
        private StaticOrbit _orbit;
        [ShowInInspector] private IStaticBody _parentCelestial;
        [ShowInInspector] private IStaticBody[] _children;
        private bool _isSatellite;
        private int _id;
        private int _ownerId;

        #region InterfaceImplementation
        string IHierarchyElement.Name => name;
        int IHierarchyElement.ParentId => transform.parent.gameObject.GetInstanceID();
        Transform IHierarchyElement.Transform => transform;
        public Vector3 LocalEulerAngles => Vector3.zero;
        public IMassSystem MassSystem => _massSystem;
        public IStaticBody ParentCelestial => _parentCelestial;
        public IEnumerable<IStaticBody> Children => _children;
        public bool IsSatellite => _isSatellite;
        public double GravParameter => settings.mass * MassUtility.G;

        int IHierarchyElement.Id
        {
            get
            {
                if (_ownerId == 0)
                {
                    _ownerId = gameObject.GetInstanceID();
                }
                return _ownerId;
            }
            set
            {
                if (_ownerId != 0)
                {
                    throw new Exception("Can't write Id twice");
                }
                _ownerId = value;
            }
        }
        [ShowInInspector, ReadOnly] int IStaticBodyAccessor.Id
        {
            get
            {
                if (_id == 0)
                {
                    _id = GetInstanceID();
                }
                return _id;
            }
            set
            {
                if (_id != 0)
                {
                    throw new Exception("Can't write Id twice");
                }
                _id = value;
            }
        }

        IStaticBody IStaticBodyAccessor.Self => this;
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
            get => _parentCelestial;
            set => _parentCelestial = value;
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

        TrajectorySettings IStaticBodyAccessor.Settings
        {
            get => settings;
            set => settings = value;
        }
        World IStaticBodyAccessor.World
        {
            set => _world = value;
        }
        #endregion


        public DVector3 LocalPosition => _orbit.GetPositionAtT(TimeService.WorldTime);

        public double Mass => _massSystem.Mass;
        public StaticOrbit Orbit => _orbit;
    }

}