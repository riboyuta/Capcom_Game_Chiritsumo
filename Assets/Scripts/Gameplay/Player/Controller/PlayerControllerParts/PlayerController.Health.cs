using UnityEngine;

// PlayerController の体力・ダメージ処理部分（partial）
// HP管理、ダメージ処理、無敵時間、死亡処理、ノックバック処理を担当
public sealed partial class PlayerController : IDamageable
{
    [Header("Health")]
    [SerializeField] private int maxHealth = 3;                  // 最大体力
    [SerializeField] private bool invincible = false;             // 無敵モード（デバッグ用）

    [Header("Invincibility")]
    [SerializeField] private float invincibilityDuration = 1.0f; // ダメージ後の無敵時間（秒）
    [Header("Knockback")]
    [SerializeField] private float knockbackResistance = 1.0f;   // ノックバック耐性（1.0=通常、0.5=半減）
    [SerializeField] private float knockbackDuration = 0.25f;    // ノックバック継続時間（秒）
    [SerializeField] private bool decayKnockbackOverTime = true; // 時間経過でノックバックを弱めるか
    [Header("Health Debug")]
    [SerializeField] private bool showHealthDebugLog = false;  // デバッグログ表示

    // 体力・ダメージ関連の状態
    private int currentHealth;                                   // 現在の体力
    private float invincibilityTimer = 0.0f;                     // 無敵時間の残り時間
    // ノックバック関連
    private float knockbackTimer = 0.0f;                         // ノックバック残り時間
    private Vector3 knockbackInitialVelocity = Vector3.zero;    // ノックバック開始時の速度
    private Vector3 knockbackVelocity = Vector3.zero;            // 現在のノックバック速度
    private bool isKnockback = false;                            // ノックバック中かどうか

    // プロパティ：外部からアクセス可能な読み取り専用情報
    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;
    public bool IsInvincible => invincible || invincibilityTimer > 0.0f || isGrabbed;
    public bool IsKnockback => isKnockback;

    // 体力システムの初期化（メインのAwakeから呼ぶ）
    private void InitializeHealth()
    {
        currentHealth = maxHealth;
        invincibilityTimer = 0.0f;

        knockbackTimer = 0.0f;
        knockbackInitialVelocity = Vector3.zero;
        knockbackVelocity = Vector3.zero;
        isKnockback = false;
    }

    // 体力システムの更新（メインのUpdateから呼ぶ）
    private void UpdateHealth(float deltaTime)
    {
        // 無敵時間の更新
        if (invincibilityTimer > 0.0f)
        {
            invincibilityTimer -= deltaTime;
            if (invincibilityTimer < 0.0f)
            {
                invincibilityTimer = 0.0f;
            }
        }

        // ノックバック時間の更新
        if (isKnockback)
        {
            knockbackTimer -= deltaTime;

            if (knockbackTimer <= 0.0f)
            {
                EndKnockback();
            }
            else if (decayKnockbackOverTime && knockbackDuration > 0.0f)
            {
                float normalized = Mathf.Clamp01(knockbackTimer / knockbackDuration);
                knockbackVelocity = knockbackInitialVelocity * normalized;
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
        currentHealth -= damage;
        if (currentHealth < 0)
        {
            currentHealth = 0;
        }

        LogHealth($"Took {damage} damage. Health: {currentHealth}/{maxHealth}");

        // ノックバック適用
        if (knockback_force > 0.0f && rb != null)
        {
            float actual_knockback = knockback_force * knockbackResistance;
            StartKnockback(hit_direction, actual_knockback);
            LogHealth($"Knockback started: dir={hit_direction}, force={actual_knockback}");
        }

        // 無敵時間開始
        invincibilityTimer = invincibilityDuration;

        // ダメージ時の演出などを呼ぶ
        OnDamaged(damage, hit_direction, knockback_force);

        // 死亡チェック
        if (currentHealth <= 0)
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
        currentHealth = 0;
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

        isKnockback = true;
        knockbackTimer = knockbackDuration;
        knockbackInitialVelocity = direction * knockback_force;
        knockbackVelocity = knockbackInitialVelocity;
    }

    // ノックバック終了
    private void EndKnockback()
    {
        isKnockback = false;
        knockbackTimer = 0.0f;
        knockbackInitialVelocity = Vector3.zero;
        knockbackVelocity = Vector3.zero;

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
        if (rb == null || !isKnockback)
        {
            return;
        }

        // ノックバック速度を適用（Y軸の重力は保持）
        Vector3 velocity = rb.linearVelocity;
        velocity.x = knockbackVelocity.x;

        // ノックバック方向にY成分がある場合は適用
        if (Mathf.Abs(knockbackVelocity.y) > 0.01f)
        {
            velocity.y = knockbackVelocity.y;
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
        if (isGrabbed)
        {
            ForceReleaseGrab();
        }

        // ノックバック状態も解除する。
        if (isKnockback)
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
        int previousHealth = currentHealth;
        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
        int actualHeal = currentHealth - previousHealth;

        if (actualHeal > 0)
        {
            LogHealth($"Healed {actualHeal}. Health: {currentHealth}/{maxHealth}");
        }
    }

    // デバッグログ出力
    private void LogHealth(string message)
    {
        if (!showHealthDebugLog)
        {
            return;
        }

        Debug.Log($"[PlayerHealth] {message}");
    }
}