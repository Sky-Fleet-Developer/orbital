using System;
using Ara3D;
using Orbital.Core;
using Orbital.Core.Serialization;
using Orbital.Core.TrajectorySystem;
using Orbital.Navigation;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;

namespace Orbital.Test
{
    public class PathTest : MonoBehaviour
    {
        private World _world;
        private IStaticBody _parent;
        private PathRoot _pathRoot;

        [ShowInInspector]private string EditorRefresh
        {
            get
            {
                if (_parent == null)
                {
                    Init();
                    return "refresh...";
                }
                return "ready";
            }
        }
        [ShowInInspector] public PathRoot PathRoot
        {
            get
            {
                if (_pathRoot == null)
                {
                    if (string.IsNullOrEmpty(pathJson) || pathJson == "null")
                    {
                        _pathRoot = new PathRoot();
                    }
                    else
                    {
                        _pathRoot = _serializer.Deserialize<PathRoot>(pathJson);
                    }
                    _pathRoot.Init(_world, _parent);
                    _pathRoot.Reconstruct();
                }

                return _pathRoot;
            }
            set
            {
                _pathRoot = value;
                pathJson = _serializer.Serialize(value);
            }
        }

        [ShowInInspector] private bool _showJson = false;
        [SerializeField, ShowIf("_showJson")] private string pathJson;
        private ISerializer _serializer = new JsonPerformance();

        private void Init()
        {
            _world = GetComponentInParent<World>();
            _world.Load();
            _parent = GetComponentInParent<IStaticBody>();
            _pathRoot.Init(_world, _parent);
            _pathRoot.Reconstruct();
        }

        [Button]
        private void Serialize()
        {
            pathJson = _serializer.Serialize(_pathRoot);
        }
        [Button]
        public void AddPathNode()
        {
            PathRoot.AddElement(new PathNode());
        }
        [Button]
        public void Refresh()
        {
            Init();
            PathRoot.SetDirty();
            PathRoot.RefreshDirty();    
        }

        private void OnDrawGizmosSelected()
        {
            float scale = 4.456328E-09F;
            foreach (Element element in _pathRoot.Enumerate())
            {
                if (element is SampleHolderNode node)
                {
                    //DVector3 relativePosition = 
                    //node.Trajectory.DrawGizmos(node.Celestial.Position);
                    Handles.color = Color.green * 0.7f;
                    Handles.CircleHandleCap(-1, node.Celestial.Position * scale, Quaternion.Euler(-90, 0, 0), (float) MassUtility.GetGravityRadius(node.Celestial.GravParameter) * scale, EventType.Repaint);
                    if (node.Next == null)
                    {
                        node.Trajectory.DrawGizmosByT(node.Time, node.Time + 30000, node.Celestial.Position);
                    }
                    else
                    {
                        node.Trajectory.DrawGizmosByT(node.Time, node.Next.Time, node.Celestial.Position);
                    }
                }
            }
        }
    }
}
