using System;
using UnityEngine;

namespace Orbital.WorldEditor
{
    public abstract class SystemData<TRuntimeVariables, TSettings> : ComponentData
    {
        public abstract TRuntimeVariables GetVariables();
        public abstract TSettings GetSettings();
        public abstract void SetSettings(TSettings value);
        public abstract void SetVariables(TRuntimeVariables value);
    }
}