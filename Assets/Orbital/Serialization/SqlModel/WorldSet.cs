using Mono.Data.Sqlite;
using Orbital.Core;
using Orbital.Core.Serialization.Sqlite;
using Zenject;

namespace Orbital.Serialization.SqlModel
{
    public class WorldSet : DbSet
    {
        public TableSet<Player> Players = new ("Players");
        public TableSet<Object> Objects = new ("Objects");
        public TableSet<Component> Components = new ("Components");
        public TableSet<Celestial> Celestials = new ("Celestials");
        private string _connectionString;
        [Inject] private DiContainer _container;

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
                connection.GetTable(Objects, Declaration, Sql.Select(Objects.TableName, Sql.All()));
                Objects.Clear();

                foreach (IHierarchyElement hierarchyElement in context.GetHierarchyElements())
                {
                    var model = context.Convert(hierarchyElement);
                    Objects.Add(model);
                }

                connection.GetTable(Celestials, Declaration, Sql.Select(Celestials.TableName, Sql.All()));
                Celestials.Clear();
                foreach (IStaticBodyAccessor staticBodyAccessor in context.GetStaticBodies())
                {
                    var model = context.Convert(staticBodyAccessor);
                    Celestials.Add(model);
                }

                connection.Update<Celestial>(Celestials, Declaration);
                connection.Update<Object>(Objects, Declaration);

                connection.GetTable(Components, Declaration, Sql.Select(Components.TableName, Sql.All()));
                Components.Clear();

                foreach (ISystemComponentAccessor systemComponentAccessor in context.GetComponents())
                {
                    var model = context.Convert(systemComponentAccessor);
                    Components.Add(model);
                }
                
                connection.Update<Component>(Components, Declaration);
            }
        }

        public WorldContext LoadWorld()
        {
            WorldContext context = new WorldContext();
            _container.Inject(context);
            using (SqliteConnection connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                connection.GetTable(Objects, Declaration, Sql.Select(Objects.TableName, Sql.All()).Value + Sql.WhereIn("Id", Sql.Select(Celestials.TableName, "OwnerId")));
                connection.GetTable(Celestials, Declaration, Sql.Select(Celestials.TableName, Sql.All()));
            }

            context.InstallObjects(Objects);
            context.InstallCelestials(Celestials);
            _container.Inject(context.World);
            context.InitWorld();
            return context;
        }

        public void InstantiatePlayer(WorldContext context, int playerId)
        {
            using (SqliteConnection connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                connection.GetTable(Players, Declaration, Sql.Select(Players.TableName, Sql.Where($"Id == {playerId}")));
                var recursiveExp = Sql.WithRecursive("h", Sql.AllFields(Objects, Declaration),
                    Sql.Select(Objects.TableName, Sql.All()),
                    Sql.Select(Objects.TableName, "e.*") + "JOIN h eh ON e.Id = eh.ParentId");
                connection.GetTable(Objects, Declaration,  recursiveExp + Sql.Select("h", Sql.All()));
            }

            context.InstallPlayers(Players, Declaration);
        }
        
        public void InstantiateComponentsNearToPlayer(WorldContext context, int playerId)
        {
            
        }
    }
}