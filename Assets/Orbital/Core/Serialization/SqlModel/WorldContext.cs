/*using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Sqlite;

namespace Orbital.Core.Serialization.SqlModel
{
    public class WorldContext : DbContext
    {
        public WorldContext() : base()
        {
            
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (optionsBuilder.IsConfigured == false)
            {
                optionsBuilder.UseSqlite("Data Source=DB/sqliteDb.db");
            }

            base.OnConfiguring(optionsBuilder);
        }

        public virtual DbSet<World> Worlds { get; set; }
        public virtual DbSet<Object> Objects { get; set; }
        public virtual DbSet<Component> Components { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<World>();
            modelBuilder.Entity<Object>();
            modelBuilder.Entity<Component>();
        }
    }
}*/