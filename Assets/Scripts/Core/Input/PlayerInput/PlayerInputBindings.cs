using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Input
{
    // プレイヤー入力の割り当て設定をまとめるデータクラス。
    // PlayerController などの実行側は、このクラスから「ジャンプ」「ステップ」などの入力定義を受け取る。
    [Serializable]
    public sealed class PlayerInputBindings
    {
        [Header("入力設定: ジャンプ")]
        [Tooltip("ジャンプ操作に割り当てる入力設定です。キーボードの主キー / 副キーと、ゲームパッドの対応ボタンをまとめて定義します。")]
        // ジャンプ入力の割り当て設定。
        // 例:
        // - キーボード主キー: Space
        // - キーボード副キー: Z
        // - ゲームパッド: A ボタン
        [SerializeField]
        private InputActionBinding _jump =
            new InputActionBinding(
                primaryKeyboardKey: Key.Space,
                secondaryKeyboardKey: Key.Z,
                gamepadButton: RawGamepadButton.A);

        [Header("入力設定: ステップ")]
        [Tooltip("ステップ操作に割り当てる入力設定です。キーボードの主キー / 副キーと、ゲームパッドの対応ボタンをまとめて定義します。副キーを使わない場合は None を指定します。")]
        // ステップ入力の割り当て設定。
        // 例:
        // - キーボード主キー: LeftShift
        // - キーボード副キー: なし
        // - ゲームパッド: 右トリガー
        [SerializeField]
        private InputActionBinding _step =
            new InputActionBinding(
                primaryKeyboardKey: Key.LeftShift,
                secondaryKeyboardKey: Key.None,
                gamepadButton: RawGamepadButton.RightTrigger);

        // 外部公開用の読み取り専用プロパティ。
        // 呼び出し側はこのプロパティ経由で各入力設定を参照する。
        public InputActionBinding Jump => _jump;
        public InputActionBinding Step => _step;
    }
}