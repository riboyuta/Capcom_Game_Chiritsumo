using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Input
{
    // デバッグ用操作の入力割り当て設定をまとめるデータクラス。
    // 例:
    // - デバッグ表示の切り替え
    // - シーンのリロード
    // - 次 / 前シーンへの移動
    //
    // 実際の入力判定やシーン切り替え処理は別クラス側で行い、
    // このクラスは「どのキーに割り当てるか」を保持する責務だけを持つ。
    [Serializable]
    public sealed class DebugInputBindings
    {
        [Header("デバッグ入力: 表示切り替えキー")]
        [Tooltip("デバッグ表示の ON / OFF を切り替えるキーボードキーです。通常は開発中のみ使用します。")]
        // デバッグ表示の切り替えに使うキー。
        [SerializeField]
        private Key _toggleDebugViewKey = Key.F1;

        [Header("デバッグ入力: シーン再読み込みキー")]
        [Tooltip("現在のシーンを再読み込みするキーボードキーです。リセット確認や繰り返しテスト用に使います。")]
        // 現在のシーンを再読み込みするキー。
        [SerializeField]
        private Key _reloadSceneKey = Key.F8;

        [Header("デバッグ入力: 次シーン移動キー")]
        [Tooltip("次のシーンへ切り替えるキーボードキーです。複数シーンの確認を素早く行いたいときに使います。")]
        // 次のシーンへ移動するキー。
        [SerializeField]
        private Key _nextSceneKey = Key.F9;

        [Header("デバッグ入力: 前シーン移動キー")]
        [Tooltip("前のシーンへ切り替えるキーボードキーです。前後のシーン確認を往復したいときに使います。")]
        // 前のシーンへ移動するキー。
        [SerializeField]
        private Key _previousSceneKey = Key.F10;

        // 外部参照用の読み取り専用プロパティ。
        // 実際の入力判定側はこのプロパティ経由で設定値を読む。
        public Key ToggleDebugViewKey => _toggleDebugViewKey;
        public Key ReloadSceneKey => _reloadSceneKey;
        public Key NextSceneKey => _nextSceneKey;
        public Key PreviousSceneKey => _previousSceneKey;
    }
}