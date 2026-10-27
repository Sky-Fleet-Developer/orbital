using System;
using UnityEngine;
using Zenject;

namespace Orbital.Core
{
    public interface IInstaller
    {
        public void InstallBindings(DiContainer container);
    }

    /// <summary>Component bound to the scene container by its own type and base types.</summary>
    public interface IBindMe
    {
    }

    /// <summary>
    /// Data phase. Called once per scene after all bindings are installed and all root objects are injected, before any Start.
    /// The model is deserialized and built here.
    /// </summary>
    public interface ISceneReady
    {
        void OnSceneReady();
    }

    /// <summary>View phase. Called once per scene after every <see cref="ISceneReady"/> has run, so the model is ready to be shown.</summary>
    public interface IViewInitializer
    {
        void InitializeView();
    }

    public static class BindMeExtensions
    {
        public static void BindTo(this IBindMe bindMe, DiContainer container)
        {
            Type type = bindMe.GetType();
            while (type != null && type != typeof(MonoBehaviour) && type != typeof(Component))
            {
                container.Bind(type).FromInstance(bindMe);
                type = type.BaseType;
            }
        }
    }
}