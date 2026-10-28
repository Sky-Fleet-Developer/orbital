using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Orbital.Craft
{
    /// <summary>
    /// Greybox look of the craft made of primitives. The origin of the object is the foot point: the contact point of the legs with the ground
    /// (y = 0), which is where the flight state's position is. The long axis is +Y (nose, thrust), the center of mass is at y = cgHeight above
    /// the feet (<see cref="Orbital.Core.Flight.FlightConfig.CgHeight"/>), marked by a band on the hull. The side of +X carries a red strip, the
    /// side of +Z a blue one. The same builder serves the Editor menu (saves a prefab) and the runtime spawner (used when no prefab is given).
    /// </summary>
    public static class GreyboxVehicleFactory
    {
        /// <summary>Name of the child that shows the engine flame; the presenter scales it with the throttle.</summary>
        public const string FlameName = "Flame";
        /// <summary>Local y of the nozzle exit: the flame starts here and goes down.</summary>
        public const float NozzleExitY = 0.6f;

        public static GameObject Build(float cgHeight, Func<string, Material> materials)
        {
            GameObject root = new GameObject("GreyboxVehicle");
            Transform t = root.transform;

            // hull, nose, nozzle
            Part(t, "Hull", PrimitiveType.Cylinder, new Vector3(0, 7.3f, 0), new Vector3(3f, 5.5f, 3f), Quaternion.identity, materials("hull"));
            Part(t, "Nose", PrimitiveType.Sphere, new Vector3(0, 12.8f, 0), new Vector3(3f, 2.4f, 3f), Quaternion.identity, materials("nose"));
            Part(t, "Nozzle", PrimitiveType.Cylinder, new Vector3(0, 1.2f, 0), new Vector3(1.8f, 0.6f, 1.8f), Quaternion.identity, materials("nozzle"));

            // center of mass band, orientation strips
            Part(t, "CenterOfMass", PrimitiveType.Cylinder, new Vector3(0, cgHeight, 0), new Vector3(3.1f, 0.15f, 3.1f), Quaternion.identity, materials("cg"));
            Part(t, "MarkerX", PrimitiveType.Cube, new Vector3(1.55f, 7.3f, 0), new Vector3(0.15f, 4f, 0.4f), Quaternion.identity, materials("marker_x"));
            Part(t, "MarkerZ", PrimitiveType.Cube, new Vector3(0, 7.3f, 1.55f), new Vector3(0.4f, 4f, 0.15f), Quaternion.identity, materials("marker_z"));

            // RCS blocks on the four sides near the nose
            for (int i = 0; i < 4; i++)
            {
                float angle = i * 90f * Mathf.Deg2Rad;
                Vector3 p = new Vector3(Mathf.Cos(angle) * 1.75f, 11.8f, Mathf.Sin(angle) * 1.75f);
                Part(t, "Rcs" + i, PrimitiveType.Cube, p, new Vector3(0.5f, 0.5f, 0.5f), Quaternion.identity, materials("rcs"));
            }

            // four legs on the diagonals, feet at y = 0
            for (int i = 0; i < 4; i++)
            {
                float angle = (45f + i * 90f) * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                Vector3 top = dir * 1.5f + new Vector3(0, 4.5f, 0);
                Vector3 foot = dir * 3.6f + new Vector3(0, 0.1f, 0);
                Vector3 axis = top - foot;
                float length = axis.magnitude;
                Part(t, "Leg" + i, PrimitiveType.Cube, (top + foot) * 0.5f, new Vector3(0.18f, length, 0.18f),
                    Quaternion.FromToRotation(Vector3.up, axis / length), materials("leg"));
                Part(t, "Foot" + i, PrimitiveType.Cylinder, new Vector3(foot.x, 0.05f, foot.z), new Vector3(0.9f, 0.05f, 0.9f), Quaternion.identity, materials("foot"));
            }

            // flame: a cylinder under the nozzle, length and position are set by the presenter
            Part(t, FlameName, PrimitiveType.Cylinder, new Vector3(0, NozzleExitY, 0), new Vector3(1.3f, 0.001f, 1.3f), Quaternion.identity, materials("flame"));
            return root;
        }

        private static GameObject Part(Transform parent, string name, PrimitiveType type, Vector3 position, Vector3 scale, Quaternion rotation, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            Collider collider = go.GetComponent<Collider>();
            if (collider != null) Object.DestroyImmediate(collider); // the flight physics is our own, no PhysX
            Transform t = go.transform;
            t.SetParent(parent, false);
            t.localPosition = position;
            t.localRotation = rotation;
            t.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }

        /// <summary>Colour of a greybox material key.</summary>
        public static Color ColorOf(string key)
        {
            switch (key)
            {
                case "hull": return new Color(0.78f, 0.78f, 0.80f);
                case "nose": return new Color(0.92f, 0.92f, 0.92f);
                case "nozzle": return new Color(0.22f, 0.22f, 0.24f);
                case "cg": return new Color(0.95f, 0.80f, 0.15f);
                case "marker_x": return new Color(0.85f, 0.15f, 0.12f);
                case "marker_z": return new Color(0.15f, 0.35f, 0.90f);
                case "rcs": return new Color(0.95f, 0.50f, 0.10f);
                case "leg": return new Color(0.35f, 0.35f, 0.38f);
                case "foot": return new Color(0.15f, 0.15f, 0.15f);
                case "flame": return new Color(1.00f, 0.65f, 0.15f);
                case "ground": return new Color(0.34f, 0.40f, 0.26f);
                case "planet": return new Color(0.30f, 0.36f, 0.30f);
                case "pad": return new Color(0.70f, 0.70f, 0.70f);
                case "pole": return new Color(0.90f, 0.35f, 0.15f);
                default: return Color.magenta;
            }
        }

        /// <summary>
        /// The default material of a primitive in the current render pipeline, taken from a throw-away primitive. Copies of it are tinted, so the
        /// greybox needs no shader name and works in URP.
        /// </summary>
        public static Material DefaultPrimitiveMaterial()
        {
            GameObject probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Material material = probe.GetComponent<Renderer>().sharedMaterial;
            Object.DestroyImmediate(probe);
            return material;
        }

        /// <summary>A copy of the template tinted for the key. The caller owns it (and destroys it, or saves it as an asset).</summary>
        public static Material MakeMaterial(Material template, string key)
        {
            Material material = new Material(template);
            material.name = "Greybox_" + key;
            Color color = ColorOf(key);
            material.color = color;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            // the ground is a generated mesh: draw both sides so the winding of its triangles cannot hide it
            if ((key == "ground" || key == "planet") && material.HasProperty("_Cull")) material.SetFloat("_Cull", 0f);
            return material;
        }
    }

    /// <summary>Runtime materials of the greybox by key, made on demand and destroyed with the set.</summary>
    public sealed class GreyboxMaterials : IDisposable
    {
        private readonly Dictionary<string, Material> _materials = new Dictionary<string, Material>();
        private Material _template;

        public Material Get(string key)
        {
            if (_materials.TryGetValue(key, out Material material)) return material;
            if (_template == null) _template = GreyboxVehicleFactory.DefaultPrimitiveMaterial();
            material = GreyboxVehicleFactory.MakeMaterial(_template, key);
            _materials[key] = material;
            return material;
        }

        public void Dispose()
        {
            foreach (Material material in _materials.Values)
            {
                if (material != null) Object.Destroy(material);
            }
            _materials.Clear();
        }
    }
}
