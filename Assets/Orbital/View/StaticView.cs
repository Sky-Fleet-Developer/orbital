using System;
using Orbital.Core;
using UnityEngine;

namespace Orbital.View
{
    public class StaticView : MonoBehaviour
    {
        [SerializeField] private ViewSettings settings;

        
    }

    [Serializable]
    public struct ViewSettings
    {
        public GameObject prefab;
        public Vector3 localPosition;
        public Vector3 localEulerAngles;
    }
}