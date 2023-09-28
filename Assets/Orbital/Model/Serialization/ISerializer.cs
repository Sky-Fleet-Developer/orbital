using System;

namespace Orbital.Controllers.Data
{
    public interface ISerializer
    {
        public string Serialize(object value);
        public void Populate<T>(T target, string value);
    }
}
