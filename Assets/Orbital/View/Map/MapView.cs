using System.Collections.Generic;
using Orbital.Core;
using Orbital.Core.Handles;
using Orbital.Core.Simulation;
using Orbital.Core.TrajectorySystem;
using Plugins.LocalPool;
using UnityEngine;
using Zenject;

namespace Orbital.View.Map
{
    [ExecuteAlways]
    public class MapView : MonoBehaviour, IBindMe, IViewInitializer, ILateUpdateHandler
    {
        [SerializeField] private ScriptableObjectContainer.Container viewSettingsContainer;
        [Inject] private World world;
        [Inject] private DiContainer _container;
        [SerializeField] private ScaleSettings scaleSettings;
        private Pool<ViewContainer> _viewsPool;
        private readonly List<ViewUpdater> _views = new ();
        private const string ViewName = "View_";
        private const string HierarchyItemName = "HierarchyItem";
        private readonly Dictionary<IStaticBody, Transform> _hierarchy = new ();
        // Bodies that actually move, cached once: the root has no orbit.
        private readonly List<(StaticOrbit orbit, Transform transform)> _moving = new ();
        private bool _started;
        private bool _registered;
        private double _lastRefreshTime = double.NaN;

        // Refresh runs after the simulation handlers of the same frame.
        int IOrderHolder.Order => 100;

        // The bootstrapper calls this once the world is loaded. Start covers a view that appears later.
        void IViewInitializer.InitializeView()
        {
            if (!Application.isPlaying || _started) return;
            _started = true;
            Rebuild();
            Register();
        }

        private void Start()
        {
            ((IViewInitializer) this).InitializeView();
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
            {
                // In edit mode nothing is injected and there is no event loop: the view lives on the same object as the world.
                if (world == null) world = GetComponent<World>();
                Rebuild();
            }
            else if (_started)
            {
                Register();
            }
        }

        private void OnDisable()
        {
            Unregister();
            if (world != null && world.IsLoaded) world.Model.DynamicBodyRegistered -= OnDynamicBodyRegister;
        }

        void ILateUpdateHandler.LateUpdate()
        {
            Refresh(TimeProvider.WorldTime);
        }

        /// <summary>Moves everything to the state at <paramref name="time"/>. Does nothing while the time stands still.</summary>
        public void Refresh(double time)
        {
            if (time == _lastRefreshTime) return;
            _lastRefreshTime = time;
            foreach ((StaticOrbit orbit, Transform transform) item in _moving)
            {
                item.transform.localPosition = item.orbit.GetPositionAtT(time) * scaleSettings.scale;
            }

            foreach (ViewUpdater view in _views)
            {
                view.Update(time);
            }
        }

        /// <summary>Drops all views and builds them again from the current world.</summary>
        [ContextMenu("Rebuild")]
        public void Rebuild()
        {
            if (world == null) return;
            if (!world.IsLoaded && !Application.isPlaying)
            {
                world.Load();
            }
            if (!world.IsLoaded) return;
            foreach (ViewUpdater view in _views)
            {
                view.Dispose(_viewsPool);
            }
            _views.Clear();
            _hierarchy.Clear();
            _moving.Clear();
            DestroyStaleObjects();
            CreatePool();

            world.Model.DynamicBodyRegistered -= OnDynamicBodyRegister;
            world.Model.DynamicBodyRegistered += OnDynamicBodyRegister;
            ConstructHierarchy();
            foreach (IStaticBody body in world.gameObject.GetComponentsInChildren<IStaticBody>())
            {
                _views.Add(new CelestialViewUpdater(body, _hierarchy[body], _viewsPool, viewSettingsContainer, scaleSettings));
            }
            // Bodies registered before the view existed never raised the event.
            foreach (IDynamicBody body in world.Model.DynamicBodies)
            {
                OnDynamicBodyRegister(body);
            }
            _lastRefreshTime = double.NaN;
            Refresh(TimeProvider.WorldTime);
        }

        public bool TryGetBodyTransform(IStaticBody body, out Transform bodyTransform)
        {
            return _hierarchy.TryGetValue(body, out bodyTransform);
        }

        private void Register()
        {
            if (_registered || _container == null) return;
            HandlesRegister.RegisterHandlers(this, _container);
            _registered = true;
        }

        private void Unregister()
        {
            if (!_registered) return;
            HandlesRegister.UnregisterHandlers(this, _container);
            _registered = false;
        }

        private void OnDynamicBodyRegister(IDynamicBody body)
        {
            _views.Add(new DynamicBodyViewUpdater(body, _hierarchy, _viewsPool, viewSettingsContainer, scaleSettings));
            _lastRefreshTime = double.NaN;
        }

        // Views and hierarchy items are DontSave objects that outlive the component state (domain reload in edit mode).
        // The pool is replaced afterwards, so nothing can be returned to it twice.
        private void DestroyStaleObjects()
        {
            List<GameObject> toDestroy = new List<GameObject>();
            foreach (Transform child in transform.GetComponentsInChildren<Transform>(true))
            {
                if (child == transform) continue;
                if (child.name.Contains(ViewName) || child.name.Contains(HierarchyItemName))
                {
                    toDestroy.Add(child.gameObject);
                }
            }
            foreach (GameObject item in toDestroy)
            {
                if (!item) continue;
                if (Application.isPlaying)
                {
                    Destroy(item);
                }
                else
                {
                    DestroyImmediate(item);
                }
            }
            _viewsPool = null;
            _viewsCount = 0;
        }

        private Transform CreateHierarchyItem()
        {
            return new GameObject(HierarchyItemName) {hideFlags = HideFlags.DontSave}.transform;
        }

        private void ConstructHierarchy()
        {
            IStaticBody root = world.Model.GetRootBody();
            ConstructHierarchyRecursive(CreateHierarchyItem(), root, transform);
        }

        private void ConstructHierarchyRecursive(Transform tRoot, IStaticBody bRoot, Transform parent)
        {
            tRoot.SetParent(parent);
            _hierarchy.Add(bRoot, tRoot);
            StaticOrbit orbit = bRoot.Orbit;
            if (orbit.SemiMajorAxis != 0.0 && !double.IsNaN(orbit.SemiMajorAxis))
            {
                _moving.Add((orbit, tRoot));
            }
            foreach (IStaticBody bRootChild in bRoot.Children)
            {
                ConstructHierarchyRecursive(CreateHierarchyItem(), bRootChild, tRoot);
            }
        }

        private void CreatePool()
        {
            _viewsPool = new Pool<ViewContainer>
            {
                MakeNewObject = MakeNewView,
                OnDisable = OnDisableView,
                OnEnable = OnEnableView
            };
        }

        private int _viewsCount;
        private ViewContainer MakeNewView()
        {
            GameObject view = new GameObject(ViewName + _viewsCount++, typeof(MeshRenderer), typeof(MeshFilter));
            view.hideFlags = HideFlags.DontSave;
            view.transform.SetParent(transform);
            return new ViewContainer(view.transform);
        }
        private void OnEnableView(ViewContainer v)
        {
            v.Transform.gameObject.SetActive(true);
        }

        private void OnDisableView(ViewContainer v)
        {
            v.Transform.gameObject.SetActive(false);
            v.Transform.SetParent(transform);
            v.Transform.localScale = Vector3.one;
            v.Transform.localRotation = Quaternion.identity;
            v.Transform.localPosition = Vector3.zero;
            v.MeshRenderer.enabled = true;
        }
    }
}
