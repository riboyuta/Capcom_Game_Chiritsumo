using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Input
{
    // 1つのゲーム内アクションに対する入力割り当て設定。
    // 例:
    // - ジャンプ
    // - ダッシュ
    // - ストンピング
    //
    // キーボードの主キー / 副キーと、
    // ゲームパッドの主ボタン / 副ボタンをまとめて保持する。
    [Serializable]
    public sealed class InputActionBinding
    {
        [Header("キーボード: 主キー")]
        [Tooltip("このアクションの主入力として使うキーボードキーです。使わない場合は None を指定します。通常はこちらをメインの割り当てにします。")]
        [SerializeField]
        private Key _primaryKeyboardKey = Key.None;

        [Header("キーボード: 副キー")]
        [Tooltip("このアクションの副入力として使うキーボードキーです。予備キーや別配置に対応したい場合に使います。使わない場合は None を指定します。")]
        [SerializeField]
        private Key _secondaryKeyboardKey = Key.None;

        [Header("ゲームパッド: 主ボタン")]
        [Tooltip("このアクションの主入力として使うゲームパッドボタンです。通常はこちらをメインの割り当てにします。")]
        [SerializeField]
        private RawGamepadButton _gamepadButton = RawGamepadButton.None;

        [Header("ゲームパッド: 副ボタン")]
        [Tooltip("このアクションの副入力として使うゲームパッドボタンです。予備ボタンや別配置に対応したい場合に使います。使わない場合は None を指定します。")]
        [SerializeField]
        private RawGamepadButton _secondaryGamepadButton = RawGamepadButton.None;

        public Key PrimaryKeyboardKey => _primaryKeyboardKey;
        public Key SecondaryKeyboardKey => _secondaryKeyboardKey;
        public RawGamepadButton GamepadButton => _gamepadButton;
        public RawGamepadButton SecondaryGamepadButton => _secondaryGamepadButton;

        // 各入力割り当てをまとめて初期化するコンストラクタ。
        // secondaryGamepadButton は省略可能にして、既存の呼び出しを壊さない。
        public InputActionBinding(
            Key primaryKeyboardKey,
            Key secondaryKeyboardKey,
            RawGamepadButton gamepadButton,
            RawGamepadButton secondaryGamepadButton = RawGamepadButton.None)
        {
            _primaryKeyboardKey = primaryKeyboardKey;
            _secondaryKeyboardKey = secondaryKeyboardKey;
            _gamepadButton = gamepadButton;
            _secondaryGamepadButton = secondaryGamepadButton;
        }
    }
}