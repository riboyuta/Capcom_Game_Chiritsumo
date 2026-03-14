using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Input
{
    [Serializable]
    public sealed class SystemInputBindings
    {
        [Header("Direction")]
        [SerializeField]
        private InputActionBinding _up =
            new InputActionBinding(
                primaryKeyboardKey: Key.W,
                secondaryKeyboardKey: Key.UpArrow,
                gamepadButton: RawGamepadButton.DpadUp);

        [SerializeField]
        private InputActionBinding _down =
            new InputActionBinding(
                primaryKeyboardKey: Key.S,
                secondaryKeyboardKey: Key.DownArrow,
                gamepadButton: RawGamepadButton.DpadDown);

        [SerializeField]
        private InputActionBinding _left =
            new InputActionBinding(
                primaryKeyboardKey: Key.A,
                secondaryKeyboardKey: Key.LeftArrow,
                gamepadButton: RawGamepadButton.DpadLeft);

        [SerializeField]
        private InputActionBinding _right =
            new InputActionBinding(
                primaryKeyboardKey: Key.D,
                secondaryKeyboardKey: Key.RightArrow,
                gamepadButton: RawGamepadButton.DpadRight);

        [Header("Actions")]
        [SerializeField]
        private InputActionBinding _submit =
            new InputActionBinding(
                primaryKeyboardKey: Key.Space,
                secondaryKeyboardKey: Key.Z,
                gamepadButton: RawGamepadButton.A);

        [SerializeField]
        private InputActionBinding _cancel =
            new InputActionBinding(
                primaryKeyboardKey: Key.X,
                secondaryKeyboardKey: Key.Escape,
                gamepadButton: RawGamepadButton.B);

        [SerializeField]
        private InputActionBinding _pause =
            new InputActionBinding(
                primaryKeyboardKey: Key.P,
                secondaryKeyboardKey: Key.None,
                gamepadButton: RawGamepadButton.Start);

        public InputActionBinding Up => _up;
        public InputActionBinding Down => _down;
        public InputActionBinding Left => _left;
        public InputActionBinding Right => _right;
        public InputActionBinding Submit => _submit;
        public InputActionBinding Cancel => _cancel;
        public InputActionBinding Pause => _pause;
    }
}