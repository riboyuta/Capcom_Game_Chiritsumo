using UnityEngine;

/// <summary>
/// PlayerController の掴まれ処理部分（partial）
/// 掴まれ状態の管理と処理を担当
/// </summary>
public sealed partial class PlayerController : IGrabReceiver
{
    [Header("Grab")]
    [SerializeField] private bool m_can_be_grabbed = true;          // 掴み攻撃を受けるか

    [Header("Grab Debug")]
    [SerializeField] private bool m_show_grab_debug_log = false;    // デバッグログ表示

    // 掴まれ状態
    private bool m_is_grabbed = false;                              // 掴まれているか
    private float m_grab_timer = 0.0f;                              // 掴まれている残り時間

    // プロパティ：外部からアクセス可能な読み取り専用情報
    public bool IsGrabbed => m_is_grabbed;                          // 掴まれているか

    /// <summary>
    /// 掴みシステムの初期化（メインのAwakeから呼ぶ）
    /// </summary>
    private void InitializeGrab()
    {
        m_is_grabbed = false;
        m_grab_timer = 0.0f;
    }

    /// <summary>
    /// 掴みシステムの更新（メインのUpdateから呼ぶ）
    /// </summary>
    private void UpdateGrab(float deltaTime)
    {
        // 掴まれ状態の更新
        if (m_is_grabbed)
        {
            m_grab_timer -= deltaTime;
            if (m_grab_timer <= 0.0f)
            {
                ReleaseGrab();
            }
        }
    }

    // ========================================
    // IGrabReceiverインターフェースの実装
    // ========================================

    /// <summary>
    /// 掴まれた時の処理
    /// </summary>
    public void OnGrabbed(float duration)
    {
        // 掴まれることができない、既に掴まれている、または無敵状態の場合は無視
        if (!m_can_be_grabbed || m_is_grabbed || IsInvincible)
        {
            LogGrab("Grab ignored (cannot be grabbed or invincible)");
            return;
        }

        m_is_grabbed = true;
        m_grab_timer = duration;

        LogGrab($"Grabbed for {duration} seconds");

        // 掴まれた時の処理
        OnGrabStart();
    }

    /// <summary>
    /// 掴みから解放される
    /// </summary>
    private void ReleaseGrab()
    {
        if (!m_is_grabbed)
        {
            return;
        }

        m_is_grabbed = false;
        m_grab_timer = 0.0f;

        LogGrab("Released from grab");

        // 解放時の処理
        OnGrabEnd();
    }

    // ========================================
    // 内部処理・イベント
    // ========================================

    /// <summary>
    /// 掴まれた時の処理
    /// </summary>
    private void OnGrabStart()
    {
        // 移動を制限するため、速度をリセットする。
        // 実際の移動制限は FixedUpdate で m_is_grabbed をチェックして行われる。
        if (rb != null)
        {
            rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);
        }

        // TODO: 掴まれ時の演出
        // 例：掴まれアニメーション再生、エフェクト再生など
    }

    /// <summary>
    /// 掴みから解放された時の処理
    /// </summary>
    private void OnGrabEnd()
    {
        // TODO: 解放時の演出
        // 例：移動制限を解除、通常アニメーションに戻すなど
    }

    // ========================================
    // ユーティリティ
    // ========================================

    /// <summary>
    /// 掴みを強制解除（外部から呼び出し可能）
    /// </summary>
    public void ForceReleaseGrab()
    {
        if (m_is_grabbed)
        {
            ReleaseGrab();
            LogGrab("Grab force released");
        }
    }

    /// <summary>
    /// デバッグログ出力
    /// </summary>
    private void LogGrab(string message)
    {
        if (!m_show_grab_debug_log)
        {
            return;
        }

        Debug.Log($"[PlayerGrab] {message}");
    }
}
