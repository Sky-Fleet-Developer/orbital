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

        public WorldContext LoadWorld()
        {
            WorldContext context = new WorldContext();
            _container.Inject(context);
            using (SqliteConnection connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                connection.GetTable(Objects, Declaration)
                    .Select(Objects.TableName)
                    .WhereIn("Id").Select(Celestials.TableName, "OwnerId")
                    .Run();
                connection.GetTable(Celestials, Declaration)
                    .Select(Celestials.TableName)
                    .Run();
            }

            context.InstallObjects(Objects);
            context.InstallCelestials(Celestials);
            _container.Inject(context.World);
            context.InitWorld();
            return context;
        }

        public Core.PlayerCharacter InstantiatePlayer(WorldContext context, int playerId)
        {
            using (SqliteConnection connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                connection.GetTable(Players, Declaration).Select(Players.TableName).Where($"Id == {playerId}").Run();
                connection.GetTable(Objects, Declaration).Select(Objects.TableName).Where($"Id == {Players[0].ParentId}");
            }

            context.InstallPlayers(Players, Declaration);
        }
    }
}