using System;
using System.Collections.Generic;
using System.Linq;
using Ara3D;
using Orbital.Core;
using Orbital.Core.Handles;
using Orbital.Core.Simulation;
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
        [SerializeField] private int bodyTrajectoryMaxBufferSize = 400;
        [Inject] private DiContainer _diContainer;
        private float _scaleI;
        private World _world;
        private Dictionary<MassSystemComponent, CelestialTrajectoryView> _celestials;
        private Dictionary<IRigidBody, BodyTrajectoryView> _bodies;
        private CircleViewBuffers _circleBuffers;
        private OffsetsBuffer _offsetsBuffer;

        private void Awake()
        {
            _scaleI = 1 / scale;
            _circleBuffers = new CircleViewBuffers();
            _circleBuffers.PrepareBuffers(celestialTrajectoryAccuracy);
            _offsetsBuffer = new OffsetsBuffer();
            _world = GetComponent<World>();
        }

        private void Start()
        {
            MassSystemComponent[] masses = _world.GetComponentsInChildren<MassSystemComponent>();
            _celestials = new Dictionary<MassSystemComponent, CelestialTrajectoryView>();
            foreach (MassSystemComponent mass in masses)
            {
                if (mass.Trajectory != null)
                {
                    _celestials.Add(mass, new CelestialTrajectoryView(mass.Trajectory, _circleBuffers));
                }
            }
            _circleBuffers.Refresh();

            IRigidBody[] bodies = _world.GetComponentsInChildren<IRigidBody>();
            _bodies = new Dictionary<IRigidBody, BodyTrajectoryView>();
            /*foreach (IRigidBody body in bodies)
            {
                _bodies.Add(body, new BodyTrajectoryView(body.TrajectoryEnumerable, _offsetsBuffer, bodyTrajectoryMaxBufferSize));
            }*/
            _offsetsBuffer.Refresh();
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
                Vector3 positionScaled = (Vector3) (globalPosition) * _scaleI;
                celestialKv.Value.UpdateMatrix(positionScaled, _scaleI);
            }

            Render();
        }

        private void Render()
        {
            RenderParams rp = new RenderParams(material);
            rp.worldBounds = new Bounds(Vector3.zero, 1000 * Vector3.one);
            rp.matProps = new MaterialPropertyBlock();
            rp.matProps.SetBuffer("_Positions", _circleBuffers.Positions);
            rp.matProps.SetBuffer("_Matrices", _circleBuffers.Matrices);
            Graphics.RenderPrimitivesIndexed(rp, MeshTopology.Lines, _circleBuffers.Indices,
                _circleBuffers.Indices.count, 0, _circleBuffers.Matrices.count);
        }

        private void OnDestroy()
        {
            _circleBuffers.Dispose();
        }
    }
}