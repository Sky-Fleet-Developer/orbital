using Orbital.Core.Serialization.Sqlite;

namespace Orbital.Serialization.SqlModel
{
    public class Component : ModelBase
    {
        [PrimaryKey]
        public override int Id { get; set; }
        public int OwnerId { get; set; }
        [DataType("VARCHAR (200)")]
        public string Type { get; set; }
        public string Settings { get; set; }
        public string Variables { get; set; }
        [Reference("Objects", "Id")]
        public virtual Object Owner { get; set; }
    }
}