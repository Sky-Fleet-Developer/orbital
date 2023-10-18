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
    [Serializable]
    public class WorldViewSettings
    {
        public int celestialTrajectoryAccuracy = 200;
        public float scale = 224400000;
        public Material material;
        public int bodyTrajectoryMaxBufferSize = 400;
    }
    [RequireComponent(typeof(World))]
    public class WorldMapView : MonoBehaviour//, IUpdateHandler
    {
        [SerializeField] private WorldViewSettings settings;
        [Inject] private DiContainer _diContainer;
        private float _scaleI;
        private World _world;
        private Dictionary<StaticBody, StaticTrajectoryView> _celestials;
        private Dictionary<IDynamicBody, BodyTrajectoryView> _bodies;
       // private StatictrajectoryViewContainer _statictrajectoryContainer;
        private OffsetsBuffer _offsetsBuffer;

       /* private void Awake()
        {
            _scaleI = 1 / settings.scale;
            _statictrajectoryContainer = new StatictrajectoryViewContainer();
            _statictrajectoryContainer.PrepareBuffers(settings.celestialTrajectoryAccuracy);
            _offsetsBuffer = new OffsetsBuffer();
            _world = GetComponent<World>();
        }

        private void Start()
        {
            StaticBody[] masses = _world.GetComponentsInChildren<StaticBody>();
            _celestials = new Dictionary<StaticBody, StaticTrajectoryView>();
            foreach (StaticBody mass in masses)
            {
                if (mass.Trajectory != null)
                {
                    _celestials.Add(mass, new StaticTrajectoryView(mass.Trajectory, _statictrajectoryContainer));
                }
            }
            _statictrajectoryContainer.Refresh();

            IDynamicBody[] bodies = _world.GetComponentsInChildren<IDynamicBody>();
            _bodies = new Dictionary<IDynamicBody, BodyTrajectoryView>();
            foreach (IDynamicBody body in bodies)
            {
                _bodies.Add(body, new BodyTrajectoryView(body.TrajectoryEnumerable, _offsetsBuffer, bodyTrajectoryMaxBufferSize));
            }
            _offsetsBuffer.Refresh();
            HandlesRegister.RegisterHandlers(this, _diContainer);
        }

        private void OnValidate()
        {
            _scaleI = 1 / settings.scale;
        }

        void IUpdateHandler.Update()
        {
            foreach (KeyValuePair<StaticBody, StaticTrajectoryView> celestialKv in _celestials)
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
           
        }

        private void OnDestroy()
        {
            _statictrajectoryContainer.Dispose();
        }*/
    }
}