using System.Linq;
using Ara3D;
using Orbital.Core;
using Orbital.Core.Bodies;
using Orbital.Core.TrajectorySystem;
using Plugins.LocalPool;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Orbital.View.Map
{
    public class CelestialViewUpdater : ViewUpdater
    {
        private ViewContainer _orbitContainer;
        private ViewContainer? _planetContainer;
        private CelestialViewSettings _settings;
        private IStaticBody _body;
        private readonly Pool<ViewContainer> _pool;
        private ScaleSettings _scaleSettings;
        private ViewContainer? _atmosphereContainer;
        private readonly MaterialPropertyBlock _block = new ();
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        private static readonly int GlossinessId = Shader.PropertyToID("_Glossiness");
        private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
        private static readonly int MetallicId = Shader.PropertyToID("_Metallic");

        public CelestialViewUpdater(IStaticBody model, Transform hierarchyContainer, Pool<ViewContainer> pool, ScriptableObjectContainer.Container settingsContainer, ScaleSettings scaleSettings)
        {
            _pool = pool;
            _settings = settingsContainer.GetAssetsAtType<CelestialViewSettings>().First();
            _scaleSettings = scaleSettings;
            _body = model;
            _orbitContainer = pool.Get();
            _orbitContainer.Transform.SetParent(hierarchyContainer.parent);
            _orbitContainer.MeshFilter.mesh = _settings.orbitMesh;
            _orbitContainer.MeshRenderer.material = _settings.orbitMaterial;
            if (model.MassSystem is CelestialBody)
            {
                _planetContainer = pool.Get();
                _planetContainer.Value.Transform.SetParent(hierarchyContainer);
                _planetContainer.Value.MeshFilter.mesh = _settings.celestialMesh;
                _planetContainer.Value.MeshRenderer.material = _settings.celestialMaterial;
                if (_settings.bodyTable)
                {
                    _settings.bodyTable.Changed += ApplyLook;
                }
                ApplyLook();
            }
            // The root of the tree has no orbit: its orbit is a default struct, and positions from it are NaN.
            if (_body.Orbit.SemiMajorAxis == 0.0 || double.IsNaN(_body.Orbit.SemiMajorAxis))
            {
                _orbitContainer.Transform.gameObject.SetActive(false);
                return;
            }
            DVector3 pos = _body.Orbit.GetPositionFromTrueAnomaly(0);
            Vector3 up = _body.Orbit.RotationMatrix.Up();
            Vector3 fwd = _body.Orbit.RotationMatrix.Forward();
            _orbitContainer.Transform.localPosition = ((Vector3) (pos) - fwd * (float) _body.Orbit.SemiMajorAxis) * _scaleSettings.scale;
            _orbitContainer.Transform.localRotation = Quaternion.LookRotation(fwd, up);
            _orbitContainer.Transform.localScale = new Vector3((float) _body.Orbit.SemiMinorAxis * _scaleSettings.scale, 1, (float) _body.Orbit.SemiMajorAxis * _scaleSettings.scale);
        }
        
        // Disc size is the real radius at the map scale; bodies without a surface facet keep the default material untouched.
        private void ApplyLook()
        {
            if (!_planetContainer.HasValue) return;
            ViewContainer planet = _planetContainer.Value;
            BodyRecord record = null;
            if (_settings.bodyTable && _body is Object named)
            {
                _settings.bodyTable.TryGet(named.name, out record);
            }
            PhysicalFacet physical = record?.GetFacet<PhysicalFacet>();
            AppearanceFacet appearance = record?.GetFacet<AppearanceFacet>();
            SurfaceFacet surface = record?.GetFacet<SurfaceFacet>();
            AtmosphereFacet atmosphere = record?.GetFacet<AtmosphereFacet>();

            float diameter = _settings.fallbackDiameter;
            if (physical != null && physical.radiusMeters > 0)
            {
                diameter = (float) (physical.radiusMeters * 2.0 * _scaleSettings.scale)
                           * Mathf.Max(0f, _scaleSettings.bodySizeMultiplier) * (appearance?.sizeMultiplier ?? 1f);
            }
            planet.Transform.localScale = Vector3.one * diameter;

            planet.MeshRenderer.sharedMaterial = surface != null && surface.material ? surface.material : _settings.celestialMaterial;
            _block.Clear();
            Color color = Color.white;
            if (appearance != null) color = appearance.tint * appearance.brightness;
            if (surface != null) color *= surface.color;
            color.a = appearance?.tint.a ?? 1f;
            _block.SetColor(ColorId, color);
            _block.SetColor(BaseColorId, color);
            if (appearance != null && appearance.emission > 0f)
            {
                _block.SetColor(EmissionId, appearance.tint * appearance.emission);
            }
            if (surface != null)
            {
                _block.SetFloat(GlossinessId, surface.smoothness);
                _block.SetFloat(SmoothnessId, surface.smoothness);
                _block.SetFloat(MetallicId, surface.metallic);
            }
            planet.MeshRenderer.SetPropertyBlock(_block);
            ApplyAtmosphere(atmosphere, diameter);
        }

        private void ApplyAtmosphere(AtmosphereFacet atmosphere, float diameter)
        {
            Material material = atmosphere != null ? (atmosphere.material ? atmosphere.material : _settings.atmosphereMaterial) : null;
            if (!material)
            {
                if (atmosphere != null) Debug.LogWarning($"{_body}: atmosphere has no material (set one on the facet or atmosphereMaterial in CelestialViewSettings)");
                if (_atmosphereContainer.HasValue) _atmosphereContainer.Value.Transform.gameObject.SetActive(false);
                return;
            }
            if (!_atmosphereContainer.HasValue)
            {
                ViewContainer shell = _pool.Get();
                shell.Transform.SetParent(_planetContainer.Value.Transform.parent, false);
                shell.MeshFilter.mesh = _settings.celestialMesh;
                _atmosphereContainer = shell;
            }
            ViewContainer shellContainer = _atmosphereContainer.Value;
            shellContainer.Transform.gameObject.SetActive(true);
            shellContainer.MeshRenderer.sharedMaterial = material;
            shellContainer.Transform.localScale = Vector3.one * (diameter * (1f + atmosphere.thickness));
            Color color = atmosphere.color;
            color.a = atmosphere.opacity;
            _block.Clear();
            _block.SetColor(ColorId, color);
            _block.SetColor(BaseColorId, color);
            shellContainer.MeshRenderer.SetPropertyBlock(_block);
        }

        public override void Update(double time)
        {
        }
        
        public override void Dispose(Pool<ViewContainer> pool)
        {
            if (_settings.bodyTable)
            {
                _settings.bodyTable.Changed -= ApplyLook;
            }
            pool.Put(_orbitContainer);
            if (_planetContainer.HasValue)
            {
                _planetContainer.Value.MeshRenderer.SetPropertyBlock(null);
                pool.Put(_planetContainer.Value);
            }
            if (_atmosphereContainer.HasValue)
            {
                _atmosphereContainer.Value.MeshRenderer.SetPropertyBlock(null);
                pool.Put(_atmosphereContainer.Value);
            }
        }
    }
}