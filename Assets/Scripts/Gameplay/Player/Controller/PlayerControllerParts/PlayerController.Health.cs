using UnityEngine;

// 責務:
// - プレイヤーの HP 管理を行う
// - ダメージ適用、無敵時間、ノックバック、死亡処理の入口を管理する
// - IDamageable として外部からの被ダメージ要求を受ける
//
// 非責務:
// - Rigidbody 本体の生成や保持は担当しない
// - 掴み状態やリアクション状態そのものの定義は担当しない
// - ダメージ演出の具体実装（SE / VFX / アニメ再生）は担当しない
//
// 依存先:
// - rb: ノックバック速度の適用先
// - IsGrabbed / ForceReleaseGrab(): 掴まれ状態の参照と解除
// - reactionState / ChangeReactionState(): 被ダメージ / 死亡リアクションの遷移先
// - InitializeReactionState() / UpdateReactionState(): リアクション状態の初期化と更新
//
// 前提条件:
// - この partial は PlayerController の一部として使われる
// - メイン側から InitializeHealth(), UpdateHealth(deltaTime), ApplyKnockbackVelocity() が適切なタイミングで呼ばれる
// - 疑似3D横スク前提で、ノックバック計算では Z 軸移動を扱わない
public sealed partial class PlayerController : IDamageable
{
    // =====================================================================
    // Inspector 設定値
    // =====================================================================

    [Header("体力: 無敵モード")]
    [Tooltip("常時ダメージを無効化するデバッグ用フラグです。TakeDamage と Kill の受理判定に使います。有効にすると被ダメージ確認はしにくくなりますが、ステージ検証や挙動確認を安全に行えます。")]
    [SerializeField] private bool invincible = false;

    [Header("ノックバック: 耐性倍率")]
    [Tooltip("受けたノックバック力に掛ける倍率です。TakeDamage で knockback_force に乗算して実際の吹き飛び量を決めます。大きいほど強く吹き飛び、小さいほどノックバックを軽減します。1.0 が基準値です。")]
    [SerializeField] private float knockbackResistance = 1.0f;

    [Header("ノックバック: 継続時間(秒)")]
    [Tooltip("ノックバック状態を維持する時間です。StartKnockback で knockbackTimer に設定され、UpdateHealth で減算されます。長くすると吹き飛び拘束が続き、短くすると早く操作復帰しやすくなります。")]
    [SerializeField] private float knockbackDuration = 0.25f;

    [Header("ノックバック: 時間経過で減衰")]
    [Tooltip("ノックバック速度を時間経過で弱めるかどうかです。UpdateHealth 中の knockbackVelocity 更新に使います。有効にすると滑らかに減速し、無効にすると継続時間中は一定速度に近い挙動になります。")]
    [SerializeField] private bool decayKnockbackOverTime = true;

    [Header("デバッグ(Runtime): 体力ログ表示")]
    [Tooltip("体力・ダメージ関連のデバッグログを出すかどうかです。TakeDamage、Heal、死亡、ノックバック開始終了の観測に使います。調整用ではなく確認用で、有効にすると Console の情報量が増えます。")]
    [SerializeField] private bool showHealthDebugLog = false;

    // =====================================================================
    // 実行時状態
    // =====================================================================

    // 現在の HP。0 以下にならないよう TakeDamage と Heal で管理する。
    private int currentHealth;

    // ダメージ後の無敵時間の残り秒数。
    // 0 より大きい間は IsInvincible が true になり、追加ダメージを無効化する。
    private float invincibilityTimer = 0.0f;

    // ノックバック残り時間。
    // isKnockback 中に UpdateHealth で減算し、0 以下で EndKnockback を呼ぶ。
    private float knockbackTimer = 0.0f;

    // ノックバック開始時の初速。
    // 時間減衰を行う場合の基準速度として使う。
    private Vector3 knockbackInitialVelocity = Vector3.zero;

    // 現在フレームで適用するノックバック速度。
    // FixedUpdate 側から ApplyKnockbackVelocity で Rigidbody に反映する前提。
    private Vector3 knockbackVelocity = Vector3.zero;

    // ノックバック状態中かどうか。
    // 移動制御側で行動制限判定に使うことを想定した公開状態でもある。
    private bool isKnockback = false;

    // =====================================================================
    // 公開プロパティ
    // =====================================================================

    // 現在 HP の参照口。
    public int CurrentHealth => currentHealth;

    // 最大 HP の参照口。
    public int MaxHealth => ConfiguredMaxHealth;

    // 無敵判定の統合口。
    // デバッグ無敵、被弾後無敵、掴まれ中をまとめて「ダメージ無効」として扱う。
    public bool IsInvincible => invincible || invincibilityTimer > 0.0f || IsGrabbed;

    // ノックバック中かどうかの参照口。
    public bool IsKnockback => isKnockback;

    private int ConfiguredMaxHealth => healthSettings != null ? Mathf.Max(1, healthSettings.maxHealth) : 1;

    private float ConfiguredInvincibilityDuration => healthSettings != null ? Mathf.Max(0.0f, healthSettings.invincibilityDuration) : 0.0f;


    // =====================================================================
    // 初期化
    // =====================================================================

    // 体力系状態を初期化する。
    // メインの Awake / 初期化シーケンスから呼ばれる前提で、HP・無敵時間・ノックバック状態を既定値へ戻す。
    private void InitializeHealth()
    {
        currentHealth = ConfiguredMaxHealth;
        invincibilityTimer = 0.0f;

        knockbackTimer = 0.0f;
        knockbackInitialVelocity = Vector3.zero;
        knockbackVelocity = Vector3.zero;
        isKnockback = false;
        isDeathSequencePlaying = false;
        lastDeathCause = DeathCause.Damage;
        InitializeReactionState();
    }

    // =====================================================================
    // 毎フレーム更新
    // =====================================================================

    // 体力系の時間経過状態を更新する。
    // メインの Update から呼ばれる前提で、無敵時間とノックバックの残り時間を進める。
    private void UpdateHealth(float deltaTime)
    {
        // 無敵時間は 0 未満にしない。
        if (invincibilityTimer > 0.0f)
        {
            invincibilityTimer -= deltaTime;
            if (invincibilityTimer < 0.0f)
            {
                invincibilityTimer = 0.0f;
            }
        }

        // ノックバック中だけ残り時間と速度を更新する。
        if (isKnockback)
        {
            knockbackTimer -= deltaTime;

            if (knockbackTimer <= 0.0f)
            {
                EndKnockback();
            }
            else if (decayKnockbackOverTime && knockbackDuration > 0.0f)
            {
                // 減衰有効時は、開始速度から残り時間比率に応じて線形に弱める。
                float normalized = Mathf.Clamp01(knockbackTimer / knockbackDuration);
                knockbackVelocity = knockbackInitialVelocity * normalized;
            }
        }
        ConsumeDebugDeathRequest();
        UpdateReactionState(deltaTime);
    }
    private void ConsumeDebugDeathRequest()
    {
        if (healthSettings == null)
        {
            return;
        }

        if (healthSettings.debugRequestDeath)
        {
            healthSettings.debugRequestDeath = false;
            RequestDeathStart(DeathCause.Damage);
            return;
        }

        if (healthSettings.debugRequestHazardDeath)
        {
            healthSettings.debugRequestHazardDeath = false;
            RequestDeathStart(DeathCause.Hazard);
        }
    }
    // =====================================================================
    // IDamageable 実装
    // =====================================================================

    // 外部からダメージを受ける入口。
    // HP 減算、ノックバック開始、無敵時間開始、リアクション更新、死亡判定までをまとめて行う。
    public void TakeDamage(int damage, Vector3 hit_direction, float knockback_force)
    {
        if (IsInvincible)
        {
            LogHealth("Damage ignored (invincible)");
            return;
        }

        // HP は 0 未満にしない。
        currentHealth -= damage;
        if (currentHealth < 0)
        {
            currentHealth = 0;
        }

        LogHealth($"Took {damage} damage. Health: {currentHealth}/{ConfiguredMaxHealth}");

        // ノックバックは force が正で、かつ Rigidbody がある場合だけ開始する。
        if (knockback_force > 0.0f && rb != null)
        {
            float actual_knockback = knockback_force * knockbackResistance;
            StartKnockback(hit_direction, actual_knockback);
            LogHealth($"Knockback started: dir={hit_direction}, force={actual_knockback}");
        }

        // 被弾後の連続ヒットを防ぐため、最後に無敵時間を開始する。
        invincibilityTimer = ConfiguredInvincibilityDuration;

        // 演出やリアクション遷移の入口。
        OnDamaged(damage, hit_direction, knockback_force);

        // HP が尽きたら死亡処理へ進む。
        if (currentHealth <= 0)
        {
            OnDeath();
        }
    }

    // =====================================================================
    // 敵の体当たり用入口
    // =====================================================================

    // 即死処理。
    // SendMessage などで外部から呼ばれる想定で、通常ダメージ計算を経由せず死亡状態へ移す。
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

    // =====================================================================
    // ノックバック内部処理
    // =====================================================================

    // ノックバック状態を開始する。
    // hit_direction から速度ベクトルを作るが、疑似3D横スク前提のため Z 軸成分は使わない。
    private void StartKnockback(Vector3 hit_direction, float knockback_force)
    {
        Vector3 direction = hit_direction.normalized;

        // 疑似3D横スク前提なので、Z 移動は発生させない。
        direction.z = 0.0f;
        direction = direction.normalized;

        isKnockback = true;
        knockbackTimer = knockbackDuration;
        knockbackInitialVelocity = direction * knockback_force;
        knockbackVelocity = knockbackInitialVelocity;
    }

    // ノックバック状態を終了する。
    // 内部速度状態をリセットし、Rigidbody の X 速度だけを止めて横方向の吹き飛びを終了させる。
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

    // 現在のノックバック速度を Rigidbody に反映する。
    // FixedUpdate から呼ばれる前提で、X は常に上書きし、Y は明確なノックバック成分があるときだけ上書きする。
    private void ApplyKnockbackVelocity()
    {
        if (rb == null || !isKnockback)
        {
            return;
        }

        // Y 軸の重力挙動はなるべく保持しつつ、ノックバックの横速度を適用する。
        Vector3 velocity = rb.linearVelocity;
        velocity.x = knockbackVelocity.x;

        // ノックバック方向に十分な Y 成分がある場合だけ縦方向も上書きする。
        if (Mathf.Abs(knockbackVelocity.y) > 0.01f)
        {
            velocity.y = knockbackVelocity.y;
        }

        rb.linearVelocity = velocity;
    }

    // =====================================================================
    // 内部イベント
    // =====================================================================

    // 被ダメージ時の内部イベント。
    // ここではリアクション状態の変更を担当し、演出再生は今後の拡張ポイントとして残す。
    private void OnDamaged(int damage, Vector3 hit_direction, float knockback_force)
    {
        if (reactionState == PlayerReactionState.Normal)
        {
            ChangeReactionState(PlayerReactionState.Damaged);
        }

        // TODO: ダメージエフェクト、サウンド、アニメーションなどを再生
    }

    // 死亡時の内部イベント。
    // リアクション状態を Dead へ遷移し、掴み中やノックバック中なら競合状態を解除する。
    private void OnDeath()
    {
        LogHealth("Player died!");


        // 掴まれ状態のまま死亡すると他状態と競合しやすいため、先に解除する。
        if (IsGrabbed)
        {
            ForceReleaseGrab();
        }

        // ノックバック状態も終了して、死亡後に速度状態が残らないようにする。
        if (isKnockback)
        {
            EndKnockback();
        }
        RequestDeathStart(DeathCause.Damage);
    }
    
    // =====================================================================
    // 公開ユーティリティ
    // =====================================================================

    // HP を回復する。
    // 最大 HP を超えない範囲で currentHealth を増やし、実際に回復した量だけをログに出す。
    public void Heal(int amount)
    {
        int previousHealth = currentHealth;
        currentHealth = Mathf.Min(currentHealth + amount, ConfiguredMaxHealth);
        int actualHeal = currentHealth - previousHealth;

        if (actualHeal > 0)
        {
            LogHealth($"Healed {actualHeal}. Health: {currentHealth}/{ConfiguredMaxHealth}");
        }
    }

    // =====================================================================
    // デバッグ補助
    // =====================================================================

    // 体力関連のログ出力。
    // showHealthDebugLog が有効なときだけ Console に出す。
    private void LogHealth(string message)
    {
        if (!showHealthDebugLog)
        {
            return;
        }

        Debug.Log($"[PlayerHealth] {message}");
    }
}