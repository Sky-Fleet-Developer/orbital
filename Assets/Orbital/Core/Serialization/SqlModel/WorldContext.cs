using System.Collections.Generic;
using UnityEngine;

namespace Orbital.Core.Serialization.SqlModel
{
    public class WorldContext
    {
        private static ISerializer _serializer = new JsonPerformance();
        private World _world;

        public WorldContext()
        {
            _world = new GameObject("World").AddComponent<World>();
        }

        public WorldContext(World world)
        {
            _world = world;
        }

        public IEnumerable<IHierarchyElement> GetHierarchyElements()
        {
            return _world.GetComponentsInChildren<IHierarchyElement>();
        }

        public IEnumerable<IStaticBodyAccessor> GetStaticBodies()
        {
            return _world.GetComponentsInChildren<IStaticBodyAccessor>();
        }

        public IEnumerable<ISystemComponentAccessor> GetComponents()
        {
            return _world.GetComponentsInChildren<ISystemComponentAccessor>();
        }

        public Object Convert(IHierarchyElement hierarchyElement)
        {
            var g = ((MonoBehaviour) hierarchyElement).gameObject;
            var p = hierarchyElement.Transform.parent;
            return new Object
            {
                Id = hierarchyElement.Id,
                ParentId = p == null ? null : p.gameObject.GetInstanceID(),
                LocalPosition = _serializer.Serialize(hierarchyElement.LocalPosition),
                LocalRotation = "",
                Tag = g.tag,
                Layer = g.layer
            };
        }

        public Component Convert(ISystemComponentAccessor source)
        {
            return new Component
            {
                Id = source.Id,
                OwnerId = (source as MonoBehaviour).gameObject.GetInstanceID(),
                Type = source.GetType().AssemblyQualifiedName,
                Settings = _serializer.Serialize(source.Settings),
                Variables = _serializer.Serialize(source.Variables)
            };
        }

        public Player Convert(Core.Player source)
        {
            return new Player
            {
                Id = source.Id,
                Name = source.PlayerName,
                Position = _serializer.Serialize(source.transform.localPosition),
                Rotation = _serializer.Serialize(source.transform.localEulerAngles),
                ParentId = source.Parent.Id
            };
        }

        public Celestial Convert(IStaticBodyAccessor source)
        {
            IHierarchyElement hierarchyElement = (IHierarchyElement) source;
            return new Celestial
            {
                Id = source.Id,
                Mass = source.Settings.mass,
                Eccentricity = source.Settings.eccentricity,
                SemiMajorAxis = source.Settings.semiMajorAxis,
                Inclination = source.Settings.inclination,
                ArgumentOfPeriapsis = source.Settings.argumentOfPeriapsis,
                LongitudeAscendingNode = source.Settings.longitudeAscendingNode,
                Epoch = source.Settings.epoch,
                OwnerId = hierarchyElement.Id
            };
        }
    }
}