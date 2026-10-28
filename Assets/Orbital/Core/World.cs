using Orbital.Core.Bodies;
using UnityEngine;
using Zenject;

namespace Orbital.Core
{
    /// <summary>
    /// Scene entry point of the world. Owns the serialized tree and, on load, deserializes it, builds the <see cref="WorldModel"/>,
    /// binds it to the body components and injects the hierarchy. Views are initialized afterwards (<see cref="IViewInitializer"/>).
    /// </summary>
    public class World : MonoBehaviour, ISceneReady, IBindMe
    {
        [SerializeField] private TreeContainer tree;
        [Inject] private DiContainer _container;
        // Radius, rotation and the other per-body data; optional: without it bodies have no data (radius 0).
        [Inject(Optional = true)] private IBodyDataSource _bodyData;

        public WorldModel Model { get; private set; }
        public bool IsLoaded => Model != null;

        /// <summary>Sets the source of the body data. Call before <see cref="Load"/> where there is no DI container (edit mode).</summary>
        public void SetBodyData(IBodyDataSource data)
        {
            _bodyData = data;
        }

        void ISceneReady.OnSceneReady()
        {
            Load();
        }

        private void OnDestroy()
        {
            Debug.Log("World destroyed");
        }

        /// <summary>Does nothing if the world is loaded already or has no tree.</summary>
        public void Load()
        {
            if (IsLoaded)
            {
                if (Application.isPlaying)
                {
                    Debug.LogError("World has been already loaded!");
                }
                return;
            }
            if (tree == null)
            {
                tree = new TreeContainer();
            }
            if (tree.Root == null)
            {
                tree.Load();
            }
            if (tree.Root == null) return;

            WorldModel model = new WorldModel(tree.Root, TimeProvider.WorldTime);
            new WorldSceneBinder().Bind(transform, model, _bodyData);
            Model = model;
            InjectHierarchy();
        }

        private void InjectHierarchy()
        {
            if (_container == null)
            {
                // Edit mode has no container.
                if (Application.isPlaying) Debug.LogError("Has no DI container in World component");
                return;
            }
            _container.InjectGameObject(gameObject);
        }
    }
}
