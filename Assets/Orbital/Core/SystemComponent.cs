using System;
using Sirenix.OdinInspector;
using UnityEngine;
using Zenject;

namespace Orbital.Core
{
    public interface ISystemComponentAccessor
    {
        int Id { get; set; }
        object Settings { get; set; }
        object Variables { get; set; }
    }
    public abstract class SystemComponent<TRuntimeVariables, TSettings> : MonoBehaviour, ISystemComponentAccessor where TRuntimeVariables : new() where TSettings : new()
    {
        [ShowInInspector, ReadOnly] public int Id
        {
            get
            {
                if (id == -1)
                {
                    id = GetInstanceID();
                }
                return id;
            }
            set
            {
                if (id != -1)
                {
                    throw new Exception("Can't write Id twice");
                }
                id = value;
            }
        }

        object ISystemComponentAccessor.Settings
        {
            get => Settings;
            set => Settings = (TSettings)value;
        }
        
        object ISystemComponentAccessor.Variables
        {
            get => Variables;
            set => Variables = (TRuntimeVariables)value;
        }

        [SerializeField, HideInInspector] private int id = -1;
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