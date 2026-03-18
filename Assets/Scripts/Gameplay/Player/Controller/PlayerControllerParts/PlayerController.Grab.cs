using UnityEngine;

// PlayerController の掴まれ処理部分（partial）
// 掴まれ状態の管理と処理を担当
public sealed partial class PlayerController : IGrabReceiver
{
    [Header("Grab")]
    [SerializeField] private bool canBeGrabbed = true;          // 掴み攻撃を受けるか

    [Header("Grab Debug")]
    [SerializeField] private bool showGrabDebugLog = false;    // デバッグログ表示

    // 掴まれ状態
    private bool isGrabbed = false;                              // 掴まれているか
    private float grabTimer = 0.0f;                              // 掴まれている残り時間
    // プロパティ：外部からアクセス可能な読み取り専用情報
    public bool IsGrabbed => isGrabbed;                          // 掴まれているか

    // 掴みシステムの初期化（メインのAwakeから呼ぶ）
    private void InitializeGrab()
    {
        isGrabbed  = false;
        grabTimer = 0.0f;
    }

    // 掴みシステムの更新（メインのUpdateから呼ぶ）
    private void UpdateGrab(float deltaTime)
    {
        // 掴まれ状態の更新
        if (isGrabbed)
        {
            grabTimer -= deltaTime;
            if (grabTimer <= 0.0f)
            {
                ReleaseGrab();
            }
        }
    }

    // ========================================
    // IGrabReceiverインターフェースの実装
    // ========================================

    // 掴まれた時の処理
    public void OnGrabbed(float duration)
    {
        // 掴まれることができない、既に掴まれている、または無敵状態の場合は無視
        if (!canBeGrabbed || isGrabbed || IsInvincible)
        {
            LogGrab("Grab ignored (cannot be grabbed or invincible)");
            return;
        }

        isGrabbed = true;
        grabTimer = duration;

        LogGrab($"Grabbed for {duration} seconds");

        // 掴まれた時の処理
        OnGrabStart();
    }

    // 掴みから解放される
    private void ReleaseGrab()
    {
        if (!isGrabbed)
        {
            return;
        }

        isGrabbed = false;
        grabTimer = 0.0f;

        LogGrab("Released from grab");

        // 解放時の処理
        OnGrabEnd();
    }

    // ========================================
    // 内部処理・イベント
    // ========================================

    // 掴まれた時の処理
    private void OnGrabStart()
    {
        // 移動を制限するため、速度をリセットする。
        // 実際の移動制限は FixedUpdate で is_grabbed をチェックして行われる。
        if (rb != null)
        {
            rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);
        }

        // TODO: 掴まれ時の演出
        // 例：掴まれアニメーション再生、エフェクト再生など
    }

    // 掴みから解放された時の処理
    private void OnGrabEnd()
    {
        // TODO: 解放時の演出
        // 例：移動制限を解除、通常アニメーションに戻すなど
    }

    // ========================================
    // ユーティリティ
    // ========================================

    // 掴みを強制解除（外部から呼び出し可能）
    public void ForceReleaseGrab()
    {
        if (isGrabbed)
        {
            ReleaseGrab();
            LogGrab("Grab force released");
        }
    }

    // デバッグログ出力
    private void LogGrab(string message)
    {
        if (!showGrabDebugLog)
        {
            return;
        }

        Debug.Log($"[PlayerGrab] {message}");
    }
}
