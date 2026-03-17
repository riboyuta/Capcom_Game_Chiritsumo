using UnityEngine;

/// <summary>
/// PlayerController の体力・ダメージ処理部分（partial）
/// HP管理、ダメージ処理、無敵時間、死亡処理、ノックバック処理を担当
/// </summary>
public sealed partial class PlayerController : IDamageable
{
    [Header("Health")]
    [SerializeField] private int m_max_health = 3;                  // 最大体力
    [SerializeField] private bool m_invincible = false;             // 無敵モード（デバッグ用）

    [Header("Invincibility")]
    [SerializeField] private float m_invincibility_duration = 1.0f; // ダメージ後の無敵時間（秒）

    [Header("Knockback")]
    [SerializeField] private float m_knockback_resistance = 1.0f;   // ノックバック耐性（1.0=通常、0.5=半減）
    [SerializeField] private float m_knockback_duration = 0.25f;    // ノックバック継続時間（秒）
    [SerializeField] private bool m_decay_knockback_over_time = true; // 時間経過でノックバックを弱めるか

    [Header("Health Debug")]
    [SerializeField] private bool m_show_health_debug_log = false;  // デバッグログ表示

    // 体力・ダメージ関連の状態
    private int m_current_health;                                   // 現在の体力
    private float m_invincibility_timer = 0.0f;                     // 無敵時間の残り時間

    // ノックバック関連
    private float m_knockback_timer = 0.0f;                         // ノックバック残り時間
    private Vector3 m_knockback_initial_velocity = Vector3.zero;    // ノックバック開始時の速度
    private Vector3 m_knockback_velocity = Vector3.zero;            // 現在のノックバック速度
    private bool m_is_knockback = false;                            // ノックバック中かどうか

    // プロパティ：外部からアクセス可能な読み取り専用情報
    public int CurrentHealth => m_current_health;
    public int MaxHealth => m_max_health;
    public bool IsInvincible => m_invincible || m_invincibility_timer > 0.0f || m_is_grabbed;
    public bool IsKnockback => m_is_knockback;

    /// <summary>
    /// 体力システムの初期化（メインのAwakeから呼ぶ）
    /// </summary>
    private void InitializeHealth()
    {
        m_current_health = m_max_health;
        m_invincibility_timer = 0.0f;

        m_knockback_timer = 0.0f;
        m_knockback_initial_velocity = Vector3.zero;
        m_knockback_velocity = Vector3.zero;
        m_is_knockback = false;
    }

    /// <summary>
    /// 体力システムの更新（メインのUpdateから呼ぶ）
    /// </summary>
    private void UpdateHealth(float deltaTime)
    {
        // 無敵時間の更新
        if (m_invincibility_timer > 0.0f)
        {
            m_invincibility_timer -= deltaTime;
            if (m_invincibility_timer < 0.0f)
            {
                m_invincibility_timer = 0.0f;
            }
        }

        // ノックバック時間の更新
        if (m_is_knockback)
        {
            m_knockback_timer -= deltaTime;

            if (m_knockback_timer <= 0.0f)
            {
                EndKnockback();
            }
            else if (m_decay_knockback_over_time && m_knockback_duration > 0.0f)
            {
                float normalized = Mathf.Clamp01(m_knockback_timer / m_knockback_duration);
                m_knockback_velocity = m_knockback_initial_velocity * normalized;
            }
        }
    }

    // ========================================
    // IDamageableインターフェースの実装
    // ========================================

    /// <summary>
    /// ダメージを受ける処理
    /// </summary>
    public void TakeDamage(int damage, Vector3 hit_direction, float knockback_force)
    {
        if (IsInvincible)
        {
            LogHealth("Damage ignored (invincible)");
            return;
        }

        // ダメージ適用
        m_current_health -= damage;
        if (m_current_health < 0)
        {
            m_current_health = 0;
        }

        LogHealth($"Took {damage} damage. Health: {m_current_health}/{m_max_health}");

        // ノックバック適用
        if (knockback_force > 0.0f && rb != null)
        {
            float actual_knockback = knockback_force * m_knockback_resistance;
            StartKnockback(hit_direction, actual_knockback);
            LogHealth($"Knockback started: dir={hit_direction}, force={actual_knockback}");
        }

        // 無敵時間開始
        m_invincibility_timer = m_invincibility_duration;

        // ダメージ時の演出などを呼ぶ
        OnDamaged(damage, hit_direction, knockback_force);

        // 死亡チェック
        if (m_current_health <= 0)
        {
            OnDeath();
        }
    }

    // ========================================
    // 敵の体当たり用（SendMessageで呼ばれる）
    // ========================================

    /// <summary>
    /// 即死処理（敵の体当たりなど）
    /// </summary>
    public void Kill()
    {
        if (IsInvincible)
        {
            LogHealth("Kill ignored (invincible)");
            return;
        }

        LogHealth("Killed by instant death!");
        m_current_health = 0;
        OnDeath();
    }

    // ========================================
    // ノックバック内部処理
    // ========================================

    /// <summary>
    /// ノックバック開始
    /// </summary>
    private void StartKnockback(Vector3 hit_direction, float knockback_force)
    {
        Vector3 direction = hit_direction.normalized;

        // 疑似3D横スクなのでZは固定
        direction.z = 0.0f;

        //// 真上真下すぎる場合の最低限の横成分補正
        //if (Mathf.Abs(direction.x) < 0.01f)
        //{
        //    direction.x = isFacingRight ? -1.0f : 1.0f;
        //}

        direction = direction.normalized;

        m_is_knockback = true;
        m_knockback_timer = m_knockback_duration;
        m_knockback_initial_velocity = direction * knockback_force;
        m_knockback_velocity = m_knockback_initial_velocity;
    }

    /// <summary>
    /// ノックバック終了
    /// </summary>
    private void EndKnockback()
    {
        m_is_knockback = false;
        m_knockback_timer = 0.0f;
        m_knockback_initial_velocity = Vector3.zero;
        m_knockback_velocity = Vector3.zero;

        if (rb != null)
        {
            Vector3 velocity = rb.linearVelocity;
            velocity.x = 0.0f;
            rb.linearVelocity = velocity;
        }

        LogHealth("Knockback ended");
    }

    /// <summary>
    /// ノックバック速度を適用（FixedUpdateから呼ばれる）
    /// </summary>
    private void ApplyKnockbackVelocity()
    {
        if (rb == null || !m_is_knockback)
        {
            return;
        }

        // ノックバック速度を適用（Y軸の重力は保持）
        Vector3 velocity = rb.linearVelocity;
        velocity.x = m_knockback_velocity.x;

        // ノックバック方向にY成分がある場合は適用
        if (Mathf.Abs(m_knockback_velocity.y) > 0.01f)
        {
            velocity.y = m_knockback_velocity.y;
        }

        rb.linearVelocity = velocity;
    }

    // ========================================
    // 内部処理・イベント
    // ========================================

    /// <summary>
    /// ダメージを受けた時の演出処理
    /// </summary>
    private void OnDamaged(int damage, Vector3 hit_direction, float knockback_force)
    {
        // TODO: ダメージエフェクト、サウンド、アニメーションなどを再生
    }

    /// <summary>
    /// 死亡処理
    /// </summary>
    private void OnDeath()
    {
        LogHealth("Player died!");

        // 掴まれ状態を解除する。
        if (m_is_grabbed)
        {
            ForceReleaseGrab();
        }

        // ノックバック状態も解除する。
        if (m_is_knockback)
        {
            EndKnockback();
        }

        // TODO: 死亡処理
    }

    // ========================================
    // ユーティリティ
    // ========================================

    /// <summary>
    /// 体力を回復する（アイテムなど）
    /// </summary>
    public void Heal(int amount)
    {
        int previous_health = m_current_health;
        m_current_health = Mathf.Min(m_current_health + amount, m_max_health);
        int actual_heal = m_current_health - previous_health;

        if (actual_heal > 0)
        {
            LogHealth($"Healed {actual_heal}. Health: {m_current_health}/{m_max_health}");
        }
    }

    /// <summary>
    /// デバッグログ出力
    /// </summary>
    private void LogHealth(string message)
    {
        if (!m_show_health_debug_log)
        {
            return;
        }

        Debug.Log($"[PlayerHealth] {message}");
    }
}