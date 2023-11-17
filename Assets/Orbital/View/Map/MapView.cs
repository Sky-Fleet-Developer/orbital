using System.Collections.Generic;
using System.Linq;
using Orbital.Core;
using Orbital.Core.Simulation;
using Orbital.Core.TrajectorySystem;
using Plugins.LocalPool;
using UnityEngine;

namespace Orbital.View.Map
{
    [ExecuteAlways]
    public class MapView : MonoBehaviour
    {
        [SerializeField] private ScriptableObjectContainer.Container viewSettingsContainer;
        [SerializeField] private World world;
        [SerializeField] private ScaleSettings scaleSettings;
        private Pool<ViewContainer> _viewsPool;
        private readonly List<ViewUpdater> _views = new ();
        private const string ViewName = "View_";
        private const string HierarchyItemName = "HierarchyItem";
        private Dictionary<IStaticBody, Transform> _hierarchy = new ();
        
        private void Start()
        {
            Init();
        }

#if UNITY_EDITOR
        private void OnEnable()
        {
            if (world == null)
            {
                Init();
            }
        }
#endif
        
        private void LateUpdate()
        {
            foreach (KeyValuePair<IStaticBody, Transform> pair in _hierarchy)
            {
                Vector3 pos = pair.Key.Orbit.GetPositionAtT(TimeService.WorldTime) * scaleSettings.scale;
                pair.Value.localPosition = pos;
            }

            foreach (ViewUpdater view in _views)
            {
                view.Update(TimeService.WorldTime);
            }
        }
        
        private void Init()
        {
            //_world = GetComponentInChildren<PlayerCharacter>();
            #if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                world.Load();
            }
            #endif
            world.DynamicBodyRegisterHandler -= OnDynamicBodyRegister;
            world.DynamicBodyRegisterHandler += OnDynamicBodyRegister;
            CreatePool();
            ClearViews();
            ConstructHierarchy();
            foreach (IStaticBody body in world.gameObject.GetComponentsInChildren<IStaticBody>())
            {
                _views.Add(new CelestialViewUpdater(body, _hierarchy[body], _viewsPool, viewSettingsContainer, scaleSettings));
            }
        }

        private void OnDisable()
        {
            world.DynamicBodyRegisterHandler -= OnDynamicBodyRegister;
        }

        private void OnDynamicBodyRegister(IDynamicBody body)
        {
            _views.Add(new DynamicBodyViewUpdater(body, _hierarchy, _viewsPool, viewSettingsContainer, scaleSettings));
        }

        private void ClearViews()
        {
            List<Transform> toDestroy = new List<Transform>();
            foreach (Transform child in transform.GetComponentsInChildren<Transform>(true))
            {
                if (child.name.Contains(ViewName))
                {
                    _viewsPool.Put(new ViewContainer(child));
                }else if (child.name.Contains(HierarchyItemName))
                {
                    toDestroy.Add(transform);
                }
            }
            foreach (Transform item in toDestroy)
            {
                if (Application.isPlaying)
                {
                    Destroy(item.gameObject);
                }
                else
                {
                    DestroyImmediate(item.gameObject);
                }
            }
        }

        private Transform CreateHierarchyItem()
        {
            return new GameObject("HierarchyItem") {hideFlags = HideFlags.DontSave}.transform;
        }
        
        private void ConstructHierarchy()
        {
            IStaticBody root = world.GetRootBody();
            ConstructHierarchyRecursive(CreateHierarchyItem(), root, transform);
        }

        private void ConstructHierarchyRecursive(Transform tRoot, IStaticBody bRoot, Transform parent)
        {
            tRoot.SetParent(parent);
            _hierarchy.Add(bRoot, tRoot);
            foreach (IStaticBody bRootChild in bRoot.Children)
            {
                ConstructHierarchyRecursive(CreateHierarchyItem(), bRootChild, tRoot);
            }
        }

        private void CreatePool()
        {
            _viewsPool ??= new Pool<ViewContainer>();
            _viewsPool.MakeNewObject = MakeNewView;
            _viewsPool.OnDisable = OnDisableView;
            _viewsPool.OnEnable = OnEnableView;
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
