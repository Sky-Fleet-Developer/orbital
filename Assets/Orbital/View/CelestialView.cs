using Orbital.Core;
using Orbital.Core.TrajectorySystem;
using UnityEngine;

namespace Orbital.View
{
    //[RequireComponent(typeof(MeshRenderer), typeof(MeshFilter))]
    [ExecuteInEditMode]
    public class CelestialView : MonoBehaviour
    {
        //private MeshFilter _meshFilter;
        //private MeshRenderer _meshRenderer;
        private IStaticBody _body;
        private void Awake()
        {
            if (!Application.isPlaying)
            {
                GetComponentInParent<World>().Load();
            }
            _body = GetComponent<IStaticBody>();
        }
        
        void Start()
        {
            //_meshFilter = GetComponent<MeshFilter>();
            //_meshRenderer = GetComponent<MeshRenderer>();
        }

        private void Update()
        {
#if UNITY_EDITOR
            if (_body == null)
            {
                Awake();
                return;
            }
#endif
        }
        
        private void OnDrawGizmos()
        {
            float scale = 4.456328E-09F;
            Gizmos.DrawSphere(_body.Position * scale, 0.1f);
        }
    }
}
