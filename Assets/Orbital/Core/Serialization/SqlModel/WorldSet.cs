using System.Collections.Generic;
using System.Linq;
using Mono.Data.Sqlite;
using Orbital.Core.Serialization.Sqlite;
using UnityEngine;

namespace Orbital.Core.Serialization.SqlModel
{
    public class WorldSet : DbSet
    {
        private static ISerializer _serializer = new JsonPerformance();
        
        public TableSet<Player> Players = new ("Players");
        public TableSet<Object> Objects = new ("Objects");
        public TableSet<Component> Components = new ("Components");
        public TableSet<Celestial> Celestials = new ("Celestials");
        private string _connectionString;

        public WorldSet(Declaration declaration, string connectionString) : base(declaration)
        {
            _connectionString = connectionString;
        }

        public void Initialize()
        {
            using (SqliteConnection connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                connection.GetTable(Celestials, Declaration);
            }
        }

        public void WriteWorld(World world)
        {
            using (SqliteConnection connection = new SqliteConnection(_connectionString))
            {
                connection.Open();

                connection.GetTable(Objects, Declaration);
                Objects.Clear();

                foreach (IHierarchyElement hierarchyElement in world.GetComponentsInChildren<IHierarchyElement>())
                {
                    var model = Convert(hierarchyElement);
                    Objects.Add(model);
                }

                connection.GetTable(Celestials, Declaration);
                Celestials.Clear();
                foreach (IStaticBodyAccessor staticBodyAccessor in world.GetComponentsInChildren<IStaticBodyAccessor>())
                {
                    var model = Convert(staticBodyAccessor);
                    Celestials.Add(model);
                }

                connection.Update<Celestial>(Celestials, Declaration);
                connection.Update<Object>(Objects, Declaration);

                connection.GetTable(Components, Declaration);
                Components.Clear();

                foreach (ISystemComponentAccessor systemComponentAccessor in world
                    .GetComponentsInChildren<ISystemComponentAccessor>())
                {
                    var model = Convert(systemComponentAccessor);
                    Components.Add(model);
                }
                
                connection.Update<Component>(Components, Declaration);
            }
        }

        private Object Convert(IHierarchyElement hierarchyElement)
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

        public Object Convert(Transform source)
        {
            return new Object
            {
                Id = source.gameObject.GetInstanceID(),
                
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