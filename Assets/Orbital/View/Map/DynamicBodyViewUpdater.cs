using System;
using System.Collections.Generic;
using System.Linq;
using Ara3D;
using Orbital.Core;
using Orbital.Core.Navigation;
using Orbital.Core.Simulation;
using Orbital.Core.TrajectorySystem;
using Orbital.View.Utilities;
using Plugins.LocalPool;
using ScriptableObjectContainer;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Orbital.View.Map
{
    public class DynamicBodyViewUpdater : ViewUpdater
    {
        private ViewContainer _self;
        private ViewContainer _orbit;
        private ViewContainer _startNode;
        private ViewContainer _endNode;
        private Mesh _orbitMesh;
        private IDynamicBody _body;
        private StaticOrbit _oldOrbit;
        private OrbitEnding _ending;
        
        private ScaleSettings _scaleSettings;
        private DynamicBodyViewSettings _viewSettings;
        private Transform _parent;
        private Dictionary<IStaticBody, Transform> _hierarchy;

        public DynamicBodyViewUpdater(IDynamicBody model, Dictionary<IStaticBody, Transform> hierarchy, Pool<ViewContainer> pool, Container settingsContainer, ScaleSettings scaleSettings)
        {
            _body = model;
            _hierarchy = hierarchy;
            _scaleSettings = scaleSettings;
            _viewSettings = settingsContainer.GetAssetsAtType<DynamicBodyViewSettings>().First();
            _parent = _hierarchy[_body.Parent];
            _self = pool.Get();
            SetupView(_self, _viewSettings.selfMesh, _viewSettings.selfMaterial);
            _self.Transform.localScale = Vector3.one * 0.05f;
            _orbitMesh = MeshUtils.GenerateLineMesh("LineMesh", _viewSettings.orbitMaxVerticesCount, false);
            _orbit = pool.Get();
            SetupView(_orbit, _orbitMesh, _viewSettings.orbitMaterial);
            _body.OrbitChangedHandler += OnOrbitChanged;
            _startNode = pool.Get();
            SetupView(_startNode, _viewSettings.nodeMesh, _viewSettings.nodeMaterial);
            _startNode.Transform.localScale = Vector3.one * 0.05f;
            _endNode = pool.Get();
            SetupView(_endNode, _viewSettings.nodeMesh, _viewSettings.nodeMaterial);
            _endNode.Transform.localScale = Vector3.one * 0.05f;
            OnOrbitChanged();
        }

        private void SetupView(ViewContainer container, Mesh mesh, Material material)
        {
            container.Transform.SetParent(_parent);
            container.MeshRenderer.material = material;
            container.MeshFilter.mesh = mesh;
        }

        private void OnOrbitChanged()
        {
            _oldOrbit = _body.Orbit;
            _parent = _hierarchy[_body.Parent];
            _self.Transform.parent = _parent;
            _orbit.Transform.parent = _parent;
            _orbit.Transform.localPosition = Vector3.zero;
            _startNode.Transform.parent = _parent;
            _endNode.Transform.parent = _parent;
            _ending = _body.Orbit.GetEnding(_body.Parent, _body.Orbit.Epoch);
            AlignOrbitVertices();
            switch (_ending.Type)
            {
                case OrbitEndingType.Cycle:
                    _startNode.Transform.gameObject.SetActive(false);
                    _endNode.Transform.gameObject.SetActive(false);
                    break;
                case OrbitEndingType.Leave:
                    _startNode.Transform.gameObject.SetActive(true);
                    _endNode.Transform.gameObject.SetActive(true);
                    _startNode.Transform.localPosition = _body.Orbit.GetPositionAtT(_body.Orbit.TimeToPe)
                                                         * _scaleSettings.scale;
                    _endNode.Transform.localPosition = _body.Orbit.GetPositionAtT(_ending.Time)
                                                       * _scaleSettings.scale;
                    break;
                case OrbitEndingType.Entry:
                    _startNode.Transform.gameObject.SetActive(false);
                    _endNode.Transform.gameObject.SetActive(true);
                    _endNode.Transform.localPosition = _body.Orbit.GetPositionAtT(_ending.Time)
                                                       * _scaleSettings.scale;
                    break;
            }
        }

        public override void Update(double time)
        {
            if (!_oldOrbit.Equals(_body.Orbit))
            {
                OnOrbitChanged();
            }
            _self.Transform.localPosition = _body.Orbit.GetPositionAtT(time) * _scaleSettings.scale;
            _self.Transform.localRotation = Quaternion.LookRotation(_body.Orbit.GetOrbitalVelocityAtOrbitTime(time));
        }
        
        public override void Dispose(Pool<ViewContainer> pool)
        {
            _body.OrbitChangedHandler -= OnOrbitChanged;
            Object.DestroyImmediate(_orbitMesh);
            pool.Put(_self);
            pool.Put(_orbit);
            pool.Put(_startNode);
            pool.Put(_endNode);
        }

        private void AlignOrbitVertices()
        {
            double startTime, endTime;
            switch (_ending.Type)
            {
                case OrbitEndingType.Cycle: case OrbitEndingType.Entry:
                    startTime = _body.Orbit.Epoch;
                    if (_body.Orbit.Eccentricity < 1)
                    {
                        endTime = startTime + _body.Orbit.Period;
                    }
                    else
                    {
                        endTime = _ending.Time;
                    }
                    break;
                case OrbitEndingType.Leave:
                    endTime = _ending.Time;
                    startTime = _body.Orbit.TimeToPe; //_body.Orbit.Epoch - (endTime - _body.Orbit.Epoch);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            double step = (endTime - startTime) / (_viewSettings.orbitMaxVerticesCount - 1);
            //var positions = new NativeArray<Vector3>(_viewSettings.orbitMaxVerticesCount, Allocator.Temp);
            /*for (int i = 0; i < _viewSettings.orbitMaxVerticesCount; i++)
            {
                positions[i] = _body.Orbit.GetPositionAtT(startTime + step * i) * _scaleSettings.scale;
            }*/
            var job = new AlignVerticesJob
            {
                Orbit = _body.Orbit,
                StartTime = startTime,
                EndTime = endTime,
                Step = step,
                Scale = _scaleSettings.scale,
                Positions = new NativeArray<Vector3>(_viewSettings.orbitMaxVerticesCount, Allocator.TempJob)
            };

            var handler = job.Schedule(_viewSettings.orbitMaxVerticesCount, 32);
            handler.Complete();
            
            _orbitMesh.SetVertices(job.Positions);
            _orbitMesh.RecalculateBounds();
            job.Positions.Dispose();
        }
        
        private struct AlignVerticesJob : IJobParallelFor
        {
            public StaticOrbit Orbit;
            public double StartTime;
            public double EndTime;
            public double Step;
            public float Scale;
            [WriteOnly]
            public NativeArray<Vector3> Positions;

            public void Execute(int index)
            {
                Positions[index] = Orbit.GetPositionAtT(StartTime + Step * index) * Scale;
            }
        }
    }
}