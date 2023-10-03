using Orbital.Model;
using Orbital.Model.Handles;
using UnityEngine;
/*
namespace Orbital.Views.Components
{
    public class OrbitViewComponent : MonoBehaviour, IUpdateHandler
    {
        private OrbitSystemComponent _orbitSystem;
        
        public void Start()
        {
            _orbitSystem = MyBody.GetComponent<OrbitSystemComponent>();
        }

        public void Update()
        {
            Vector3 pos = _orbitSystem.LocalToWorldMatrix.GetPosition();
            Debug.DrawRay(Vector3.zero, pos);
        }

        public OrbitViewComponent(Body myBody) : base(myBody)
        {
        }
    }
}
*/