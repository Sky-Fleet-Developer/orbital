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
        public float rotation;
        public Vector3 position;
        public Vector3 velocity;
        
        private World _world;
        private IStaticBody _parent;
        private NavigationPath _navigationPath;

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
        [ShowInInspector] public NavigationPath NavigationPath
        {
            get
            {
                if (_navigationPath == null)
                {
                    if (string.IsNullOrEmpty(pathJson) || pathJson == "null")
                    {
                        _navigationPath = new NavigationPath();
                    }
                    else
                    {
                        _navigationPath = _serializer.Deserialize<NavigationPath>(pathJson);
                    }
                    CalculatePath();
                }

                return _navigationPath;
            }
            set
            {
                _navigationPath = value;
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
            CalculatePath();
        }

        private void CalculatePath()
        {
            Vector3 h = Vector3.Cross(position, velocity).normalized;
            Quaternion quaternion = Quaternion.AngleAxis(rotation, h);
            _navigationPath.Calculate(GetComponentInParent<IStaticBody>(), quaternion * position, quaternion * velocity, TimeService.WorldTime);
            //_navigationPath.Reconstruct();
        }

        [Button]
        private void Serialize()
        {
            pathJson = _serializer.Serialize(_navigationPath);
        }
        [Button]
        public void AddPathNode()
        {
            NavigationPath.AddElement(new PathNode());
        }
        [Button]
        public void Refresh()
        {
            Init();
            _navigationPath.BuildTransitions(0);
            /*NavigationPath.SetDirty();
            NavigationPath.RefreshDirty();    */
        }

        #if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            float scale = 4.456328E-09F;
            foreach (PathElement node in _navigationPath.Enumerate())
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
        #endif
    }
}
