using System;
using System.Collections.Generic;
using Orbital.Core.Bodies;
using Orbital.Core.TrajectorySystem;
using Orbital.Core.Utilities;
using Orbital.Core.Worlds;
using UnityEngine;
using Object = UnityEngine.Object;

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

        /// <summary>
        /// Binding of a world built from the shared world file (stage 2c.W): every mass of the file tree finds its scene object under the object of
        /// its parent by the body id (<see cref="WorldFileLoader.IdOf"/> of the object name without the "[slot]" suffix), not by the slot.
        /// Differences are reported, not fatal (the scene is only the view):
        /// - a body of the file without a scene object: error; an empty object "&lt;Name&gt;[slot]" stands in (no <see cref="IStaticBodyAccessor"/>, so
        ///   the body has no view and the model does not see it as a body: no sphere of influence for crafts near it);
        /// - a body object of the scene the file does not have: warning; in play mode the object is switched off, so no view draws a body
        ///   that has no orbit (in edit mode the scene is left as it is);
        /// - every body gets the record of <see cref="WorldBodyRecords.Make"/>: radius and rotation from the file (stage 2c.W2, the numbers of the
        ///   server), view facets from the body table; when the table record has other model facets they are ignored: warning.
        /// Returns the number of errors and warnings.
        /// </summary>
        public (int errors, int warnings) BindByName(Transform tRoot, WorldModel model, IReadOnlyDictionary<IMassSystem, WorldBodyDef> bodyOf,
            IBodyDataSource bodyData = null, Object context = null)
        {
            int errors = 0, warnings = 0;
            Transform root = tRoot.Find(RootMassName);
            if (!root)
            {
                root = MakeNewObject(RootMassName, tRoot);
            }

            Dictionary<IMassSystem, Transform> transforms = new() {{model.Root, root}};

            void MapByName(IMassSystem mParent, Transform tParent)
            {
                int i = -1;
                foreach (IMassSystem mass in mParent.GetContent())
                {
                    i++;
                    if (mass == null || !bodyOf.TryGetValue(mass, out WorldBodyDef def)) continue;
                    Transform tChild = null;
                    foreach (Transform candidate in tParent)
                    {
                        if (WorldFileLoader.IdOf(BodyTable.KeyOf(candidate.name)) != def.Id) continue;
                        tChild = candidate;
                        break;
                    }

                    if (!tChild)
                    {
                        tChild = MakeNewObject($"{def.Name ?? def.Id}[{i}]", tParent);
                        Debug.LogError($"World: body '{def.Id}' of the world file has no object under '{tParent.name}' in the scene; it has no view " +
                                       "and no sphere of influence on the client (add a StaticBody object named after it)", context);
                        errors++;
                    }

                    transforms[mass] = tChild;
                    MapByName(mass, tChild);
                }
            }

            MapByName(model.Root, root);
            HashSet<Transform> used = new HashSet<Transform>(transforms.Values);
            foreach (IStaticBodyAccessor body in root.GetComponentsInChildren<IStaticBodyAccessor>(true))
            {
                Transform t = ((Component) body).transform;
                if (used.Contains(t)) continue;
                Debug.LogWarning($"World: scene body '{t.name}' is not in the world file; it is not part of the world" +
                                 (Application.isPlaying ? " and is switched off" : string.Empty), context);
                warnings++;
                if (Application.isPlaying) t.gameObject.SetActive(false);
            }

            model.BindBodies(mass =>
                transforms.TryGetValue(mass, out Transform transform) && transform.TryGetComponent(out IStaticBodyAccessor body) ? body : null);
            HashSet<string> compared = new HashSet<string>();
            foreach (KeyValuePair<IMassSystem, Transform> pair in transforms)
            {
                if (!pair.Value.TryGetComponent(out IStaticBodyAccessor body) || !bodyOf.TryGetValue(pair.Key, out WorldBodyDef def)) continue;
                BodyRecord view = null;
                bodyData?.TryGet(pair.Value.name, out view);
                body.Record = WorldBodyRecords.Make(def, view);
                if (view == null || !compared.Add(def.Id)) continue;
                string difference = CompareWithRecord(def, view);
                if (difference == null) continue;
                Debug.LogWarning($"World: body '{def.Id}': {difference} (the client uses the world file; the model facets of the table count only for the source Scene)", context);
                warnings++;
            }

            return (errors, warnings);
        }

        /// <summary>Null when the body table record has no model facets or the ones of the file.</summary>
        private static string CompareWithRecord(WorldBodyDef def, BodyRecord record)
        {
            if (def.Virtual) return null;
            double radius = record.GetFacet<PhysicalFacet>()?.radiusMeters ?? 0.0;
            if (record.HasFacet<PhysicalFacet>() && !Same(radius, def.Radius)) return $"radius {def.Radius} m in the file, {radius} m in the body table";
            RotationFacet rotation = record.GetFacet<RotationFacet>();
            WorldRotationDef file = def.Rotation;
            bool fileSpins = file != null && (file.PeriodSeconds > 0 || file.TidallyLocked);
            bool tableSpins = rotation != null && (rotation.rotationPeriodSeconds > 0 || rotation.tidallyLocked);
            if (rotation == null || (!fileSpins && !tableSpins)) return null;
            if (fileSpins != tableSpins) return "rotation " + (fileSpins ? "only in the file" : "only in the body table");
            if (!Same(file.PeriodSeconds, rotation.rotationPeriodSeconds) || !Same(file.ObliquityDeg, rotation.axialTiltDegrees) ||
                !Same(file.AxisAzimuthDeg, rotation.axisAzimuthDegrees) || !Same(file.InitialAngleDeg, rotation.initialAngleDegrees) ||
                file.TidallyLocked != rotation.tidallyLocked)
            {
                return "rotation differs between the file and the body table";
            }

            return null;
        }

        private static bool Same(double a, double b)
        {
            return Math.Abs(a - b) <= 1e-9 * Math.Max(1.0, Math.Max(Math.Abs(a), Math.Abs(b)));
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
