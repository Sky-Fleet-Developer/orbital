using System.Collections.Generic;
using Orbital.Core.Bodies;
using Orbital.Core.TrajectorySystem;
using Orbital.Core.Utilities;
using UnityEngine;

namespace Orbital.Core
{
    /// <summary>Scene side of the world: keeps a transform per mass under a root and hands the body components to the model.</summary>
    public class WorldSceneBinder
    {
        private const string RootMassName = "RootMass";

        /// <summary><paramref name="bodyData"/> (optional) gives each body component its record by the name of the scene object.</summary>
        public void Bind(Transform tRoot, WorldModel model, IBodyDataSource bodyData = null)
        {
            Transform root = tRoot.Find(RootMassName);
            if (!root)
            {
                root = MakeNewObject(RootMassName, tRoot);
            }
            CreateTransformsRecursively(model.Root, root);

            Dictionary<IMassSystem, Transform> transforms = new() {{model.Root, root}};
            MapRecursively(transforms, model.Root, root);
            model.BindBodies(mass =>
                transforms.TryGetValue(mass, out Transform transform) && transform.TryGetComponent(out IStaticBodyAccessor body) ? body : null);
            if (bodyData == null) return;
            foreach (Transform transform in transforms.Values)
            {
                if (transform.TryGetComponent(out IStaticBodyAccessor body) && bodyData.TryGet(transform.name, out BodyRecord record))
                {
                    body.Record = record;
                }
            }
        }

        // The slot index counts empty slots too, so a transform keeps its name when a neighbour appears or goes away.
        private static void CreateTransformsRecursively(IMassSystem mRoot, Transform tRoot)
        {
            int i = -1;
            foreach (IMassSystem mass in mRoot.GetContent())
            {
                i++;
                if (mass == null) continue;
                Transform tChild = FindSlot(tRoot, i);
                if (!tChild)
                {
                    tChild = MakeNewObject($"Child[{i}]", tRoot);
                }
                CreateTransformsRecursively(mass, tChild);
            }
        }

        private static void MapRecursively(Dictionary<IMassSystem, Transform> map, IMassSystem mRoot, Transform tRoot)
        {
            int i = -1;
            foreach (IMassSystem mass in mRoot.GetContent())
            {
                i++;
                if (mass == null) continue;
                Transform tChild = FindSlot(tRoot, i);
                if (!tChild) continue;
                map[mass] = tChild;
                MapRecursively(map, mass, tChild);
            }
        }

        private static Transform FindSlot(Transform parent, int index)
        {
            return parent.FindRegex($".*\\[{index}\\]$");
        }

        private static Transform MakeNewObject(string name, Transform parent)
        {
            Transform newObject = new GameObject(name).transform;
            newObject.SetParent(parent);
            return newObject;
        }
    }
}
