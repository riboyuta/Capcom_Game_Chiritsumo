using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Input
{
    [Serializable]
    public sealed class PlayerInputBindings
    {
        [Header("入力設定: ジャンプ")]
        [Tooltip("ジャンプ操作に割り当てる入力設定です。Celeste寄りの配置として、キーボードはCを主キー、Spaceを副キーにしています。")]
        [SerializeField]
        private InputActionBinding jump =
            new InputActionBinding(
                primaryKeyboardKey: Key.C,
                secondaryKeyboardKey: Key.Space,
                gamepadButton: RawGamepadButton.A);

        [Header("入力設定: ダッシュ")]
        [Tooltip("ダッシュ操作に割り当てる入力設定です。Celeste寄りに、押した瞬間の方向入力と組み合わせて使う前提です。")]
        [SerializeField]
        private InputActionBinding dash =
            new InputActionBinding(
                primaryKeyboardKey: Key.X,
                secondaryKeyboardKey: Key.LeftShift,
                gamepadButton: RawGamepadButton.X);

        [Header("入力設定: ストンピング")]
        [Tooltip("ストンピング操作に割り当てる入力設定です。キーボード Stomp は Down 入力で扱うため、専用キーは基本使用しません。")]
        [SerializeField]
        private InputActionBinding stomp =
            new InputActionBinding(
                primaryKeyboardKey: Key.None,
                secondaryKeyboardKey: Key.None,
                gamepadButton: RawGamepadButton.B);

        [SerializeField]
        private InputActionBinding stompAlternate =
            new InputActionBinding(
                primaryKeyboardKey: Key.None,
                secondaryKeyboardKey: Key.None,
                gamepadButton: RawGamepadButton.LeftTrigger);

        [Header("入力設定: つかむ")]
        [Tooltip("壁つかみ・壁登りに割り当てる入力設定です。押している間だけ有効になるホールド入力として使います。")]
        [SerializeField]
        private InputActionBinding grab =
            new InputActionBinding(
                primaryKeyboardKey: Key.Z,
                secondaryKeyboardKey: Key.LeftCtrl,
                gamepadButton: RawGamepadButton.RightTrigger);

        public InputActionBinding Jump => jump;
        public InputActionBinding Dash => dash;
        public InputActionBinding Stomp => stomp;
        public InputActionBinding StompAlternate => stompAlternate;
        public InputActionBinding Grab => grab;
    }
}