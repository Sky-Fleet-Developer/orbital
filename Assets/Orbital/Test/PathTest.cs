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
            _pathRoot.BuildTransitions();
            /*PathRoot.SetDirty();
            PathRoot.RefreshDirty();    */
        }

        private void OnDrawGizmosSelected()
        {
            float scale = 4.456328E-09F;
            foreach (Element element in _pathRoot.Enumerate())
            {
                if (element is SampleHolderNode node)
                {
                    //DVector3 relativePosition = 
                    //node.Orbit.DrawGizmos(node.Celestial.Position);
                    Handles.color = Color.green * 0.7f;
                    Handles.CircleHandleCap(-1, node.Celestial.Position * scale, Quaternion.Euler(-90, 0, 0), (float) MassUtility.GetGravityRadius(node.Celestial.GravParameter) * scale, EventType.Repaint);
                    if (node.Next == null || node.Ending.Type == OrbitEndingType.Cycle)
                    {
                        node.Orbit.DrawGizmos(node.Celestial.Position);
                    }
                    else
                    {
                        if(Math.Abs(node.Time - node.Next.Time) < 1e-8) continue;
                        node.Orbit.DrawGizmosByT(node.Time, node.Next.Time, node.Celestial.Position);
                    }
                    Debug.DrawLine(node.Celestial.Position * scale, (node.Celestial.Position + node.Orbit.GetPositionAtT(node.Time)) * scale, Color.yellow, 2);
                    Debug.DrawLine(node.Celestial.Position * scale, (node.Celestial.Position + node.Orbit.GetPositionAtT(node.Ending.Time)) * scale, Color.green, 2);
                }
            }
        }
    }
}
