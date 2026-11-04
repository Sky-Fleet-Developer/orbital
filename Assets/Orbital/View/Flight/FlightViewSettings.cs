using System.Linq;
using ScriptableObjectContainer;
using UnityEngine;

namespace Orbital.View.Flight
{
    /// <summary>
    /// The settings of the flight view, one small asset per topic, all nested in Assets/_game/ViewSettings/ViewSettingsContainer.asset next to the map's.
    /// To add a topic: a new ScriptableObject class in this folder, a field here, then the menu Orbital/Add Missing Flight View Settings.
    /// A section missing from the container does not stop the game: it gets its default values and a warning.
    /// </summary>
    public sealed class FlightViewSettings
    {
        public FlightCameraSettings FlightCamera { get; }
        public SkySettings Sky { get; }
        public FlightLightingSettings Lighting { get; }
        public LocationViewSettings Location { get; }
        public MapViewSettings Map { get; }

        public FlightViewSettings(Container container)
        {
            FlightCamera = Resolve<FlightCameraSettings>(container);
            Sky = Resolve<SkySettings>(container);
            Lighting = Resolve<FlightLightingSettings>(container);
            Location = Resolve<LocationViewSettings>(container);
            Map = Resolve<MapViewSettings>(container);
        }

        private static T Resolve<T>(Container container) where T : ScriptableObject
        {
            T found = container != null ? container.GetAssetsAtType<T>().FirstOrDefault() : null;
            if (found != null) return found;
            Debug.LogWarning($"FlightViewSettings: no {typeof(T).Name} in the view settings container, using the default values");
            return ScriptableObject.CreateInstance<T>();
        }
    }
}
