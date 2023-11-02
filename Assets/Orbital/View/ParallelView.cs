using System;
using UnityEngine;

namespace Orbital.View
{
    public abstract class ParallelView : MonoBehaviour
    {
        protected MeshFilter MeshFilter { get; private set; }
        protected MeshRenderer MeshRenderer { get; private set; }
        protected Transform ViewTransform { get; private set; }
        protected abstract Mesh GetMesh();
        protected abstract Material GetMaterial();
        private const string ViewName = "parallel_view";

        private void Start()
        {
            if(Application.isPlaying) Initialize();
        }

        private void OnEnable()
        {
            if(!Application.isPlaying) Initialize();
        }

        protected virtual void OnInitialize()
        {
            
        }
        
        private void Initialize()
        {
            int searchIdx = transform.GetSiblingIndex() + 1;
            if (searchIdx < transform.parent.childCount)
            {
                ViewTransform = transform.parent.GetChild(searchIdx);
            }

            if (ViewTransform == null || ViewTransform.name != ViewName)
            {
                GameObject viewGameObject = new GameObject(ViewName, typeof(MeshFilter), typeof(MeshRenderer))
                {
                    hideFlags = HideFlags.DontSave
                };
                ViewTransform = viewGameObject.transform;
                ViewTransform.SetParent(transform.parent);
                ViewTransform.SetSiblingIndex(transform.GetSiblingIndex() + 1);
            }

            OnInitialize();
            ViewTransform.GetComponent<MeshFilter>().mesh = GetMesh();
            ViewTransform.GetComponent<MeshRenderer>().material = GetMaterial();
        }
    }
}