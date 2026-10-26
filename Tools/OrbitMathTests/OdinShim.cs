using System;

// Stand-in for the Odin Inspector attribute used by Ara3D structs, so they compile without Unity.
namespace Sirenix.OdinInspector
{
    [AttributeUsage(AttributeTargets.All)]
    public class HorizontalGroupAttribute : Attribute
    {
        public float LabelWidth { get; set; }
        public HorizontalGroupAttribute(params object[] args) { }
    }
}
