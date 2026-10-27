using System;
using Newtonsoft.Json;
using Orbital.Core.Serialization;
using Orbital.Core.TrajectorySystem;
using UnityEngine;

namespace Orbital.Core
{
    /// <summary>Serialized description of the mass tree. Runtime state lives in <see cref="WorldModel"/>.</summary>
    [Serializable]
    public class TreeContainer
    {
        [JsonProperty] public IMassSystem Root;
        [SerializeField, TextArea(minLines: 6, maxLines: 10)]
        private string serializedValue;

        public void Load()
        {
            Root = null;
            if (string.IsNullOrEmpty(serializedValue)) return;
            ISerializer serializer = new JsonPerformance();
            serializer.Populate(this, serializedValue);
        }
    }
}
