using System;
using ModestTree;
using UnityEngine;
using Zenject;

namespace Orbital.Core
{
    public class HandleDiForChildren : MonoBehaviour, IInstaller
    {
        public bool createSubContainer;
        
        private DiContainer _localContainer;
        public void InstallBindings(DiContainer container)
        {
            if (createSubContainer)
            {
                _localContainer = container.CreateSubContainer();
                container = _localContainer;
            }
            foreach (var inst in GetComponentsInChildren<IInstaller>(true))
            {
                if(inst == this) continue;
                inst.InstallBindings(container);
            }
            foreach (var bindMe in GetComponentsInChildren<IBindMe>(true))
            {
                bindMe.BindTo(container);
            }
        }
        
        [Inject]
        private void Inject(DiContainer container)
        {
            if (_localContainer != null)
            {
                container = _localContainer;
            }
            Inject(container, gameObject, this);
        }

        public static void Inject(DiContainer container, GameObject target, Component exclude = null)
        {
            foreach (var mb in target.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if(mb == exclude) continue;
                try
                {
                    container.Inject(mb);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }
    }
}