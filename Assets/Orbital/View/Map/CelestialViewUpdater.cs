using System.Linq;
using Ara3D;
using Orbital.Core;
using Orbital.Core.TrajectorySystem;
using Plugins.LocalPool;
using UnityEngine;

namespace Orbital.View.Map
{
    public class CelestialViewUpdater : ViewUpdater
    {
        private ViewContainer _orbitContainer;
        private ViewContainer? _planetContainer;
        private CelestialViewSettings _settings;
        private IStaticBody _body;
        private ScaleSettings _scaleSettings;
        
        public CelestialViewUpdater(IStaticBody model, Transform hierarchyContainer, Pool<ViewContainer> pool, ScriptableObjectContainer.Container settingsContainer, ScaleSettings scaleSettings)
        {
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
                _planetContainer.Value.Transform.localScale = Vector3.one * 0.08f;
            }
            DVector3 pos = _body.Orbit.GetPositionFromTrueAnomaly(0);
            Vector3 up = _body.Orbit.RotationMatrix.Up();
            Vector3 fwd = _body.Orbit.RotationMatrix.Forward();
            _orbitContainer.Transform.localPosition = ((Vector3) (pos) - fwd * (float) _body.Orbit.SemiMajorAxis) * _scaleSettings.scale;
            _orbitContainer.Transform.localRotation = Quaternion.LookRotation(fwd, up);
            _orbitContainer.Transform.localScale = new Vector3((float) _body.Orbit.SemiMinorAxis * _scaleSettings.scale, 1, (float) _body.Orbit.SemiMajorAxis * _scaleSettings.scale);
        }
        
        public override void Update(double time)
        {
        }
        
        public override void Dispose(Pool<ViewContainer> pool)
        {
            pool.Put(_orbitContainer);
            if(_planetContainer.HasValue) pool.Put(_planetContainer.Value);
        }
    }
}