using System.Xml.Serialization;
using Sirenix.OdinInspector;

namespace Orbital.Model
{
    public abstract class Component
    {
        private static int _idCounter;
        [ShowInInspector] private int _id;
        public int Id => _id;

        public Body MyBody { get; }

        public Component(Body myBody)
        {
            MyBody = myBody;
            _id = _idCounter++;
        }
        
        public virtual void Start(){}
    }
}
