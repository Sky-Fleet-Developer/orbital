using System.Collections.Generic;
using Orbital.Core;
using Orbital.Core.Serialization;
using Orbital.Core.Serialization.Sqlite;
using UnityEngine;
using Zenject;
using Component = Orbital.Serialization.SqlModel.Component;
using Object = Orbital.Serialization.SqlModel.Object;

namespace Orbital.Serialization.SqlModel
{
    public class WorldContext
    {
        private static ISerializer _serializer = new JsonPerformance();
        private World _world;
        private Dictionary<int, Transform> _objects = new();
        private Dictionary<int, IStaticBodyAccessor> _celestials = new();
        private Dictionary<int, PlayerCharacter> _players = new();
        [Inject] private IFactory<GameObject, IStaticBodyAccessor> _staticBodyFactory;
        [Inject] private IFactory<PlayerCharacter> _playerFactory;

        public PlayerCharacter GetPlayer(int id)
        {
            return _players[id];
        }
        
        public World World => _world;

        public WorldContext()
        {
            _world = new GameObject("World").AddComponent<World>();
        }

        public WorldContext(World world)
        {
            _world = world;
            foreach (IHierarchyElement hierarchyElement in GetHierarchyElements())
            {
                _objects.Add(hierarchyElement.Id, hierarchyElement.Transform);
            }
            foreach (IStaticBodyAccessor staticBodyAccessor in GetStaticBodies())
            {
                _celestials.Add(staticBodyAccessor.Id, staticBodyAccessor);
            }
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
            int? p = hierarchyElement.ParentId;
            if (p == _world.gameObject.GetInstanceID()) p = null;
            var o = new Object
            {
                Id = hierarchyElement.Id,
                Name = hierarchyElement.Name,
                ParentId = p == null ? null : p,
                Tag = g.tag,
                Layer = g.layer
            };
            o.SetPosition(hierarchyElement.LocalPosition);
            o.SetRotation(hierarchyElement.LocalEulerAngles);
            return o;
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

        public Player Convert(Core.PlayerCharacter source)
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
                MyType = source.GetType().AssemblyQualifiedName,
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

        public void InstallObjects(TableSet<Object> table)
        {
            foreach (Object o in table)
            {
                if (_objects.ContainsKey(o.Id)) continue;
                
                Transform instance = new GameObject(o.Name).transform;
                instance.gameObject.layer = o.Layer;
                instance.gameObject.tag = o.Tag;
                instance.localPosition = o.GetPosition();
                instance.localEulerAngles = o.GetRotation();
                _objects.Add(o.Id, instance);
            }

            foreach (Object o in table)
            {
                Transform parent = o.ParentId.HasValue ? _objects[o.ParentId.Value] : _world.transform;
                _objects[o.Id].SetParent(parent, false);
            }
        }

        public void InstallCelestials(TableSet<Celestial> table)
        {
            foreach (Celestial celestial in table)
            {
                if (_celestials.ContainsKey(celestial.Id)) continue;
                
                Transform transform = _objects[celestial.OwnerId];

                IStaticBodyAccessor instance = _staticBodyFactory.Create(transform.gameObject);//transform.gameObject.AddComponent(Type.GetType(celestial.MyType)) as IStaticBodyAccessor;
                instance.Id = celestial.Id;
                IHierarchyElement hierarchyElement = (IHierarchyElement) instance;
                hierarchyElement.Id = celestial.OwnerId;
                var s = instance.Settings;
                s.mass = celestial.Mass;
                s.eccentricity = celestial.Eccentricity;
                s.semiMajorAxis = celestial.SemiMajorAxis;
                s.inclination = celestial.Inclination;
                s.argumentOfPeriapsis = celestial.ArgumentOfPeriapsis;
                s.longitudeAscendingNode = celestial.LongitudeAscendingNode;
                s.epoch = celestial.Epoch;
                instance.Settings = s;
            }
        }

        public void InitWorld()
        {
            _world.Load(false);
        }

        public void InstallPlayers(TableSet<Player> table, Declaration declaration)
        {
            foreach (Player player in table)
            {
                if (_players.ContainsKey(player.Id)) continue;
                
                PlayerCharacter instance = _playerFactory.Create();
                instance.Id = player.Id;
                instance.PlayerName = player.Name;
                instance.transform.SetParent(_objects[player.ParentId]);
                instance.transform.localPosition = _serializer.Deserialize<Vector3>(player.Position);
                instance.transform.localEulerAngles = _serializer.Deserialize<Vector3>(player.Rotation);
                _players.Add(player.Id, instance);
            }
        }
    }
}