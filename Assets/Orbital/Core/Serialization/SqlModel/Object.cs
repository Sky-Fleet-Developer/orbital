
using Orbital.Core.Serialization.Sqlite;

namespace Orbital.Core.Serialization.SqlModel
{
    public class Object
    {
        [PrimaryKey]
        public int Id { get; set; }
        public int? ParentId { get; set; }
        [DataType("VARCHAR (100)")]
        public string LocalPosition { get; set; }
        [DataType("VARCHAR (100)")]
        public string LocalRotation { get; set; }
        [DataType("VARCHAR (50)")]
        public string Tag { get; set; }
        public int Layer { get; set; }
        [Reference("Objects", "Id")]
        public virtual Object Parent { get; set; }
    }
}