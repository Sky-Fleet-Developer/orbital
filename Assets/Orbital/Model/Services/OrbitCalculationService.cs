using System.Collections.Generic;
using System.Linq;
using Orbital.Model.Components;
using Orbital.Model.TrajectorySystem;
using Zenject;

namespace Orbital.Model.Services
{
    public class OrbitCalculationService
    {
        //public const float GravityThreshold = 0.005f;
        public const double G = 6.67430e-11;

        private List<CelestialBody> _celestialBodies = new List<CelestialBody>();
        
        /*public void RegisterBody(DiContainer containerWithBody)
        {
            CelestialSystemComponent celestialComponent = containerWithBody.TryResolve<CelestialSystemComponent>();
            //if (celestialComponent != null)
            {
                var cBody = new CelestialBody(celestialComponent.GetSettings());
                containerWithBody.Bind<CelestialBody>().FromInstance(cBody);
                _celestialBodies.Add(cBody);
            }
        }*/

        public void RegisterTree(IMass root)
        {
            List<IMass> allElements = root.GetRecursively().ToList();
            
            
        }

        public void RecalculateCelestialTrajectories()
        {
            
            
        }
    }
}
