using System;
using System.Collections.Generic;
using System.Linq;
using Ara3D;
using Orbital.Core;
using Orbital.Core.Handles;
using UnityEngine;
using Zenject;

namespace Orbital.View
{
    [RequireComponent(typeof(World))]
    public class WorldMapView : MonoBehaviour, IUpdateHandler
    {
        [SerializeField] private int celestialTrajectoryAccuracy = 200;
        [SerializeField] private float scale = 224400000;
        [SerializeField] private Material material;
        [Inject] private DiContainer _diContainer;
        private float _scaleI;
        private World _world;
        private Dictionary<MassSystemComponent, CelestialTrajectoryView> _celestials;
        private List<MassSystemComponent> _masses;
        private CircleViewBuffers _buffers;
        private void Awake()
        {
            _scaleI = 1 / scale;
            _buffers = new CircleViewBuffers();
            _buffers.PrepareBuffers(celestialTrajectoryAccuracy);
            _world = GetComponent<World>();
        }

        private void Start()
        {
            _masses = _world.GetComponentsInChildren<MassSystemComponent>().ToList();
            _celestials = new Dictionary<MassSystemComponent, CelestialTrajectoryView>();
            for (int i = 0; i < _masses.Count; i++)
            {
                if (_masses[i].Trajectory != null)
                {
                    _celestials.Add(_masses[i], new CelestialTrajectoryView(_masses[i].Trajectory, _buffers));
                }
            }
            _buffers.RefreshMatrices();
            
            HandlesRegister.RegisterHandlers(this, _diContainer);
        }

        private void OnValidate()
        {
            _scaleI = 1 / scale;
        }

        void IUpdateHandler.Update()
        {
            foreach (KeyValuePair<MassSystemComponent, CelestialTrajectoryView> celestialKv in _celestials)
            {
                var parent = _world.GetParent(celestialKv.Key);
                DVector3 globalPosition = parent == null ? DVector3.Zero : _world.GetGlobalPosition(parent);
                Vector3 positionScaled = (Vector3)(globalPosition) * _scaleI;
                celestialKv.Value.UpdateMatrix(positionScaled, _scaleI);
            }

            Render();
        }

        private void Render()
        {
            RenderParams rp = new RenderParams(material);
            rp.worldBounds = new Bounds(Vector3.zero, 10000*Vector3.one); // use tighter bounds
            rp.matProps = new MaterialPropertyBlock();
            rp.matProps.SetBuffer("_Positions", _buffers.Positions);
            rp.matProps.SetBuffer("_Matrices", _buffers.Matrices);
            rp.matProps.SetInt("_BaseVertexIndex", 0);
            Graphics.RenderPrimitivesIndexed(rp, MeshTopology.Lines, _buffers.Indices, _buffers.Indices.count, 0, _buffers.Matrices.count);
        }

        private void OnDestroy()
        {
            _buffers.ReleaseBuffers();
        }
    }
}
