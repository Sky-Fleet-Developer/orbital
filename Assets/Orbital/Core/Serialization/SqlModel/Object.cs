﻿
using Orbital.Core.Serialization.Sqlite;

namespace Orbital.Core.Serialization.SqlModel
{
    public class Object : ModelBase
    {
        [PrimaryKey]
        public override int Id { get; set; }
        //public int CelestialId { get; set; }
        //public int ClusterId { get; set; }
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
        //[Reference("Celestials", "Id")]
        //public virtual Celestial Celestial { get; set; }
    }
}