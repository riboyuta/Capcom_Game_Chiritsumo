public sealed partial class PlayerController
{
    // HoodRecover の見た目上の復帰タイミングに到達したことを通知する。
    // PlayerModelView または Animation Event から呼ばれる想定。
    internal void CompleteHoodRecoverVisual()
    {
        runtimeState.hoodVisualState = PlayerHoodVisualState.Up;
        runtimeState.requestHoodRecoverThisFrame = false;
    }

    // HoodRecover を即時完了させる。
    // リセット、デバッグ、アニメイベント未設定時の保険に使う。
    internal void ForceHoodVisualUp()
    {
        runtimeState.hoodVisualState = PlayerHoodVisualState.Up;
        runtimeState.requestHoodRecoverThisFrame = false;
    }
}