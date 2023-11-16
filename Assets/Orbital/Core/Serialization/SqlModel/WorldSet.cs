using System.Collections.Generic;
using System.Linq;
using Mono.Data.Sqlite;
using Orbital.Core.Serialization.Sqlite;
using UnityEngine;

namespace Orbital.Core.Serialization.SqlModel
{
    public class WorldSet : DbSet
    {
        public TableSet<Player> Players = new ("Players");
        public TableSet<Object> Objects = new ("Objects");
        public TableSet<Component> Components = new ("Components");
        public TableSet<Celestial> Celestials = new ("Celestials");
        private string _connectionString;

        public WorldSet(Declaration declaration, string connectionString) : base(declaration)
        {
            _connectionString = connectionString;
        }

        public void WriteWorld(World world)
        {
            WorldContext context = new WorldContext(world);
            using (SqliteConnection connection = new SqliteConnection(_connectionString))
            {
                connection.Open();

                connection.GetTable(Objects, Declaration).Select(Objects.TableName).Run();
                Objects.Clear();

                foreach (IHierarchyElement hierarchyElement in context.GetHierarchyElements())
                {
                    var model = context.Convert(hierarchyElement);
                    Objects.Add(model);
                }

                connection.GetTable(Celestials, Declaration).Select(Celestials.TableName).Run();
                Celestials.Clear();
                foreach (IStaticBodyAccessor staticBodyAccessor in context.GetStaticBodies())
                {
                    var model = context.Convert(staticBodyAccessor);
                    Celestials.Add(model);
                }

                connection.Update<Celestial>(Celestials, Declaration);
                connection.Update<Object>(Objects, Declaration);

                connection.GetTable(Components, Declaration).Select(Components.TableName).Run();
                Components.Clear();

                foreach (ISystemComponentAccessor systemComponentAccessor in context.GetComponents())
                {
                    var model = context.Convert(systemComponentAccessor);
                    Components.Add(model);
                }
                
                connection.Update<Component>(Components, Declaration);
            }
        }

        public void LoadWorld()
        {
            //WorldContext context = new WorldContext();
            using (SqliteConnection connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                connection.GetTable(Objects, Declaration).Select(Objects.TableName).WhereIn("Id").Select(Celestials.TableName, "OwnerId").Run();
            }

            foreach (Object o in Objects)
            {
                Debug.Log(o.Id);
            }
        }
    }
}