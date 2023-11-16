
using System;
using Orbital.Core.Serialization.Sqlite;
using Sirenix.OdinInspector;

namespace Orbital.Core.Serialization.SqlModel
{
    [Serializable]
    public class Player : ModelBase
    {
        [PrimaryKey, ShowInInspector]
        public override int Id { get; set; }
        [DataType("VARCHAR (50)"), ShowInInspector]
        public string Name { get; set; }
        [DataType("VARCHAR (100)"), ShowInInspector]
        public string Position { get; set; }
        [DataType("VARCHAR (100)"), ShowInInspector]
        public string Rotation { get; set; }
        [ShowInInspector] public int ParentId { get; set; }
        [Reference("Objects", "Id")]
        public virtual Object Parent { get; set; }
    }
}