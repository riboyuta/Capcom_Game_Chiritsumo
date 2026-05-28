using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Input
{
    [Serializable]
    public sealed class PlayerInputBindings
    {

        public enum PlayerMouseInputAction
        {
            None,
            Jump,
            Dash,
            Stomp,
            Grab
        }


        [Header("入力設定: ジャンプ")]
        [Tooltip("ジャンプ操作に割り当てる入力設定です。Celeste寄りの配置として、キーボードはCを主キー、Spaceを副キーにしています。")]
        [SerializeField]
        private InputActionBinding jump =
            new InputActionBinding(
                primaryKeyboardKey: Key.C,
                secondaryKeyboardKey: Key.Space,
                gamepadButton: RawGamepadButton.A,
                secondaryGamepadButton: RawGamepadButton.None);

        [Header("入力設定: ダッシュ")]
        [Tooltip("ダッシュ操作に割り当てる入力設定です。Celeste寄りに、押した瞬間の方向入力と組み合わせて使う前提です。")]
        [SerializeField]
        private InputActionBinding dash =
            new InputActionBinding(
                primaryKeyboardKey: Key.X,
                secondaryKeyboardKey: Key.LeftShift,
                gamepadButton: RawGamepadButton.X,
                secondaryGamepadButton: RawGamepadButton.None);

        [Header("入力設定: ストンピング")]
        [Tooltip("ストンピング操作に割り当てる入力設定です。キーボードは V を主キー、RightShift を副キーに設定しています。ゲームパッドはBを主ボタン、LeftTriggerを副ボタンとして扱います。")]
        [SerializeField]
        private InputActionBinding stomp =
            new InputActionBinding(
                primaryKeyboardKey: Key.V,
                secondaryKeyboardKey: Key.RightShift,
                gamepadButton: RawGamepadButton.B,
                secondaryGamepadButton: RawGamepadButton.LeftTrigger);

        [Header("入力設定: つかむ")]
        [Tooltip("壁つかみ・壁登りに割り当てる入力設定です。押している間だけ有効になるホールド入力として使います。")]
        [SerializeField]
        private InputActionBinding grab =
            new InputActionBinding(
                primaryKeyboardKey: Key.Z,
                secondaryKeyboardKey: Key.LeftCtrl,
                gamepadButton: RawGamepadButton.RightTrigger,
                secondaryGamepadButton: RawGamepadButton.None);

        [Header("入力設定: マウス")]
        [Tooltip("左クリックで実行するプレイヤーアクションです。None の場合、左クリックはどのアクションにも使いません。")]
        [SerializeField]
        private PlayerMouseInputAction leftMouseAction = PlayerMouseInputAction.Dash;

        [Header("入力設定: マウス")]
        [Tooltip("右クリックで実行するプレイヤーアクションです。None の場合、右クリックはどのアクションにも使いません。")]
        [SerializeField]
        private PlayerMouseInputAction rightMouseAction = PlayerMouseInputAction.Jump;

        public InputActionBinding Jump => jump;
        public InputActionBinding Dash => dash;
        public InputActionBinding Stomp => stomp;
        public InputActionBinding Grab => grab;
        public PlayerMouseInputAction LeftMouseAction => leftMouseAction;
        public PlayerMouseInputAction RightMouseAction => rightMouseAction;
    }
}