using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Input
{
    [Serializable]
    public sealed class InputActionBinding
    {
        [Header("Keyboard")]
        [SerializeField]
        private Key _primaryKeyboardKey = Key.None;

        [SerializeField]
        private Key _secondaryKeyboardKey = Key.None;

        [Header("Gamepad")]
        [SerializeField]
        private RawGamepadButton _gamepadButton = RawGamepadButton.A;

        public Key PrimaryKeyboardKey => _primaryKeyboardKey;
        public Key SecondaryKeyboardKey => _secondaryKeyboardKey;
        public RawGamepadButton GamepadButton => _gamepadButton;

        public InputActionBinding(
            Key primaryKeyboardKey,
            Key secondaryKeyboardKey,
            RawGamepadButton gamepadButton)
        {
            _primaryKeyboardKey = primaryKeyboardKey;
            _secondaryKeyboardKey = secondaryKeyboardKey;
            _gamepadButton = gamepadButton;
        }
    }
}