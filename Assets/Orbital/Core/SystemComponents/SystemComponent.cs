using UnityEngine;
using Zenject;

namespace Orbital.Core.SystemComponents
{
    public abstract class SystemComponent<TRuntimeVariables, TSettings> : MonoBehaviour
    {
        public virtual TRuntimeVariables Variables { get; set; } 
        public virtual TSettings Settings { get; set; } 
        [Inject] private ComponentsRegistrationService _componentsRegistrationService;
        
        protected virtual void Start()
        {
            if (Application.isPlaying)
            {
                _componentsRegistrationService.RegisterComponent(this);
            }
        }


        protected virtual void OnDestroy()
        {
            if (Application.isPlaying)
            {
                _componentsRegistrationService.UnregisterComponent(this);
            }
        }
    }
}