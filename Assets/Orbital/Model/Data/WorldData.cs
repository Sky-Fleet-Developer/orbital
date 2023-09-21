using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Orbital.Model;
using UnityEngine;
using Zenject;

namespace Orbital.Controllers.Data
{
    [Serializable]
    public class WorldData
    {
        [SerializeField] public BodyData[] bodies;
    }
}
