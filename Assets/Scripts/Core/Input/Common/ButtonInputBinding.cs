using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Input
{
    // 1つのゲーム内アクションに対する入力割り当て設定。
    // 例:
    // - ジャンプ
    // - ステップ
    //
    // キーボードの主キー / 副キーと、ゲームパッドの対応ボタンをまとめて保持する。
    [Serializable]
    public sealed class InputActionBinding
    {
        [Header("キーボード: 主キー")]
        [Tooltip("このアクションの主入力として使うキーボードキーです。使わない場合は None を指定します。通常はこちらをメインの割り当てにします。")]
        // キーボード側の主入力キー。
        // 通常は最もよく使うキーをここに置く。
        [SerializeField]
        private Key _primaryKeyboardKey = Key.None;

        [Header("キーボード: 副キー")]
        [Tooltip("このアクションの副入力として使うキーボードキーです。予備キーや別配置に対応したい場合に使います。使わない場合は None を指定します。")]
        // キーボード側の副入力キー。
        // 主キーとは別の予備キーを割り当てたいときに使う。
        [SerializeField]
        private Key _secondaryKeyboardKey = Key.None;

        [Header("ゲームパッド: ボタン")]
        [Tooltip("このアクションに対応するゲームパッド入力です。キーボードとは別に、パッド操作時の対応ボタンを定義します。")]
        // ゲームパッド側の対応ボタン。
        [SerializeField]
        private RawGamepadButton _gamepadButton = RawGamepadButton.A;

        // 外部参照用の読み取り専用プロパティ。
        // 実際の入力判定側はこのプロパティ経由で設定値を読む。
        public Key PrimaryKeyboardKey => _primaryKeyboardKey;
        public Key SecondaryKeyboardKey => _secondaryKeyboardKey;
        public RawGamepadButton GamepadButton => _gamepadButton;

        // 各入力割り当てをまとめて初期化するコンストラクタ。
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