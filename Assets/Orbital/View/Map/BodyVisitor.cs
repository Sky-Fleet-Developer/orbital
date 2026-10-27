using System;
using System.Linq;
using Cinemachine;
using Orbital.Core;
using Orbital.Core.Navigation;
using UnityEngine;
using UnityEngine.InputSystem;
using Zenject;
using Object = UnityEngine.Object;

namespace Orbital.View.Map
{
    /// <summary>
    /// Runtime tool: M toggles a Cinemachine camera that orbits any body of the world tree.
    /// Arrows walk the tree: Up/Down along the expanded list, Left = inside (expand / first child), Right = outside (collapse / parent).
    /// Mouse scroll = exponential zoom, mouse delta = orbit.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class BodyVisitor : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private float lookSensitivity = 0.15f;
        [SerializeField] private float zoomPerNotch = 0.2f;
        [SerializeField] private float minDistance = 0.02f;
        [SerializeField] private float maxDistance = 100000f;
        [SerializeField] private float fallbackDistance = 0.4f;
        [SerializeField] private float followSharpness = 10f;
        [Inject] private World _world;
        [Inject] private MapView mapView;

        private InputAction _toggle, _up, _down, _inside, _outside, _zoom, _look;
        private BodyTreeCursor _cursor;
        private Transform _pivot;
        private CinemachineVirtualCamera _vcam;
        private float _yaw, _pitch = 20f;
        private float _distance = 1f, _shownDistance = 1f;
        private bool _active;
        private CursorLockMode _savedLock;
        private bool _savedVisible;

        private void Awake()
        {
            _toggle = new InputAction("Toggle", InputActionType.Button, "<Keyboard>/m");
            _up = new InputAction("Up", InputActionType.Button, "<Keyboard>/upArrow");
            _down = new InputAction("Down", InputActionType.Button, "<Keyboard>/downArrow");
            _inside = new InputAction("Inside", InputActionType.Button, "<Keyboard>/leftArrow");
            _outside = new InputAction("Outside", InputActionType.Button, "<Keyboard>/rightArrow");
            _zoom = new InputAction("Zoom", InputActionType.Value, "<Mouse>/scroll");
            _look = new InputAction("Look", InputActionType.Value, "<Mouse>/delta");
            _toggle.performed += _ => SetActive(!_active);
            _up.performed += _ => Navigate(c => c.MoveUp());
            _down.performed += _ => Navigate(c => c.MoveDown());
            _inside.performed += _ => Navigate(c => c.MoveInside());
            _outside.performed += _ => Navigate(c => c.MoveOutside());
        }

        private void OnEnable()
        {
            _toggle.Enable();
        }

        private void OnDisable()
        {
            SetActive(false);
            _toggle.Disable();
        }

        private void OnDestroy()
        {
            foreach (InputAction action in new[] {_toggle, _up, _down, _inside, _outside, _zoom, _look})
            {
                action.Dispose();
            }
            if (_pivot) Destroy(_pivot.gameObject);
        }

        private void SetActive(bool value)
        {
            if (_active == value) return;
            if (value && !Activate()) return;
            _active = value;
            foreach (InputAction action in new[] {_up, _down, _inside, _outside, _zoom, _look})
            {
                if (value) action.Enable(); else action.Disable();
            }
            if (value)
            {
                _savedLock = Cursor.lockState;
                _savedVisible = Cursor.visible;
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            else
            {
                Cursor.lockState = _savedLock;
                Cursor.visible = _savedVisible;
            }
            if (_vcam) _vcam.gameObject.SetActive(value);
        }

        private bool Activate()
        {
            IStaticBody root = _world != null && _world.IsLoaded ? _world.Model.GetRootBody() : null;
            if (root == null)
            {
                Debug.LogWarning("BodyVisitor: world is not loaded yet");
                return false;
            }
            if (_cursor == null) _cursor = new BodyTreeCursor(root);
            if (!_pivot && !CreateRig()) return false;
            _vcam.gameObject.SetActive(false);
            if (mapView.TryGetBodyTransform(_cursor.Current, out Transform body))
            {
                _pivot.position = body.position;
            }
            FitDistance();
            _shownDistance = _distance;
            return true;
        }

        private bool CreateRig()
        {
            Camera cam = targetCamera ? targetCamera : Camera.main;
            if (!cam)
            {
                Debug.LogWarning("BodyVisitor: no camera to drive");
                return false;
            }
            if (!cam.TryGetComponent(out CinemachineBrain _))
            {
                cam.gameObject.AddComponent<CinemachineBrain>();
            }
            _pivot = new GameObject("BodyVisitorPivot") {hideFlags = HideFlags.DontSave}.transform;
            GameObject camObject = new GameObject("BodyVisitorCamera");
            camObject.transform.SetParent(_pivot, false);
            _vcam = camObject.AddComponent<CinemachineVirtualCamera>();
            _vcam.m_Lens.NearClipPlane = minDistance * 0.1f;
            _vcam.m_Lens.FarClipPlane = Mathf.Max(cam.farClipPlane, maxDistance * 4f);
            return true;
        }

        private void Navigate(Func<BodyTreeCursor, bool> move)
        {
            if (!_active || !move(_cursor)) return;
            FitDistance();
        }

        // Frames the selected body together with its direct children.
        private void FitDistance()
        {
            float fit = fallbackDistance;
            if (mapView.TryGetBodyTransform(_cursor.Current, out Transform body))
            {
                fit = (_cursor.Current.Children ?? Enumerable.Empty<IStaticBody>())
                    .Where(x => mapView.TryGetBodyTransform(x, out _))
                    .Select(x =>
                    {
                        mapView.TryGetBodyTransform(x, out Transform child);
                        return Vector3.Distance(body.position, child.position) * 2.5f;
                    })
                    .DefaultIfEmpty(fallbackDistance).Max();
            }
            _distance = Mathf.Clamp(fit, minDistance, maxDistance);
        }

        private void LateUpdate()
        {
            if (!_active) return;
            Vector2 look = _look.ReadValue<Vector2>();
            _yaw += look.x * lookSensitivity;
            _pitch = Mathf.Clamp(_pitch - look.y * lookSensitivity, -89f, 89f);

            // Wheel units differ between platforms and input backends, so only the direction is used.
            float scroll = _zoom.ReadValue<Vector2>().y;
            if (scroll != 0f)
            {
                _distance = Mathf.Clamp(_distance * Mathf.Exp(-Mathf.Sign(scroll) * zoomPerNotch), minDistance, maxDistance);
            }

            float k = 1f - Mathf.Exp(-followSharpness * Time.unscaledDeltaTime);
            _shownDistance = Mathf.Lerp(_shownDistance, _distance, k);
            if (mapView.TryGetBodyTransform(_cursor.Current, out Transform body))
            {
                _pivot.position = Vector3.Lerp(_pivot.position, body.position, k);
            }
            _pivot.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            _vcam.transform.localPosition = new Vector3(0f, 0f, -_shownDistance);
        }

        // Greybox view of the walkable list.
        private void OnGUI()
        {
            if (!_active) return;
            GUILayout.BeginArea(new Rect(10, 10, 280, Screen.height - 20));
            for (int i = 0; i < _cursor.Visible.Count; i++)
            {
                IStaticBody body = _cursor.Visible[i];
                string marker = !_cursor.HasChildren(body) ? "  " : _cursor.IsExpanded(body) ? "- " : "+ ";
                string label = new string(' ', _cursor.Depths[i] * 3) + marker + (body is Object o ? o.name : body.ToString());
                GUILayout.Label(body == _cursor.Current ? $"<b>> {label}</b>" : "  " + label);
            }
            GUILayout.EndArea();
        }
    }
}
