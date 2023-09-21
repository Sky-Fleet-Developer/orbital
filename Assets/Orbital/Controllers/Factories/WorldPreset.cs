using System;
using System.Collections.Generic;
using Orbital.Controllers.Data;
using Orbital.Model;
using UnityEngine;
using Zenject;
using Component = Orbital.Model.Component;

namespace Orbital.Controllers.Factories
{
    [CreateAssetMenu(menuName = "World/WorldPreset", fileName = "World Preset")]
    public class WorldPreset : ScriptableObject, IFactory<World>
    {
        [SerializeField] private WorldData worldData;
        [Inject] private ISerializer _serializer;
        [Inject] private DiContainer _container;
        public World Create()
        {
            World result = new World();
            ApplyDataToWorld(result);
            return result;
        }

        private const int FinalSetupIteration = 2; 
        private void ApplyDataToWorld(World world)
        {
            List<IEnumerator<int>> iterators = new ();
            
            for (var i = 0; i < worldData.bodies.Length; i++)
            {
                Body newBody = new Body();
                iterators.Add(SetupComponents(newBody, worldData.bodies[i]).GetEnumerator());
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
            Dictionary<ComponentData, Component> createdComponents = new ();
            for (int i = 0; i < bodyData.components.Length; i++)
            {
                Type type = Type.GetType(bodyData.components[i].Type);
                if(type == null) continue;
                Component component = (Component)Activator.CreateInstance(type, new object[] {body});
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
                ComponentData componentData = bodyData.components[i];
                _serializer.Populate(createdComponents[componentData], componentData.Data);
            }

            yield return FinalSetupIteration;
        }
    }
}