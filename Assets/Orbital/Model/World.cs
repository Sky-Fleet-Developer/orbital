using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using Unity.Collections;
using UnityEngine;

namespace Orbital.Model
{
    public class World
    {
        [ShowInInspector] private List<Body> _bodies = new();
        
        public void Init()
        {
            Debug.Log("World created");
        }

        public void AddBody(Body body)
        {
            _bodies.Add(body);
        }

        public void RemoveBody(Body body)
        {
            _bodies.Remove(body);
        }
    }
}
