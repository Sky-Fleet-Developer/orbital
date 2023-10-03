using UnityEngine;

namespace Orbital.Model.SystemComponents
{
    public abstract class SystemComponent<TRuntimeVariables, TSettings> : MonoBehaviour
    {
        public abstract TRuntimeVariables GetVariables();
        public abstract TSettings GetSettings();
        public abstract void SetSettings(TSettings value);
        public abstract void SetVariables(TRuntimeVariables value);
    }
}