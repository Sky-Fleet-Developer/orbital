using System;
using Orbital.Core;
using Orbital.Core.Simulation;
using Orbital.Core.TrajectorySystem;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Rendering;

namespace Orbital.View
{
    [RequireComponent(typeof(IDynamicBody))]
    [ExecuteAlways]
    public class StaticTrajectoryDynamicView : ParallelView
    {
        private const double Deg2Rad = 0.01745329;
        private const double Rad2Deg = 57.29578;
        [SerializeField] private Material material;
        [SerializeField] private int accuracy;
        [SerializeField] private int dr;
        private IDynamicBody _body;
        private Mesh _mesh;
        private NativeArray<Vector3> _positions;
        private bool _isRefreshInProgress = false;

        protected override Mesh GetMesh()
        {
            _mesh = new Mesh {name = "Orbit line"};
            _mesh.MarkDynamic();
            _mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1000);
            
            {
                _positions = new NativeArray<Vector3>(accuracy, Allocator.Persistent);
                for (int i = 0; i < accuracy; i++)
                {
                    _positions[i] = Vector3.zero;
                }
                _mesh.SetVertices(_positions);
            }
            {
                int[] arr = new int[accuracy * 2 - 1];
                for (int i = 0; i < accuracy - 1; i++)
                {
                    arr[i * 2] = i;
                    arr[i * 2 + 1] = i + 1;
                }
                //arr[accuracy * 2 - 2] = accuracy - 1;
                //arr[accuracy * 2 - 1] = 0;
                _mesh.SetIndices(arr, MeshTopology.Lines, 0);
            }
            return _mesh;
        }

        protected override Material GetMaterial() => material;

        protected override void OnInitialize()
        {
            _body = GetComponent<IDynamicBody>();
            if (!Application.isPlaying)
            {
                GetComponentInParent<World>().Load();
                _body.Init();
            }

            _body.Orbit.WasChangedHandler += Refresh;
        }
        
        private void Refresh()
        {
#if UNITY_EDITOR
            if(_body == null) return;
#endif
            Refresh(4.456328E-08F);
        }

        private static AsyncThreadScheduler _copyVerticesScheduler = new AsyncThreadScheduler(3);
        private async void Refresh(float scale)
        {
            if(_isRefreshInProgress) return;
            _isRefreshInProgress = true;

            await _copyVerticesScheduler.Schedule(() =>
            {
                if (_body.Orbit.Eccentricity < 0.999)
                {
                    //float indexMp = Math.PI / (accuracy - f1);
                    for (int i = 0; i < _positions.Length; i++)
                    {
                        float val = i / (accuracy - 1f) * 2 - 1;
                        val = Mathf.Pow(Mathf.Abs(val), 1.2f) * Math.Sign(val);
                        _positions[i] = _body.Orbit.GetPositionFromEccAnomaly(val * Math.PI) * scale;
                    }
                }
                else
                {
                    float indexMp = (float) Math.PI * 2 / (accuracy - 1);
                    float start = (float)(-Math.Acos(-(1.0 / Math.Max(_body.Orbit.Eccentricity, 1))) + dr * (Math.PI / 180.0));
                    float end = -start;
                    for (int i = 0; i < _positions.Length; i++)
                    {
                        _positions[i] = _body.Orbit.GetPositionFromTrueAnomaly(Mathf.Lerp(start, end, i / (accuracy - 1f))) * scale;
                    }
                   /*double eccentricity = _body.Orbit.Eccentricity;
                    int i = 0;
                    for (double tA = -Math.Acos(-(1.0 / eccentricity)) + accuracy * (Math.PI / 180.0);
                        tA < Math.Acos(-(1.0 / eccentricity)) - accuracy * (Math.PI / 180.0);
                        tA += accuracy * (Math.PI / 180.0))
                    {
                        _positions[i++] = _body.Orbit.GetPositionFromTrueAnomaly(tA) * scale;
                    }*/

                    /*float m = (float)Math.Sqrt(_body.Parent.GravParameter / 0.001);
                    _positions[0] = _positions[0].normalized * m;
                    _positions[accuracy - 1] = _positions[accuracy - 1].normalized * m;*/
                }
            });
            
            _mesh.SetVertices(_positions);
            _isRefreshInProgress = false;
        }
        
        private void OnDisable()
        {
            _body.Orbit.WasChangedHandler -= Refresh;
            _positions.Dispose();
        }

        
        private struct CopyVerticesJob : IJobParallelFor
        {
            public IStaticOrbit Orbit;
            [WriteOnly] public NativeArray<Vector3> Positions;
            public float Scale;
            public float IndexMp;
            public void Execute(int index)
            {
                Positions[index] = Orbit.GetPositionFromTrueAnomaly(index * IndexMp) * Scale;
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
