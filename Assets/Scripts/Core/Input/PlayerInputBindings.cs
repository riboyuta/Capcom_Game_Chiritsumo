using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Input
{
    /// 1つの意味入力に対する物理入力設定。
    /// キーボードは最大2キー、ゲームパッドは1ボタンを持つ。
    [Serializable]
    public sealed class PlayerActionBinding
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

        public PlayerActionBinding(
            Key primaryKeyboardKey,
            Key secondaryKeyboardKey,
            RawGamepadButton gamepadButton)
        {
            _primaryKeyboardKey = primaryKeyboardKey;
            _secondaryKeyboardKey = secondaryKeyboardKey;
            _gamepadButton = gamepadButton;
        }
    }

    /// プレイヤー入力の Inspector 設定。

    [Serializable]
    public sealed class PlayerInputBindings
    {
        [Header("Player Input Config")]
        [SerializeField]
        private PlayerActionBinding _jump =
            new PlayerActionBinding(
                primaryKeyboardKey: Key.Space,
                secondaryKeyboardKey: Key.Z,
                gamepadButton: RawGamepadButton.A);

        [SerializeField]
        private PlayerActionBinding _step =
            new PlayerActionBinding(
                primaryKeyboardKey: Key.LeftShift,
                secondaryKeyboardKey: Key.None,
                gamepadButton: RawGamepadButton.RightTrigger);

        public PlayerActionBinding Jump => _jump;
        public PlayerActionBinding Step => _step;
    }
}