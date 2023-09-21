using System;
using System.Collections.Generic;
using Orbital.Model;
using UnityEngine;
using Zenject;
using Component = Orbital.Model.Component;

namespace Orbital.Controllers.Data
{
    [Serializable]
    public class BodyData
    {
        [SerializeField] public ComponentData[] components;
    }
}