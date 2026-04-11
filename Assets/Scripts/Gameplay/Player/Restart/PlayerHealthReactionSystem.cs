using System;
using UnityEngine;
public enum PlayerReactionState
{
    Normal,
    Damaged,
    Grabbed,
    Smashed,
    Dead
}
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
    // 死亡開始要求の委譲口。
    private readonly Func<PlayerController.DeathCause, bool> requestDeathStart;
    // 体力ログ出力の委譲口。
    private readonly Action<string> logHealth;
    // リアクションログ出力の委譲口。
    private readonly Action<string> logReaction;
    // ノックバック耐性倍率の参照口。
    private readonly Func<float> knockbackResistanceProvider;
    // ノックバック継続時間の参照口。
    private readonly Func<float> knockbackDurationProvider;
    // ノックバック減衰有効フラグの参照口。
    private readonly Func<bool> decayKnockbackOverTimeProvider;
    // Damaged 継続時間の参照口。
    private readonly Func<float> damagedStateDurationProvider;
    // Grabbed 継続時間の参照口。
    private readonly Func<float> grabbedStateDurationProvider;
    // Smashed 継続時間の参照口。
    private readonly Func<float> smashedStateDurationProvider;
    // Grabbed 後の即死設定参照口。
    private readonly Func<bool> killAfterGrabbedDurationProvider;
    // Smash 即死設定参照口。
    private readonly Func<bool> smashIsInstantDeathProvider;
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

    // reaction の現在状態。
    private PlayerReactionState reactionState;
    // 現在 reaction 状態に入ってからの経過時間。
    private float reactionStateTimer;
    // 掴まれ中に追従する先。
    private Transform currentGrabAnchor;

    // 現在 HP の公開参照口。
    internal int CurrentHealth => currentHealth;
    // 最大 HP の公開参照口。
    internal int MaxHealth => Mathf.Max(1, healthSettings != null ? healthSettings.maxHealth : 1);
    // 無敵判定の公開参照口。
    internal bool IsInvincible => (healthSettings != null && healthSettings.invincible) || invincibilityTimer > 0f || IsGrabbed;
    // ノックバック判定の公開参照口。
    internal bool IsKnockback => isKnockback;
    // 死亡判定の公開参照口。
    internal bool IsDead => isDead;
    // 死亡シーケンス進行中判定の公開参照口。
    internal bool IsDeathSequencePlaying => isDeathSequencePlaying;
    // 復帰可能判定の公開参照口。
    internal bool IsRespawnReady => isRespawnReady;
    // リアクション状態参照口。
    internal PlayerReactionState ReactionState => reactionState;
    // Grabbed 判定参照口。
    internal bool IsGrabbed => reactionState == PlayerReactionState.Grabbed;
    // Smashed 判定参照口。
    internal bool IsSmashed => reactionState == PlayerReactionState.Smashed;
    // Dead 判定参照口。
    internal bool IsDeadState => reactionState == PlayerReactionState.Dead;
    // ActionLocked 判定の公開参照口。
    internal bool IsActionLocked =>
        reactionState == PlayerReactionState.Grabbed ||
        reactionState == PlayerReactionState.Smashed ||
        reactionState == PlayerReactionState.Dead ||
        isDead;    // リアクション処理中判定の公開参照口。
    internal bool IsReactionProcessing => reactionState != PlayerReactionState.Normal;
    // Health / Reaction システムを構築する。
    internal PlayerHealthReactionSystem(
        PlayerRuntimeState runtimeState,
        PlayerHealthSettings healthSettings,
        Rigidbody rb,
        Transform playerTransform,
        Func<PlayerController.DeathCause, bool> requestDeathStart,
        Action<string> logHealth,
        Action<string> logReaction,
        Func<float> knockbackResistanceProvider,
        Func<float> knockbackDurationProvider,
        Func<bool> decayKnockbackOverTimeProvider,
        Func<float> damagedStateDurationProvider,
        Func<float> grabbedStateDurationProvider,
        Func<float> smashedStateDurationProvider,
        Func<bool> killAfterGrabbedDurationProvider,
        Func<bool> smashIsInstantDeathProvider)
    {
        this.runtimeState = runtimeState;
        this.healthSettings = healthSettings;
        this.rb = rb;
        this.playerTransform = playerTransform;
        this.requestDeathStart = requestDeathStart;
        this.logHealth = logHealth;
        this.logReaction = logReaction;
        this.knockbackResistanceProvider = knockbackResistanceProvider;
        this.knockbackDurationProvider = knockbackDurationProvider;
        this.decayKnockbackOverTimeProvider = decayKnockbackOverTimeProvider;
        this.damagedStateDurationProvider = damagedStateDurationProvider;
        this.grabbedStateDurationProvider = grabbedStateDurationProvider;
        this.smashedStateDurationProvider = smashedStateDurationProvider;
        this.killAfterGrabbedDurationProvider = killAfterGrabbedDurationProvider;
        this.smashIsInstantDeathProvider = smashIsInstantDeathProvider;
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
        InitializeReactionState();
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

        UpdateReactionState(deltaTime);
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

        if (reactionState == PlayerReactionState.Normal)
        {
            ChangeReactionState(PlayerReactionState.Damaged);
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
        ChangeReactionState(PlayerReactionState.Dead);
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
        InitializeReactionState();
    }

    // 掴み追従先を設定し、Grabbed 状態へ遷移する。
    internal void StartGrab(Transform grabAnchor)
    {
        currentGrabAnchor = grabAnchor;
        ChangeReactionState(PlayerReactionState.Grabbed);
    }

    // 掴みを強制解放する。
    internal void ForceReleaseGrab()
    {
        currentGrabAnchor = null;

        if (reactionState == PlayerReactionState.Grabbed)
        {
            ChangeReactionState(PlayerReactionState.Normal);
        }
    }

    // 外部からリアクション状態を変更する入口。
    private void ChangeReactionState(PlayerReactionState nextState)
    {
        reactionState = nextState;
        reactionStateTimer = 0f;

        if (reactionState != PlayerReactionState.Grabbed)
        {
            currentGrabAnchor = null;
        }

        LogReaction($"Reaction state changed: {reactionState}");
    }

    // リアクション状態を初期化する。
    private void InitializeReactionState()
    {
        reactionState = PlayerReactionState.Normal;
        reactionStateTimer = 0f;
        currentGrabAnchor = null;
    }

    // リアクション状態の更新処理。
    private void UpdateReactionState(float deltaTime)
    {
        reactionStateTimer += deltaTime;

        switch (reactionState)
        {
            case PlayerReactionState.Normal:
                break;

            case PlayerReactionState.Damaged:
                if (reactionStateTimer >= Mathf.Max(0f, damagedStateDurationProvider()))
                {
                    ChangeReactionState(PlayerReactionState.Normal);
                }
                break;

            case PlayerReactionState.Grabbed:
                if (currentGrabAnchor != null && playerTransform != null)
                {
                    Vector3 target = currentGrabAnchor.position;
                    target.z = playerTransform.position.z;
                    playerTransform.position = Vector3.Lerp(playerTransform.position, target, deltaTime * 15f);
                }

                if (reactionStateTimer >= Mathf.Max(0f, grabbedStateDurationProvider()))
                {
                    if (killAfterGrabbedDurationProvider())
                    {
                        TakeDamage(999, Vector3.zero, 0f);
                    }
                    else
                    {
                        ForceReleaseGrab();
                    }
                }
                break;

            case PlayerReactionState.Smashed:
                if (!smashIsInstantDeathProvider() && reactionStateTimer >= Mathf.Max(0f, smashedStateDurationProvider()))
                {
                    ChangeReactionState(PlayerReactionState.Normal);
                }
                break;

            case PlayerReactionState.Dead:
                break;
        }
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

        if (IsGrabbed)
        {
            ForceReleaseGrab();
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
        requestDeathStart?.Invoke(cause);
    }
    // リアクションログを出力する。
    private void LogReaction(string message)
    {
        logReaction?.Invoke(message);
    }

}

