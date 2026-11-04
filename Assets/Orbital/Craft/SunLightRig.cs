using Orbital.Core.Flight;
using Orbital.View.Flight;
using UnityEngine;

namespace Orbital.Craft
{
    /// <summary>
    /// The one light of the flight view: a directional light for the objects near the craft and the shader values of the lit bodies, both from
    /// <see cref="FlightLightingSettings"/>. The direction comes from the simulation; the rig lives and dies with the view's root.
    /// </summary>
    public sealed class SunLightRig
    {
        private static readonly int BodyDay = Shader.PropertyToID("_OrbitalBodyDay");
        private static readonly int BodyNight = Shader.PropertyToID("_OrbitalBodyNight");

        private readonly FlightLightingSettings _settings;
        private readonly Light _light;

        public SunLightRig(FlightLightingSettings settings, Transform parent)
        {
            _settings = settings;
            GameObject sun = new GameObject("FlightSun");
            sun.transform.SetParent(parent, false);
            sun.transform.rotation = Quaternion.Euler(40f, -30f, 0f); // until the first direction; stays for a world without a body table
            _light = sun.AddComponent<Light>();
            _light.type = LightType.Directional;
            ApplySettings();
        }

        /// <param name="toSun">Unit direction from the eye to the Sun.</param>
        public void Point(Vec3 toSun)
        {
            _light.transform.rotation = Quaternion.LookRotation(CraftPresenter.ToVector(toSun * -1.0));
            ApplySettings();
        }

        private void ApplySettings()
        {
            _light.color = _settings.sunColor;
            _light.intensity = _settings.sunIntensity;
            Shader.SetGlobalFloat(BodyDay, _settings.dayBrightness);
            Shader.SetGlobalFloat(BodyNight, _settings.nightBrightness);
        }
    }
}
