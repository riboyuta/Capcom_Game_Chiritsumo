using UnityEngine;

// PlayerController の体力・ダメージ処理部分（partial）
// HP管理、ダメージ処理、無敵時間、死亡処理、ノックバック処理を担当
public sealed partial class PlayerController : IDamageable
{
    [Header("Health")]
    [SerializeField] private int max_health = 3;                  // 最大体力
    [SerializeField] private bool invincible = false;             // 無敵モード（デバッグ用）

    [Header("Invincibility")]
    [SerializeField] private float invincibility_duration = 1.0f; // ダメージ後の無敵時間（秒）

    [Header("Knockback")]
    [SerializeField] private float knockback_resistance = 1.0f;   // ノックバック耐性（1.0=通常、0.5=半減）
    [SerializeField] private float knockback_duration = 0.25f;    // ノックバック継続時間（秒）
    [SerializeField] private bool decay_knockback_over_time = true; // 時間経過でノックバックを弱めるか

    [Header("Health Debug")]
    [SerializeField] private bool show_health_debug_log = false;  // デバッグログ表示

    // 体力・ダメージ関連の状態
    private int current_health;                                   // 現在の体力
    private float invincibility_timer = 0.0f;                     // 無敵時間の残り時間

    // ノックバック関連
    private float knockback_timer = 0.0f;                         // ノックバック残り時間
    private Vector3 knockback_initial_velocity = Vector3.zero;    // ノックバック開始時の速度
    private Vector3 knockback_velocity = Vector3.zero;            // 現在のノックバック速度
    private bool is_knockback = false;                            // ノックバック中かどうか

    // プロパティ：外部からアクセス可能な読み取り専用情報
    public int CurrentHealth => current_health;
    public int MaxHealth => max_health;
    public bool IsInvincible => invincible || invincibility_timer > 0.0f || is_grabbed;
    public bool IsKnockback => is_knockback;

    // 体力システムの初期化（メインのAwakeから呼ぶ）
    private void InitializeHealth()
    {
        current_health = max_health;
        invincibility_timer = 0.0f;

        knockback_timer = 0.0f;
        knockback_initial_velocity = Vector3.zero;
        knockback_velocity = Vector3.zero;
        is_knockback = false;
    }

    // 体力システムの更新（メインのUpdateから呼ぶ）
    private void UpdateHealth(float deltaTime)
    {
        // 無敵時間の更新
        if (invincibility_timer > 0.0f)
        {
            invincibility_timer -= deltaTime;
            if (invincibility_timer < 0.0f)
            {
                invincibility_timer = 0.0f;
            }
        }

        // ノックバック時間の更新
        if (is_knockback)
        {
            knockback_timer -= deltaTime;

            if (knockback_timer <= 0.0f)
            {
                EndKnockback();
            }
            else if (decay_knockback_over_time && knockback_duration > 0.0f)
            {
                float normalized = Mathf.Clamp01(knockback_timer / knockback_duration);
                knockback_velocity = knockback_initial_velocity * normalized;
            }
        }
    }

    // ========================================
    // IDamageableインターフェースの実装
    // ========================================

    // ダメージを受ける処理
    public void TakeDamage(int damage, Vector3 hit_direction, float knockback_force)
    {
        if (IsInvincible)
        {
            LogHealth("Damage ignored (invincible)");
            return;
        }

        // ダメージ適用
        current_health -= damage;
        if (current_health < 0)
        {
            current_health = 0;
        }

        LogHealth($"Took {damage} damage. Health: {current_health}/{max_health}");

        // ノックバック適用
        if (knockback_force > 0.0f && rb != null)
        {
            float actual_knockback = knockback_force * knockback_resistance;
            StartKnockback(hit_direction, actual_knockback);
            LogHealth($"Knockback started: dir={hit_direction}, force={actual_knockback}");
        }

        // 無敵時間開始
        invincibility_timer = invincibility_duration;

        // ダメージ時の演出などを呼ぶ
        OnDamaged(damage, hit_direction, knockback_force);

        // 死亡チェック
        if (current_health <= 0)
        {
            OnDeath();
        }
    }

    // ========================================
    // 敵の体当たり用（SendMessageで呼ばれる）
    // ========================================

    // 即死処理（敵の体当たりなど）
    public void Kill()
    {
        if (IsInvincible)
        {
            LogHealth("Kill ignored (invincible)");
            return;
        }

        LogHealth("Killed by instant death!");
        current_health = 0;
        OnDeath();
    }

    // ========================================
    // ノックバック内部処理
    // ========================================

    // ノックバック開始
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

        is_knockback = true;
        knockback_timer = knockback_duration;
        knockback_initial_velocity = direction * knockback_force;
        knockback_velocity = knockback_initial_velocity;
    }

    // ノックバック終了
    private void EndKnockback()
    {
        is_knockback = false;
        knockback_timer = 0.0f;
        knockback_initial_velocity = Vector3.zero;
        knockback_velocity = Vector3.zero;

        if (rb != null)
        {
            Vector3 velocity = rb.linearVelocity;
            velocity.x = 0.0f;
            rb.linearVelocity = velocity;
        }

        LogHealth("Knockback ended");
    }

    // ノックバック速度を適用（FixedUpdateから呼ばれる）
    private void ApplyKnockbackVelocity()
    {
        if (rb == null || !is_knockback)
        {
            return;
        }

        // ノックバック速度を適用（Y軸の重力は保持）
        Vector3 velocity = rb.linearVelocity;
        velocity.x = knockback_velocity.x;

        // ノックバック方向にY成分がある場合は適用
        if (Mathf.Abs(knockback_velocity.y) > 0.01f)
        {
            velocity.y = knockback_velocity.y;
        }

        rb.linearVelocity = velocity;
    }

    // ========================================
    // 内部処理・イベント
    // ========================================

    // ダメージを受けた時の演出処理
    private void OnDamaged(int damage, Vector3 hit_direction, float knockback_force)
    {
        // TODO: ダメージエフェクト、サウンド、アニメーションなどを再生
    }

    // 死亡処理
    private void OnDeath()
    {
        LogHealth("Player died!");

        // 掴まれ状態を解除する。
        if (is_grabbed)
        {
            ForceReleaseGrab();
        }

        // ノックバック状態も解除する。
        if (is_knockback)
        {
            EndKnockback();
        }

        // TODO: 死亡処理
    }

    // ========================================
    // ユーティリティ
    // ========================================

    // 体力を回復する（アイテムなど）
    public void Heal(int amount)
    {
        int previous_health = current_health;
        current_health = Mathf.Min(current_health + amount, max_health);
        int actual_heal = current_health - previous_health;

        if (actual_heal > 0)
        {
            LogHealth($"Healed {actual_heal}. Health: {current_health}/{max_health}");
        }
    }

    // デバッグログ出力
    private void LogHealth(string message)
    {
        if (!show_health_debug_log)
        {
            return;
        }

        Debug.Log($"[PlayerHealth] {message}");
    }
}