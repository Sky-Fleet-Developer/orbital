using System;
using JetBrains.Annotations;

namespace Orbital.Core.Serialization
{
    public interface ISerializer
    {
        public string Serialize(object value);
        public void Populate<T>(T target, string value);
        public T Deserialize<T>(string value);
        [CanBeNull] public object Deserialize(Type type, string value);

    }
}
