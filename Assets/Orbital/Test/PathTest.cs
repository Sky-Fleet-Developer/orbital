using System;
using Ara3D;
using Orbital.Core;
using Orbital.Core.Serialization;
using Orbital.Navigation;
using Sirenix.OdinInspector;
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
            PathRoot.RefreshDirty();    
        }

        private void OnDrawGizmosSelected()
        {
            foreach (Element element in _pathRoot.Enumerate())
            {
                if (element is PathNode node)
                {
                    //DVector3 relativePosition = 
                    //node.Trajectory.DrawGizmosByT(node.Previous.Time, node.Time, );
                }
            }
        }
    }
}
