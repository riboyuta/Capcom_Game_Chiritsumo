using UnityEngine;

// - プレイヤーの HP 管理 API を公開する
// - 実際の継続状態と更新処理は PlayerHealthReactionSystem へ委譲する
public sealed partial class PlayerController
{
    // =====================================================================
    // Inspector 設定値
    // =====================================================================

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
    // 公開プロパティ
    // =====================================================================

    // 現在 HP の参照口。
    public int CurrentHealth => healthReactionSystem != null ? healthReactionSystem.CurrentHealth : ConfiguredMaxHealth;

    // 最大 HP の参照口。
    public int MaxHealth => healthReactionSystem != null ? healthReactionSystem.MaxHealth : ConfiguredMaxHealth;
    // 無敵判定の統合口。
    public bool IsInvincible => healthReactionSystem != null
        ? healthReactionSystem.IsInvincible
        : (healthSettings != null && healthSettings.invincible) || IsGrabbed;

    // ノックバック中かどうかの参照口。
    public bool IsKnockback => healthReactionSystem != null && healthReactionSystem.IsKnockback;

    // Health/Reaction 起因の ActionLocked 判定。
    public bool IsHealthReactionActionLocked => healthReactionSystem != null && healthReactionSystem.IsActionLocked;

    // 死亡中かどうかの参照口。
    public bool IsHealthReactionDead => healthReactionSystem != null && healthReactionSystem.IsDead;

    // 死亡シーケンス中かどうかの参照口。
    public bool IsDeathSequencePlaying => healthReactionSystem != null && healthReactionSystem.IsDeathSequencePlaying;

    private int ConfiguredMaxHealth => healthSettings != null ? Mathf.Max(1, healthSettings.maxHealth) : 1;

    // =====================================================================
    // 初期化
    // =====================================================================

    // 体力系状態を初期化する。
    private void InitializeHealth()
    {
        healthReactionSystem?.Initialize();
    }

    // =====================================================================
    // 毎フレーム更新
    // =====================================================================

    // 体力系の時間経過状態を更新する。
    // メインの Update から呼ばれる前提で、無敵時間とノックバックの残り時間を進める。
    private void UpdateHealth(float deltaTime)
    {
        // 無敵時間は 0 未満にしない。
        if (healthReactionSystem == null)
        {
            return;
        }

        healthReactionSystem.Tick(deltaTime);
        healthReactionSystem.ConsumeDebugDeathRequest();
    }
    // =====================================================================
    // IDamageable 実装
    // =====================================================================

    // 外部からダメージを受ける入口。
    public void TakeDamage(int damage, Vector3 hit_direction, float knockback_force)
    {
        healthReactionSystem?.TakeDamage(damage, hit_direction, knockback_force);
    }


    // 即死処理。
    public void Kill()
    {
        healthReactionSystem?.RequestKill(Vector3.zero);
    }
    // 外部からの意味付き即死要求。
    internal void RequestKill(Vector3 damageDirection)
    {
        healthReactionSystem?.RequestKill(damageDirection);
    }

    // ノックバック状態を終了する。
    // 内部速度状態をリセットし、Rigidbody の X 速度だけを止めて横方向の吹き飛びを終了させる。
    internal void RequestKnockback(Vector3 force)
    {
        healthReactionSystem?.RequestKnockback(force);
    }

    private void ApplyKnockbackVelocity()
    {
        healthReactionSystem?.ApplyKnockbackVelocity();
    }
    

    // HP を回復する。
    public void Heal(int amount)
    {
        healthReactionSystem?.Heal(amount);
    }


    // 体力関連のログ出力。
    private void LogHealth(string message)
    {
        if (!showHealthDebugLog)
        {
            return;
        }

        Debug.Log($"[PlayerHealth] {message}");
    }
}