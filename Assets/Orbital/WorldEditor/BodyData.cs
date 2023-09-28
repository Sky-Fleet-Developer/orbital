using System;
using UnityEngine;

namespace Orbital.WorldEditor
{
    public class BodyData : MonoBehaviour
    {
        [SerializeField] public ComponentData[] components;

        public void Init()
        {
            components = GetComponents<ComponentData>();
        }
    }
}