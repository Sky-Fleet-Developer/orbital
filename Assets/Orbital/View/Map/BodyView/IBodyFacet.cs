using System;
using System.Reflection;

namespace Orbital.View.Map
{
    /// <summary>
    /// A small piece of view data a body may or may not have (look, surface, atmosphere...).
    /// Every implementation declares <c>public const int TypeId</c>, unique and below <see cref="BodyViewRecord.MaxFacets"/>,
    /// and returns it from <see cref="Id"/>.
    /// </summary>
    public interface IBodyFacet
    {
        int Id { get; }
    }

    /// <summary>The <c>TypeId</c> constant of a facet type, read once per type.</summary>
    public static class FacetId<T> where T : IBodyFacet
    {
        public static readonly int Value = Read();

        private static int Read()
        {
            FieldInfo field = typeof(T).GetField("TypeId", BindingFlags.Public | BindingFlags.Static);
            if (field == null || !field.IsLiteral || field.FieldType != typeof(int))
            {
                throw new InvalidOperationException($"{typeof(T).Name} must declare 'public const int TypeId'");
            }
            return (int) field.GetRawConstantValue();
        }
    }
}
