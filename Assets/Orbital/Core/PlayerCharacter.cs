﻿using Orbital.Core.TrajectorySystem;
using UnityEngine;

namespace Orbital.Core
{
    public class PlayerCharacter : MonoBehaviour
    {
        [SerializeField] private string playerName;
        [SerializeField] private int id;
        private IHierarchyElement _parent;

        public string PlayerName
        {
            get => playerName;
            set
            {
                playerName = value;
                name = value + "_Player";
            }
        }

        public int Id
        {
            get => id;
            set => id = value;
        }

        public IHierarchyElement Parent => _parent;
    }
}