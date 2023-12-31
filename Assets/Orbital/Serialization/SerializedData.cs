﻿using UnityEngine;
using Zenject;

namespace Orbital.Core.Serialization
{
    public class SerializedData<T> : ScriptableObject
    {
        [SerializeField] private T value;
        public T GetValue() => value;

        [Inject]
        private void Inject(DiContainer container)
        {
            container.Inject(value);
        }

        public static implicit operator T(SerializedData<T> data) => data.GetValue();
    }
}