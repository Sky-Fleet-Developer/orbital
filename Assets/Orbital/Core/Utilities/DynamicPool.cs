using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zenject;

namespace Orbital.Core.Utilities
{
    public class DynamicPool : MonoBehaviour
    {
        public DiContainer DiContainer;
        private List<(GameObject, Component)> pool;
        private List<(GameObject, Component)> free;
        private List<(GameObject, Component)> all;

        public void Awake()
        {
            free = new List<(GameObject, Component)>();
            pool = new List<(GameObject, Component)>();
            all = new List<(GameObject, Component)>();
        }

        public (GameObject, Component) Find(Component prefab, bool remuve = false)
        {
            for (int i = 0; i < pool.Count; i++)
            {
                if (pool[i].Item1 == prefab.gameObject)
                {
                    if (remuve)
                    {
                        (GameObject, Component) value = pool[i];
                        pool.RemoveAt(i);
                        return value;
                    }
                    return pool[i];
                }
            }
            return (null, null);
        }
        
        public T Get<T>(T prefab, Transform parent = null) where T : Component
        {
            (GameObject, Component) target = Find(prefab, true);
            if (target.Item1 == null)
            {
                T instance = Instantiate(prefab, parent);
                HandleDiForChildren.Inject(DiContainer, instance.gameObject);
                instance.name = prefab.name + $"({all.Count(x => x.Item1 == prefab)})";
                target = (prefab.gameObject, instance);
                all.Add(target);
            }
            else
            {
                target.Item2.transform.SetParent(parent);
            }
            free.Add(target);
            target.Item2.gameObject.SetActive(true);
            if (!parent)
            {
                target.Item2.transform.SetParent(null);
            }
            else
            {
                target.Item2.transform.localPosition = Vector3.zero;
                target.Item2.transform.localRotation =Quaternion.identity;
                target.Item2.transform.localScale = Vector3.one;
                if (target.Item2.transform is RectTransform rtr)
                {
                    rtr.anchoredPosition = Vector2.zero;
                }
            }

            return target.Item2 as T;
        }

        public void Return(Component component)
        {
            if (!component) return;

            for (int i = 0; i < free.Count; i++)
            {
                if (free[i].Item2 == component)
                {
                    component.transform.SetParent(transform);
                    component.gameObject.SetActive(false);
                    pool.Add(free[i]);
                    free.RemoveAt(i);
                    return;
                }
            }
            pool.Add((component.gameObject, component));
            component.gameObject.SetActive(false);
        }
    }
}