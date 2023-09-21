using System;
using UnityEngine;

namespace Orbital.Controllers.Data
{
    [Serializable]
    public class ComponentData
    {
        [SerializeField] private string serializedType;
        [SerializeField] private string serializedData;
        public string Type => serializedType;
        public string Data => serializedData;
    }
}