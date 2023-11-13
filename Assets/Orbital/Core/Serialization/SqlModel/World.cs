
using System;
using Orbital.Core.Serialization.Sqlite;
using Sirenix.OdinInspector;

namespace Orbital.Core.Serialization.SqlModel
{
    [Serializable]
    public class World
    {
        [PrimaryKey, ShowInInspector]
        public int Id { get; set; }
        [DataType("VARCHAR (50)"), ShowInInspector]
        public string Name { get; set; }
        [DataType("VARCHAR (100)"), ShowInInspector]
        public string PlayerPosition { get; set; }
        [DataType("VARCHAR (100)"), ShowInInspector]
        public string PlayerRotation { get; set; }
        [ShowInInspector] public int PlayerParentId { get; set; }
        [Reference("Objects", "Id")]
        public virtual Object PlayerParent { get; set; }
    }
}