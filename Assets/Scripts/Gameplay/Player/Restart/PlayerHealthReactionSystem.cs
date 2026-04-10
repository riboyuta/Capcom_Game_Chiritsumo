using System;
using UnityEngine;

// プレイヤーの Health / Reaction 系ルールを担当する内部システム。
// 被弾、無敵、ノックバック、死亡遷移、復帰待機情報を保持する。
internal sealed class PlayerHealthReactionSystem
{
    // 実行時の移動状態参照先。
    private readonly PlayerRuntimeState runtimeState;
    // 体力設定参照先。
    private readonly PlayerHealthSettings healthSettings;
    // ノックバック速度の適用先。
    private readonly Rigidbody rb;
    // 拘束追従などで使う Transform。
    private readonly Transform playerTransform;
    // 掴み中判定の参照口。
    private readonly Func<bool> isGrabbedProvider;
    // Health/Death 以外の ActionLocked 判定の参照口。
    private readonly Func<bool> additionalActionLockedProvider;
    // リアクション状態を参照する口。
    private readonly Func<PlayerController.PlayerReactionState> reactionStateProvider;
    // リアクション初期化の委譲口。
    private readonly Action initializeReactionState;
    // リアクション更新の委譲口。
    private readonly Action<float> updateReactionState;
    // 掴み解除の委譲口
    private readonly Action forceReleaseGrab;
    // リアクション状態遷移の委譲口。
    private readonly Action<PlayerController.PlayerReactionState> changeReactionState;
    // 死亡開始要求の委譲口。
    private readonly Func<PlayerController.DeathCause, bool> requestDeathStart;
    // 体力ログ出力の委譲口。
    private readonly Action<string> logHealth;
    // ノックバック耐性倍率の参照口。
    private readonly Func<float> knockbackResistanceProvider;
    // ノックバック継続時間の参照口。
    private readonly Func<float> knockbackDurationProvider;
    // ノックバック減衰有効フラグの参照口。
    private readonly Func<bool> decayKnockbackOverTimeProvider;

    // 現在 HP。
    private int currentHealth;
    // 無敵残り時間。
    private float invincibilityTimer;
    // ノックバック中かどうか。
    private bool isKnockback;
    // ノックバック残り時間。
    private float knockbackTimer;
    // ノックバック開始時の速度。
    private Vector3 knockbackInitialVelocity;
    // 現在適用すべきノックバック速度。
    private Vector3 knockbackVelocity;
    // 死亡中かどうか。
    private bool isDead;
    // 死亡復帰待機の残り時間。
    private float deathRespawnTimer;
    // 復帰可能になったかどうか。
    private bool isRespawnReady;
    // 死亡シーケンス中かどうか。
    private bool isDeathSequencePlaying;
    // reaction 処理中かどうかのフラグ。
    private bool isReactionProcessing;

    // 現在 HP の公開参照口。
    internal int CurrentHealth => currentHealth;
    // 最大 HP の公開参照口。
    internal int MaxHealth => Mathf.Max(1, healthSettings != null ? healthSettings.maxHealth : 1);
    // 無敵判定の公開参照口。
    internal bool IsInvincible => (healthSettings != null && healthSettings.invincible) || invincibilityTimer > 0f || isGrabbedProvider();
    // ノックバック判定の公開参照口。
    internal bool IsKnockback => isKnockback;
    // 死亡判定の公開参照口。
    internal bool IsDead => isDead;
    // 死亡シーケンス進行中判定の公開参照口。
    internal bool IsDeathSequencePlaying => isDeathSequencePlaying;
    // 復帰可能判定の公開参照口。
    internal bool IsRespawnReady => isRespawnReady;
    // ActionLocked 判定の公開参照口。
    internal bool IsActionLocked => isDead || additionalActionLockedProvider();
    // リアクション処理中判定の公開参照口。
    internal bool IsReactionProcessing => isReactionProcessing;

    // Health / Reaction システムを構築する。
    internal PlayerHealthReactionSystem(
        PlayerRuntimeState runtimeState,
        PlayerHealthSettings healthSettings,
        Rigidbody rb,
        Transform playerTransform,
        Func<bool> isGrabbedProvider,
        Func<bool> additionalActionLockedProvider,
        Func<PlayerController.PlayerReactionState> reactionStateProvider,
        Action initializeReactionState,
        Action<float> updateReactionState,
        Action forceReleaseGrab,
        Action<PlayerController.PlayerReactionState> changeReactionState,
        Func<PlayerController.DeathCause, bool> requestDeathStart,
        Action<string> logHealth,
        Func<float> knockbackResistanceProvider,
        Func<float> knockbackDurationProvider,
        Func<bool> decayKnockbackOverTimeProvider)
    {
        this.runtimeState = runtimeState;
        this.healthSettings = healthSettings;
        this.rb = rb;
        this.playerTransform = playerTransform;
        this.isGrabbedProvider = isGrabbedProvider;
        this.additionalActionLockedProvider = additionalActionLockedProvider;
        this.reactionStateProvider = reactionStateProvider;
        this.initializeReactionState = initializeReactionState;
        this.updateReactionState = updateReactionState;
        this.forceReleaseGrab = forceReleaseGrab;
        this.changeReactionState = changeReactionState;
        this.requestDeathStart = requestDeathStart;
        this.logHealth = logHealth;
        this.knockbackResistanceProvider = knockbackResistanceProvider;
        this.knockbackDurationProvider = knockbackDurationProvider;
        this.decayKnockbackOverTimeProvider = decayKnockbackOverTimeProvider;
    }

    // 初期化処理。
    internal void Initialize()
    {
        currentHealth = MaxHealth;
        invincibilityTimer = 0f;
        isKnockback = false;
        knockbackTimer = 0f;
        knockbackInitialVelocity = Vector3.zero;
        knockbackVelocity = Vector3.zero;
        isDead = false;
        deathRespawnTimer = 0f;
        isRespawnReady = false;
        isDeathSequencePlaying = false;
        isReactionProcessing = false;
        initializeReactionState?.Invoke();
    }

    // 毎フレーム更新処理。
    internal void Tick(float deltaTime)
    {
        if (invincibilityTimer > 0f)
        {
            invincibilityTimer = Mathf.Max(0f, invincibilityTimer - deltaTime);
        }

        if (isKnockback)
        {
            knockbackTimer -= deltaTime;
            if (knockbackTimer <= 0f)
            {
                EndKnockback();
            }
            else if (decayKnockbackOverTimeProvider() && knockbackDurationProvider() > 0f)
            {
                float normalized = Mathf.Clamp01(knockbackTimer / knockbackDurationProvider());
                knockbackVelocity = knockbackInitialVelocity * normalized;
            }
        }

        if (isDeathSequencePlaying && deathRespawnTimer > 0f)
        {
            deathRespawnTimer = Mathf.Max(0f, deathRespawnTimer - deltaTime);
            if (deathRespawnTimer <= 0f)
            {
                isRespawnReady = true;
            }
        }

        isReactionProcessing = reactionStateProvider() != PlayerController.PlayerReactionState.Normal;
        updateReactionState?.Invoke(deltaTime);
    }

    // 物理フレーム更新処理。
    internal void TickFixed(float fixedDeltaTime)
    {
        // 現段階では fixed 側の時間進行更新は不要。
    }

    // ダメージ受付処理。
    internal void TakeDamage(int damage, Vector3 hitDirection, float knockbackForce)
    {
        if (IsInvincible)
        {
            logHealth?.Invoke("Damage ignored (invincible)");
            return;
        }

        currentHealth = Mathf.Max(0, currentHealth - damage);
        logHealth?.Invoke($"Took {damage} damage. Health: {currentHealth}/{MaxHealth}");

        if (knockbackForce > 0f && rb != null)
        {
            float actualKnockback = knockbackForce * Mathf.Max(0f, knockbackResistanceProvider());
            StartKnockback(hitDirection, actualKnockback);
            logHealth?.Invoke($"Knockback started: dir={hitDirection}, force={actualKnockback}");
        }

        invincibilityTimer = Mathf.Max(0f, healthSettings != null ? healthSettings.invincibilityDuration : 0f);

        if (reactionStateProvider() == PlayerController.PlayerReactionState.Normal)
        {
            changeReactionState?.Invoke(PlayerController.PlayerReactionState.Damaged);
        }

        if (currentHealth <= 0)
        {
            HandleDeathByDamage();
        }
    }

    // 即死要求を受け付ける。
    internal void RequestKill(Vector3 damageDirection)
    {
        // damageDirection は将来の演出分岐用に受け取る。
        if (IsInvincible)
        {
            logHealth?.Invoke("Kill ignored (invincible)");
            return;
        }

        currentHealth = 0;
        HandleDeathByDamage();
    }

    // ノックバック要求を受け付ける。
    internal void RequestKnockback(Vector3 force)
    {
        if (rb == null)
        {
            return;
        }

        float magnitude = force.magnitude;
        if (magnitude <= 0f)
        {
            return;
        }

        StartKnockback(force / magnitude, magnitude);
    }

    // ノックバック速度を Rigidbody に適用する。
    internal void ApplyKnockbackVelocity()
    {
        if (rb == null || !isKnockback)
        {
            return;
        }

        Vector3 velocity = rb.linearVelocity;
        velocity.x = knockbackVelocity.x;
        if (Mathf.Abs(knockbackVelocity.y) > 0.01f)
        {
            velocity.y = knockbackVelocity.y;
        }

        rb.linearVelocity = velocity;
    }

    // デバッグ死亡要求を消費する。
    internal void ConsumeDebugDeathRequest()
    {
        if (healthSettings == null)
        {
            return;
        }

        if (healthSettings.debugRequestDeath)
        {
            healthSettings.debugRequestDeath = false;
            RequestDeathSequence(PlayerController.DeathCause.Damage);
            return;
        }

        if (healthSettings.debugRequestHazardDeath)
        {
            healthSettings.debugRequestHazardDeath = false;
            RequestDeathSequence(PlayerController.DeathCause.Hazard);
        }
    }

    // 回復処理。
    internal void Heal(int amount)
    {
        int previous = currentHealth;
        currentHealth = Mathf.Min(currentHealth + amount, MaxHealth);
        int actualHeal = currentHealth - previous;
        if (actualHeal > 0)
        {
            logHealth?.Invoke($"Healed {actualHeal}. Health: {currentHealth}/{MaxHealth}");
        }
    }

    // 死亡中フラグを開始する。
    internal void BeginDeathSequence()
    {
        isDead = true;
        isDeathSequencePlaying = true;
        deathRespawnTimer = 0f;
        isRespawnReady = false;
    }

    // 復帰待機時間を設定する。
    internal void SetRespawnWait(float waitSeconds)
    {
        deathRespawnTimer = Mathf.Max(0f, waitSeconds);
        isRespawnReady = deathRespawnTimer <= 0f;
    }

    // 復帰待機を完了扱いにする。
    internal void MarkRespawnReady()
    {
        deathRespawnTimer = 0f;
        isRespawnReady = true;
    }

    // 復帰時に health/reaction 状態を初期値へ戻す。
    internal void ResetForRespawn()
    {
        currentHealth = MaxHealth;
        invincibilityTimer = 0f;
        isKnockback = false;
        knockbackTimer = 0f;
        knockbackInitialVelocity = Vector3.zero;
        knockbackVelocity = Vector3.zero;
        isDead = false;
        deathRespawnTimer = 0f;
        isRespawnReady = false;
        isDeathSequencePlaying = false;
        isReactionProcessing = false;
        initializeReactionState?.Invoke();
    }

    // ノックバックを開始する。
    private void StartKnockback(Vector3 hitDirection, float knockbackForce)
    {
        Vector3 direction = hitDirection.normalized;
        direction.z = 0f;
        direction = direction.normalized;
        isKnockback = true;
        knockbackTimer = Mathf.Max(0f, knockbackDurationProvider());
        knockbackInitialVelocity = direction * knockbackForce;
        knockbackVelocity = knockbackInitialVelocity;
    }

    // ノックバックを終了する。
    private void EndKnockback()
    {
        isKnockback = false;
        knockbackTimer = 0f;
        knockbackInitialVelocity = Vector3.zero;
        knockbackVelocity = Vector3.zero;

        if (rb != null)
        {
            Vector3 velocity = rb.linearVelocity;
            velocity.x = 0f;
            rb.linearVelocity = velocity;
        }

        logHealth?.Invoke("Knockback ended");
    }

    // ダメージ起因の死亡処理を行う。
    private void HandleDeathByDamage()
    {
        logHealth?.Invoke("Player died!");

        if (isGrabbedProvider())
        {
            forceReleaseGrab?.Invoke();
        }

        if (isKnockback)
        {
            EndKnockback();
        }

        RequestDeathSequence(PlayerController.DeathCause.Damage);
    }

    // 死亡開始要求を行う。
    private void RequestDeathSequence(PlayerController.DeathCause cause)
    {
        if (requestDeathStart == null)
        {
            return;
        }

        if (requestDeathStart(cause))
        {
            BeginDeathSequence();
        }
    }
}