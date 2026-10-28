using System;
using Orbital.Core.Flight;
using UnityEngine.InputSystem;

namespace Orbital.Craft
{
    /// <summary>
    /// Keyboard input of the craft through the new Input System: one <see cref="InputAction"/> per key made in code (no .inputactions asset).
    /// The key -> field mapping and the default paths are the engine-free <see cref="ControlKeys"/>; the controls themselves are the
    /// engine-free <see cref="InputMapper"/> (T, the SAS switch, is one of the control keys; B, the navigation ball mode, is only a view setting
    /// and is read here like C and V). This class only reads the keys and accumulates the presses between two simulation steps.
    /// </summary>
    public sealed class CraftInput : IDisposable
    {
        private readonly InputAction[] _keys = new InputAction[ControlKeys.Count];
        private readonly InputAction _respawn;
        private readonly InputAction _toggleView;
        private readonly InputAction _toggleCamera;
        private readonly InputAction _toggleHelp;
        private readonly InputAction _toggleNavMode;
        private KeyState _accumulated;

        public CraftInput()
        {
            for (int i = 0; i < _keys.Length; i++)
            {
                ControlKey key = (ControlKey) i;
                _keys[i] = new InputAction(key.ToString(), InputActionType.Button, ControlKeys.DefaultPath(key));
                _keys[i].Enable();
            }
            _respawn = Make("Respawn", "<Keyboard>/enter");
            _toggleView = Make("ToggleView", "<Keyboard>/c");
            _toggleCamera = Make("ToggleCamera", "<Keyboard>/v");
            _toggleHelp = Make("ToggleHelp", "<Keyboard>/f1");
            _toggleNavMode = Make("ToggleNavBallMode", "<Keyboard>/b");
        }

        private static InputAction Make(string name, string path)
        {
            InputAction action = new InputAction(name, InputActionType.Button, path);
            action.Enable();
            return action;
        }

        /// <summary>Back to a fresh start (Enter).</summary>
        public bool RespawnPressed => _respawn.WasPressedThisFrame();

        /// <summary>Flight view / map view (C).</summary>
        public bool ToggleViewPressed => _toggleView.WasPressedThisFrame();

        /// <summary>Camera behind the craft / camera on the horizon (V).</summary>
        public bool ToggleCameraPressed => _toggleCamera.WasPressedThisFrame();

        /// <summary>HUD help on/off (F1).</summary>
        public bool ToggleHelpPressed => _toggleHelp.WasPressedThisFrame();

        /// <summary>Navigation ball: velocity relative to the surface / orbital velocity (B). A view setting, not part of the simulation input.</summary>
        public bool ToggleNavModePressed => _toggleNavMode.WasPressedThisFrame();

        /// <summary>Call every frame: remembers keys that were down since the last step.</summary>
        public void Poll()
        {
            _accumulated = ControlKeys.Or(_accumulated, Current());
        }

        /// <summary>Keys for one simulation step: everything pressed since the previous step. The next interval starts from the keys held now.</summary>
        public KeyState Consume()
        {
            KeyState result = ControlKeys.Or(_accumulated, Current());
            _accumulated = Current();
            return result;
        }

        private KeyState Current()
        {
            KeyState state = default;
            for (int i = 0; i < _keys.Length; i++)
            {
                if (_keys[i].IsPressed()) ControlKeys.Set(ref state, (ControlKey) i, true);
            }
            return state;
        }

        public void Dispose()
        {
            foreach (InputAction action in _keys)
            {
                action.Disable();
                action.Dispose();
            }
            foreach (InputAction action in new[] {_respawn, _toggleView, _toggleCamera, _toggleHelp, _toggleNavMode})
            {
                action.Disable();
                action.Dispose();
            }
        }
    }
}
