using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace Orbital.Core.Bodies
{
    /// <summary>
    /// Data of the bodies except their position (size, rotation, look...), one record per body name. Scene objects are matched by name;
    /// a slot suffix such as "[3]" is ignored. The model facets (<see cref="PhysicalFacet"/>, <see cref="RotationFacet"/>) are read by Core, the server
    /// world generator and the clients; the view facets live in the Orbital assembly.
    /// </summary>
    [CreateAssetMenu(menuName = "Orbital/Body Table")]
    [MovedFrom(false, sourceNamespace: "Orbital.View.Map", sourceAssembly: "Orbital", sourceClassName: "BodyViewTable")]
    public class BodyTable : ScriptableObject, IBodyDataSource, ISerializationCallbackReceiver
    {
        public List<BodyRecord> bodies = new ();

        private readonly Dictionary<string, BodyRecord> _byName = new ();

        /// <summary>Raised when the table is edited, so open views can retune.</summary>
        public event Action Changed;

        public bool TryGet(string objectName, out BodyRecord record)
        {
            return _byName.TryGetValue(KeyOf(objectName), out record);
        }

        public static string KeyOf(string objectName)
        {
            if (string.IsNullOrEmpty(objectName)) return string.Empty;
            int slot = objectName.IndexOf('[');
            return (slot >= 0 ? objectName.Substring(0, slot) : objectName).Trim();
        }

        private void RebuildIndex()
        {
            _byName.Clear();
            foreach (BodyRecord record in bodies)
            {
                if (record == null) continue;
                record.Rebuild();
                string key = KeyOf(record.name);
                if (!_byName.TryAdd(key, record))
                {
                    Debug.LogWarning($"{name}: duplicate body '{key}', the first record is used", this);
                }
            }
        }

        private void OnValidate()
        {
            RebuildIndex();
            Changed?.Invoke();
        }

        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
        }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            RebuildIndex();
        }
    }
}
