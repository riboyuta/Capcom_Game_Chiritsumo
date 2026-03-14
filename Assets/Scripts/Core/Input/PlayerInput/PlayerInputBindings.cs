using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Input
{
    [Serializable]
    public sealed class PlayerInputBindings
    {
        [Header("Player Input Config")]
        [SerializeField]
        private InputActionBinding _jump =
            new InputActionBinding(
                primaryKeyboardKey: Key.Space,
                secondaryKeyboardKey: Key.Z,
                gamepadButton: RawGamepadButton.A);

        [SerializeField]
        private InputActionBinding _step =
            new InputActionBinding(
                primaryKeyboardKey: Key.LeftShift,
                secondaryKeyboardKey: Key.None,
                gamepadButton: RawGamepadButton.RightTrigger);

        public InputActionBinding Jump => _jump;
        public InputActionBinding Step => _step;
    }
}