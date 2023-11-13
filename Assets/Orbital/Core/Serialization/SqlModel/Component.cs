﻿using Orbital.Core.Serialization.Sqlite;

namespace Orbital.Core.Serialization.SqlModel
{
    public class Component
    {
        [PrimaryKey]
        public int Id { get; set; }
        public int OwnerId { get; set; }
        [DataType("VARCHAR (200)")]
        public string Type { get; set; }
        public string Settings { get; set; }
        public string Variables { get; set; }
        [Reference("Objects", "Id")]
        public virtual Object Owner { get; set; }
    }
}