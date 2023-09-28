using System;
using Ara3D;
using Orbital.Model.Components;
using UnityEngine;

namespace Orbital.WorldEditor.SystemData
{
    [ExecuteInEditMode]
    public class CelestialSystemData : SystemData<CelestialVariables, CelestialSettings>
    {
        [SerializeField] private CelestialSettings settings;
        [SerializeField] private CelestialVariables variables;

        public override CelestialVariables GetVariables()
        {
            return variables;
        }

        public override CelestialSettings GetSettings()
        {
            return settings;
        }
    }
}
