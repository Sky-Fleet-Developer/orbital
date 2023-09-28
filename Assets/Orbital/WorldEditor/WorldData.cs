using System;
using System.Collections.Generic;
using Orbital.Controllers.Data;
using Orbital.Model;
using Orbital.Model.Components;
using Orbital.Model.TrajectorySystem;
using Sirenix.OdinInspector;
using UnityEngine;
using Zenject;

namespace Orbital.WorldEditor
{
    [ExecuteInEditMode]
    public class WorldData : MonoBehaviour, IFactory<World>
    {
        private const int FinalSetupIteration = 2;

        private ISerializer _serializer = new JsonPerformance();
        [Inject] private DiContainer _container;
        
        [SerializeField] public BodyData[] bodies;
        [SerializeField, TextArea] private string serializedTree;
        
        //[ShowInInspector] private IMass result;
        
        private void OnEnable()
        {
            if (gameObject.activeInHierarchy && Application.isPlaying)
            {
                gameObject.SetActive(false);
                throw new Exception($"This object {gameObject.name} can be exist as prefab only");
            }

            DiContainer editorModeContainer = new DiContainer();
            editorModeContainer.Inject(this);
            Init();
        }

        /*[Button]
        public void Check()
        {
            IMass root = new SingleCenterBranch(
                new CelestialBody(settings[0])
                , new List<IMass>
                {
                    new CelestialBody(settings[1]),
                    new DoubleSystemBranch(
                        new CelestialBody(settings[2]), 
                        new SingleCenterBranch(
                            new CelestialBody(settings[3]),
                            new List<IMass>
                            {
                                new CelestialBody(settings[4])
                            }))
                });
            string json = _serializer.Serialize(root);
            Debug.Log(json);
            result = _serializer.Deserialize<SingleCenterBranch>(json);
        }*/

        private void Init()
        {
            bodies = GetComponentsInChildren<BodyData>();
            foreach (BodyData bodyData in bodies)
            {
                bodyData.Init();
            }
        }
        
        public World Create()
        {
            Init();
            World result = new World();
            ApplyDataToWorld(result);
            return result;
        }


        private void ApplyDataToWorld(World world)
        {
            List<IEnumerator<int>> iterators = new();

            for (var i = 0; i < bodies.Length; i++)
            {
                Body newBody = new Body();
                iterators.Add(SetupComponents(newBody, bodies[i]).GetEnumerator());
                _container.Inject(newBody);
                world.AddBody(newBody);
            }

            for (int i = 0; i <= FinalSetupIteration; i++)
            {
                foreach (IEnumerator<int> element in iterators)
                {
                    element.MoveNext();
                }
            }
        }

        private IEnumerable<int> SetupComponents(Body body, BodyData bodyData)
        {
            /*Dictionary<ComponentData, Model.Component> createdComponents = new();
            for (int i = 0; i < bodyData.components.Length; i++)
            {
                Type type = Type.GetType(bodyData.components[i].Type);
                if (type == null) continue;
                Model.Component component = (Model.Component) Activator.CreateInstance(type, new object[] {body});
                createdComponents.Add(bodyData.components[i], component);
            }

            yield return 0;

            for (int i = 0; i < bodyData.components.Length; i++)
            {
                body.AddComponent(createdComponents[bodyData.components[i]]);
            }

            yield return 1;

            for (int i = 0; i < bodyData.components.Length; i++)
            {
                ComponentData systemData = bodyData.components[i];
                _serializer.Populate(createdComponents[systemData], systemData.Data);
            }*/

            yield return FinalSetupIteration;
        }
    }
}