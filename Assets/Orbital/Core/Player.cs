﻿using Orbital.Core.TrajectorySystem;
using UnityEngine;

namespace Orbital.Core
{
    public class Player : MonoBehaviour
    {
        [SerializeField] private string playerName;
        [SerializeField] private int id;
        private IHierarchyElement _parent;

        public string PlayerName
        {
            get => playerName;
            set => playerName = value;
        }

        public int Id
        {
            get => id;
            set => id = value;
        }

        public IHierarchyElement Parent => _parent;
    }
}