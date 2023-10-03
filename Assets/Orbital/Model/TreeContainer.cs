using System;
using Newtonsoft.Json;
using Orbital.Model.Serialization;
using Orbital.Model.TrajectorySystem;
using UnityEngine;

namespace Orbital.Model
{
    [Serializable]
    public class TreeContainer
    {
        [JsonProperty] public IMass Root;
        [SerializeField, TextArea(minLines: 6, maxLines: 10)] private string serializedValue;

        public void Load()
        {
            ISerializer serializer = new JsonPerformance();
            
            serializer.Populate(this, serializedValue);
        }
    }
}
