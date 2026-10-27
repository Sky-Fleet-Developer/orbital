using System;
using System.Collections.Generic;
using UnityEngine;

namespace Orbital.View.Map
{
    /// <summary>View data of the bodies, one record per body name. Scene objects are matched by name; a slot suffix such as "[3]" is ignored.</summary>
    [CreateAssetMenu(menuName = "Orbital/Body View Table")]
    public class BodyViewTable : ScriptableObject, ISerializationCallbackReceiver
    {
        public List<BodyViewRecord> bodies = new ();

        private readonly Dictionary<string, BodyViewRecord> _byName = new ();

        /// <summary>Raised when the table is edited, so open views can retune.</summary>
        public event Action Changed;

        public bool TryGet(string objectName, out BodyViewRecord record)
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
            foreach (BodyViewRecord record in bodies)
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
