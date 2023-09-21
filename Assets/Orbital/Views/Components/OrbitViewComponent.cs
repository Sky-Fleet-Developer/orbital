using Orbital.Model;
using Orbital.Model.Components;
using Orbital.Model.Handles;
using UnityEngine;
using Component = Orbital.Model.Component;

namespace Orbital.Views.Components
{
    public class OrbitViewComponent : Component, IUpdateHandler
    {
        private OrbitComponent _orbit;
        
        public override void Start()
        {
            _orbit = MyBody.GetComponent<OrbitComponent>();
        }

        public void Update()
        {
            Vector3 pos = _orbit.LocalToWorldMatrix.GetPosition();
            Debug.DrawRay(Vector3.zero, pos);
        }

        public OrbitViewComponent(Body myBody) : base(myBody)
        {
        }
    }
}
