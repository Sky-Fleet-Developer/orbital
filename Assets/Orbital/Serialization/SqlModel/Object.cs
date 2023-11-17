
using Ara3D;
using Orbital.Core.Serialization.Sqlite;
using UnityEngine;

namespace Orbital.Serialization.SqlModel
{
    public class Object : ModelBase
    {
        [PrimaryKey]
        public override int Id { get; set; }
        //public int CelestialId { get; set; }
        //public int ClusterId { get; set; }
        [DataType("VARCHAR (100)")]
        public string Name { get; set; }
        public int? ParentId { get; set; }
        public double PosX { get; set; }
        public double PosY { get; set; }
        public double PosZ { get; set; }
        public float RotX { get; set; }
        public float RotY { get; set; }
        public float RotZ { get; set; }
        [DataType("VARCHAR (50)")]
        public string Tag { get; set; }
        public int Layer { get; set; }
        [Reference("Objects", "Id")]
        public virtual Object Parent { get; set; }

        public void SetPosition(Vector3 value)
        {
            PosX = value.x;
            PosY = value.y;
            PosZ = value.z;
        }
        public void SetPosition(DVector3 value)
        {
            PosX = value.x;
            PosY = value.y;
            PosZ = value.z;
        }
        public Vector3 GetPosition()
        {
            return new Vector3((float)PosX, (float)PosY, (float)PosZ);
        }
        public DVector3 GetPositionD()
        {
            return new DVector3(PosX, PosY, PosZ);
        }
        public void SetRotation(Vector3 value)
        {
            RotX = value.x;
            RotY = value.y;
            RotZ = value.z;
        }
        public Vector3 GetRotation()
        {
            return new Vector3(RotX, RotY, RotZ);
        }
        //[Reference("Celestials", "Id")]
        //public virtual Celestial Celestial { get; set; }
    }
}