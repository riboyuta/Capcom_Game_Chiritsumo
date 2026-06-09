public sealed partial class PlayerController
{
    // HoodRecover の見た目上の復帰タイミングに到達したことを通知する。
    // targetVersion が現在のフード世代と一致するときだけ Up に戻す。
    internal void CompleteHoodRecoverVisual(int targetVersion)
    {
        // 古い HoodRecover 完了なら無視する。
        // 例: HoodRecover 中に再ダッシュした場合、
        // 古い完了処理で新しい Down 状態を Up にしない。
        if (runtimeState.hoodVisualVersion != targetVersion)
        {
            return;
        }

        runtimeState.hoodVisualState = PlayerHoodVisualState.Up;
        runtimeState.requestHoodRecoverThisFrame = false;
    }

    // 既存呼び出しが残っていても壊れないように残す。
    internal void CompleteHoodRecoverVisual()
    {
        CompleteHoodRecoverVisual(runtimeState.hoodVisualVersion);
    }

    // HoodRecover を即時完了させる。
    // リセット、デバッグ、アニメイベント未設定時の保険に使う。
    internal void ForceHoodVisualUp()
    {
        runtimeState.hoodVisualVersion++;
        runtimeState.hoodVisualState = PlayerHoodVisualState.Up;
        runtimeState.requestHoodRecoverThisFrame = false;
    }
}