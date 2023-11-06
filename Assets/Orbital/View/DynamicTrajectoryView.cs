using System;
using Orbital.Core;
using Orbital.Core.Simulation;
using Orbital.View.Utilities;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Rendering;
using Zenject;

namespace Orbital.View
{
    [RequireComponent(typeof(IDynamicBody))]
    [ExecuteAlways]
    public class DynamicTrajectoryView : MonoBehaviour
    {
        [SerializeField] private Material material;
        [SerializeField] private int maxVertexCount = 300;
        private IDynamicBody _body;
        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private const string ViewName = "trajectory_view";
        private Transform _viewTransform;
        private Mesh _mesh;
        
        private void Start()
        {
            if(Application.isPlaying) Initialize();
        }

        private void OnEnable()
        {
            if(!Application.isPlaying) Initialize();
        }

        private void Initialize()
        {
            _body = GetComponent<IDynamicBody>();
            if (!Application.isPlaying)
            {
                GetComponentInParent<World>().Load();
                _body.Init();
            }
            
            //_body.Orbit.PathChangedHandler += Refresh;

            int searchIdx = transform.GetSiblingIndex() + 1;
            if (searchIdx < transform.parent.childCount)
            {
                _viewTransform = transform.parent.GetChild(searchIdx);
            }

            if (_viewTransform == null || _viewTransform.name != ViewName)
            {
                GameObject viewGameObject = new GameObject(ViewName, typeof(MeshFilter), typeof(MeshRenderer))
                {
                    hideFlags = HideFlags.DontSave
                };
                _viewTransform = viewGameObject.transform;
                _viewTransform.SetParent(transform.parent);
                _viewTransform.SetSiblingIndex(transform.GetSiblingIndex() + 1);
            }

            _mesh = MeshUtils.GenerateLineMesh("LineMesh", maxVertexCount);
            _viewTransform.GetComponent<MeshFilter>().mesh = _mesh;
            _viewTransform.GetComponent<MeshRenderer>().material = material;
        }

        private void OnDisable()
        {
            //_body.Orbit.PathChangedHandler -= Refresh;
        }

        

        private void Refresh()
        {
#if UNITY_EDITOR
            if(_body == null) return;
#endif
            Refresh(4.456328E-08F);
        }

        private bool _isRefreshInProgress = false;
        private void Refresh(float scale)
        {
            /*if(_isRefreshInProgress) return;
            _isRefreshInProgress = true;
            int length = Math.Min(maxVertexCount, _body.Orbit.Length);
            CopyVerticesJob copy = new ()
            {
                Marks = _body.Orbit.Path,
                Positions = new NativeArray<Vector3>(maxVertexCount, Allocator.TempJob),
                Scale = scale
            };
            JobHandle copyHandler = copy.Schedule(length, 32);
            int remains = maxVertexCount - _body.Orbit.Length;
            if (remains > 0)
            {
                ResetExcess reset = new()
                {
                    Positions = new NativeArray<Vector3>(remains, Allocator.TempJob),
                    Target = _body.Orbit.Path[_body.Orbit.Length - 1].Position * scale
                };
                reset.Schedule(remains, 32).Complete();
                _mesh.SetVertexBufferData(reset.Positions, 0, _body.Orbit.Length, remains, 0, MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontResetBoneBounds | MeshUpdateFlags.DontRecalculateBounds);
                reset.Positions.Dispose();
            }
            copyHandler.Complete();
            _mesh.SetVertexBufferData(copy.Positions, 0, 0, length, 0, MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontResetBoneBounds | MeshUpdateFlags.DontRecalculateBounds);
            copy.Positions.Dispose();
            _isRefreshInProgress = false;*/
        }

        private struct CopyVerticesJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<Mark> Marks;
            [WriteOnly] public NativeArray<Vector3> Positions;
            public float Scale;
            public void Execute(int index)
            {
                Positions[index] = ((Vector3)Marks[index].Position) * Scale;
            }
        }
        
        private struct ResetExcess : IJobParallelFor
        {
            public NativeArray<Vector3> Positions;
            public int Offset;
            public Vector3 Target;
            public void Execute(int index)
            {
                Positions[index + Offset] = Target;
            }
        }
    }
}
