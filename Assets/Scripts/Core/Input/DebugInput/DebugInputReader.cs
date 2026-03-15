using System;

namespace Game.Input
{
    // デバッグ用ショートカット入力を読むクラス。
    // 責務は「デバッグ入力が有効かを考慮したうえで、
    // 各デバッグアクションの押下状態を公開すること」。
    public sealed class DebugInputReader
    {
        // デバッグ入力を有効にするかどうか。
        // UNITY_EDITOR のときだけ true にすることで、
        // エディタ実行中のみデバッグショートカットを使えるようにしている。
        // ビルド後は false になるため、誤って本番操作に混ざりにくい。
        private bool IsDebugInputEnabled =
#if UNITY_EDITOR 
            true;
#else
            false;
#endif

        // 生入力の供給元。
        // 実際のキー状態取得はこのオブジェクトに委譲する。
        private readonly RawInputSource _rawInputSource;

        // デバッグ入力のキー割り当て定義。
        // どのキーがどのデバッグ操作に対応するかを保持する。
        private readonly DebugInputBindings _bindings;

        // デバッグ表示の ON/OFF 切り替え入力。
        // 無効時は常に false を返す。
        public bool ToggleDebugViewPressed
        {
            get
            {
                // デバッグ入力が無効なら押下扱いしない。
                if (!IsDebugInputEnabled)
                {
                    return false;
                }

                // このフレームでキーが押された瞬間だけ true。
                return _rawInputSource.WasKeyPressedThisFrame(_bindings.ToggleDebugViewKey);
            }
        }

        // 現在シーンのリロード入力。
        // 無効時は常に false を返す。
        public bool ReloadScenePressed
        {
            get
            {
                // デバッグ入力が無効なら押下扱いしない。
                if (!IsDebugInputEnabled)
                {
                    return false;
                }

                // このフレームでキーが押された瞬間だけ true。
                return _rawInputSource.WasKeyPressedThisFrame(_bindings.ReloadSceneKey);
            }
        }

        // 次のシーンへ進む入力。
        // 無効時は常に false を返す。
        public bool NextScenePressed
        {
            get
            {
                // デバッグ入力が無効なら押下扱いしない。
                if (!IsDebugInputEnabled)
                {
                    return false;
                }

                // このフレームでキーが押された瞬間だけ true。
                return _rawInputSource.WasKeyPressedThisFrame(_bindings.NextSceneKey);
            }
        }

        // 前のシーンへ戻る入力。
        // 無効時は常に false を返す。
        public bool PreviousScenePressed
        {
            get
            {
                // デバッグ入力が無効なら押下扱いしない。
                if (!IsDebugInputEnabled)
                {
                    return false;
                }

                // このフレームでキーが押された瞬間だけ true。
                return _rawInputSource.WasKeyPressedThisFrame(_bindings.PreviousSceneKey);
            }
        }

        public DebugInputReader(RawInputSource rawInputSource, DebugInputBindings bindings)
        {
            // 依存が欠けると入力解決できないため、生成時点で null を検出する。
            _rawInputSource = rawInputSource ?? throw new ArgumentNullException(nameof(rawInputSource));
            _bindings = bindings ?? throw new ArgumentNullException(nameof(bindings));
        }
    }
}