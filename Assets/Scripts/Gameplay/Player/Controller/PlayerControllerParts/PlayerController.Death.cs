using UnityEngine;

// 責務:
// - 死亡開始要求の統一入口を持つ
// - 二重死亡開始を防ぎ、受理した死因を死亡進行へ引き渡す
// - 他 system への接着をまとめる
public sealed partial class PlayerController
{
    // 死亡開始時に記録する最小限の死因。
    // 将来、奈落死・ギミック死・特殊演出分岐を増やすときの識別キーとして使う。
    public enum DeathCause
    {
        Damage,
        Hazard
    }

    [Header("参照: CheckpointSystem")]
    [Tooltip("同一シーン内の復帰地点を解決するシステムです。未設定時は実行時に探索を試みます。")]
    [SerializeField] private CheckpointSystem checkpointSystem;

    [Header("参照: StageResetSystem")]
    [Tooltip("死亡復帰時にステージ上の敵やギミックを初期状態へ戻すシステムです。未設定時は実行時に探索を試みます。")]
    [SerializeField] private StageResetSystem stageResetSystem;

    [Header("参照: PlayerCameraController")]
    [Tooltip("復帰時に標準カメラ状態へ戻すためのカメラ制御コンポーネントです。未設定時は実行時に探索を試みます。")]
    [SerializeField] private PlayerCameraController playerCameraController;

    [Header("参照: PlayerDeathView")]
    [Tooltip("死亡時の倒れ演出と黒フェード制御を行う見た目コンポーネントです。未設定時は実行時に探索を試みます。")]
    [SerializeField] private PlayerDeathView playerDeathView;

    private PlayerDeathCoordinator deathCoordinator;

    // 外部コンポーネント向けの環境死入口。
    internal bool RequestHazardDeath()
    {
        return RequestDeathStart(DeathCause.Hazard);
    }

    // 外部コンポーネント向けのダメージ死入口。
    internal bool RequestDamageDeath()
    {
        return RequestDeathStart(DeathCause.Damage);
    }

    // 死亡開始要求の統一入口。
    // 受理判定、必要最小限の状態遷移要求、通知、coordinator 起動のみを担当する。
    private bool RequestDeathStart(DeathCause cause)
    {
        if (healthReactionSystem != null && healthReactionSystem.IsDeathSequencePlaying)
        {
            LogHealth("Death request ignored: already processing");
            return false;
        }

        healthReactionSystem?.BeginDeathSequence();
        LogHealth($"Death requested: {cause}");

        if (reactionState != PlayerReactionState.Dead)
        {
            ChangeReactionState(PlayerReactionState.Dead);
            LogHealth("Death state entered");
        }

        PlayDeathVibration(cause);
        PlayDeathSound(cause);
        deathCoordinator?.StartRespawnSequence(cause);
        return true;
    }

    private void LogRespawn(string message)
    {
        Debug.Log($"[PlayerRespawn] {message}", this);
    }

    private void LogRespawnWarning(string message)
    {
        Debug.LogWarning($"[PlayerRespawn] {message}", this);
    }
}