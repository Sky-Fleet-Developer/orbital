using System;
using Orbital.Core;
using UnityEngine;

namespace Orbital.View
{
    public class StaticView : SystemComponent<ViewVariables, ViewSettings>
    {
        [SerializeField] private ViewSettings settings;
        public override ViewSettings Settings
        {
            get => settings;
            set => settings = value;
        }
        
        
    }

    [Serializable]
    public struct ViewSettings
    {
        public GameObject prefab;
        public Vector3 localPosition;
        public Vector3 localEulerAngles;
    }
    [Serializable]
    public struct ViewVariables
    {
        
    }
}