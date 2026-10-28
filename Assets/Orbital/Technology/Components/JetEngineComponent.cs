using Ara3D;
using Leopotam.Ecs;
using UnityEngine;

namespace Orbital.Technology.Components
{
    // Superseded by the flight components (FlightComponents.cs) and the flight systems of stage 2b. Kept only because TestOrbit.prefab references
    // DynamicBodyAdapter, which still uses it (with the legacy Input in TestInputSystem, which cannot run with the new Input System only).
    public struct JetEngineComponent
    {
        public Vector3 acceleration;
    }
}