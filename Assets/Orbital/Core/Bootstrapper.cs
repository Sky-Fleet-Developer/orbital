using System.Collections.Generic;
using System.Globalization;
using Orbital.Core.Utilities;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.SceneManagement;
using Zenject;

namespace Orbital.Core
{
    public class Bootstrapper
    {
        private static Dictionary<Scene, DiContainer> _diContainers = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Main()
        {
            Application.runInBackground = true;
            Application.backgroundLoadingPriority = ThreadPriority.High;
            Application.targetFrameRate = 60;
            var bootstrapper = new Bootstrapper();
            bootstrapper.Run();
#if UNITY_EDITOR
            EditorApplication.playModeStateChanged += OnStateChanged;
            void OnStateChanged(PlayModeStateChange state)
            {
                if (state == PlayModeStateChange.ExitingPlayMode)
                {
                    bootstrapper.Stop();
                }
            }
#endif
        }

        private DiContainer _projectContainer;

        private void Run()
        {
            Debug.Log("Bootstrapper running");
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            _projectContainer = ProjectContext.Instance.Container;

            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += SceneUnloaded;
        }


        private void Stop()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= SceneUnloaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            var sceneContextContainer = _projectContainer.CreateSubContainer();
            var dynamicPool = new GameObject("[DynamicPool]").AddComponent<DynamicPool>();
            dynamicPool.DiContainer = sceneContextContainer;
            _diContainers[scene] = sceneContextContainer;
            SceneManager.MoveGameObjectToScene(dynamicPool.gameObject, scene);
            sceneContextContainer.Bind<DynamicPool>().FromInstance(dynamicPool);
        
            InstallScene(scene, sceneContextContainer);
        }
    
        private void SceneUnloaded(Scene scene)
        {
            _diContainers.Remove(scene);
        }

        private void InstallScene(Scene scene, DiContainer container)
        {
            foreach (var entry in scene.GetRootGameObjects())
            {
                if (entry.TryGetComponent<HandleDiForChildren>(out var handler))
                {
                    handler.InstallBindings(container);
                    continue;
                }
            
                foreach (var bindMe in entry.GetComponents<IBindMe>())
                {
                    bindMe.BindTo(container);
                }
                foreach (var installer in entry.GetComponents<IInstaller>())
                {
                    installer.InstallBindings(container);
                }
            }
        
            foreach (var entry in scene.GetRootGameObjects())
            {
                if (entry.TryGetComponent<HandleDiForChildren>(out var handler) && handler.createSubContainer)
                {
                    container.Inject(handler);
                    continue;
                }
                foreach (var monoBehaviour in entry.GetComponents<MonoBehaviour>())
                {
                    container.Inject(monoBehaviour);
                }
            }

            foreach (var entry in scene.GetRootGameObjects())
            {
                foreach (var ready in entry.GetComponentsInChildren<ISceneReady>(true))
                {
                    ready.OnSceneReady();
                }
            }
        }

        public static void InstallObject(GameObject obj)
        {
            var container = _diContainers[obj.scene];
            foreach (var installer in obj.GetComponents<IInstaller>())
            {
                installer.InstallBindings(container);
            }
            foreach (var monoBehaviour in obj.GetComponents<MonoBehaviour>())
            {
                container.Inject(monoBehaviour);
            }
        }
    }
}