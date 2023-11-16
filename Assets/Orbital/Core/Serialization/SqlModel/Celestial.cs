using Orbital.Core.Serialization.Sqlite;

namespace Orbital.Core.Serialization.SqlModel
{
    public class Celestial : ModelBase
    {
        [PrimaryKey]
        public override int Id { get; set; }
        [DataType("VARCHAR (500)")]
        public double Mass { get; set; }
        public double Eccentricity { get; set; }
        public double SemiMajorAxis { get; set; }
        public double Inclination { get; set; }
        public double ArgumentOfPeriapsis { get; set; }
        public double LongitudeAscendingNode { get; set; }
        public double Epoch { get; set; }
        public int? ParentId { get; set; }
        [Reference("Objects", "Id")]
        public virtual Object Parent { get; set; }

    }
}