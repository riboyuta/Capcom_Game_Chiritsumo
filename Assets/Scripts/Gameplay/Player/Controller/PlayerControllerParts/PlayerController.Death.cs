using System.Collections;
using UnityEngine;

public sealed partial class PlayerController
{
    // =====================================================================
    // 死亡遷移定義
    // =====================================================================

    // 死亡開始時に記録する最小限の死因。
    // 将来、奈落死・ギミック死・特殊演出分岐を増やすときの識別キーとして使う。
    public enum DeathCause
    {
        Damage,
        Hazard
    }

    // =====================================================================
    // 実行時状態
    // =====================================================================
    [Header("Death / Respawn")]
    [Tooltip("同一シーン内の復帰地点を解決するシステム。未設定時は実行時に探索を試みます。")]
    [SerializeField] private CheckpointSystem checkpointSystem;

    // 死亡シーケンス開始済みかどうか。
    // true の間は追加の死亡開始要求を無視し、二重発火を防ぐ。
    private bool isDeathSequencePlaying = false;

    // 最後に受理した死因。
    // デバッグ確認と、将来の死因別演出分岐の参照元として保持する。
    private DeathCause lastDeathCause = DeathCause.Damage;
    private Coroutine respawnSequenceCoroutine;

    // =====================================================================
    // 死亡開始入口
    // =====================================================================

    // 死亡シーケンス開始要求の統一入口。
    // 初回の要求だけを受理し、死因を保存したうえで Dead 状態への遷移を開始する。
    // すでに死亡開始済みなら何もせず戻り、二重死亡開始を防ぐ。
    // 外部コンポーネント向けの奈落・環境即死入口。
    // 内部の死亡統一入口へ死因 Hazard で委譲する。
    public bool RequestHazardDeath()
    {
        return RequestDeathStart(DeathCause.Hazard);
    }

    private bool RequestDeathStart(DeathCause cause)
    {
        if (isDeathSequencePlaying)
        {
            LogHealth("Death request ignored: already processing");
            return false;
        }

        isDeathSequencePlaying = true;
        lastDeathCause = cause;

        LogHealth($"Death requested: {cause}");

        // まだ Dead に入っていないときだけ状態遷移を要求する。
        // ここで二重遷移を防ぎ、死亡開始入口を 1 箇所に寄せる。
        if (reactionState != PlayerReactionState.Dead)
        {
            ChangeReactionState(PlayerReactionState.Dead);
            LogHealth("Death state entered");
        }

        StartRespawnSequence();
        return true;
    }

    private void StartRespawnSequence()
    {
        if (respawnSequenceCoroutine != null)
        {
            StopCoroutine(respawnSequenceCoroutine);
        }

        respawnSequenceCoroutine = StartCoroutine(CoRespawnSequence());
    }

    private IEnumerator CoRespawnSequence()
    {
        LogRespawn("Respawn sequence started");
        yield return new WaitForSeconds(Mathf.Max(0.0f, healthSettings != null ? healthSettings.respawnDelay : 0.0f));

        if (checkpointSystem == null)
        {
            checkpointSystem = FindFirstObjectByType<CheckpointSystem>();
        }

        if (checkpointSystem == null)
        {
            LogRespawnWarning("Respawn checkpoint missing");
            isDeathSequencePlaying = false;
            respawnSequenceCoroutine = null;
            yield break;
        }

        Transform checkpoint = checkpointSystem.GetCurrentCheckpoint();
        if (checkpoint == null)
        {
            LogRespawnWarning("Respawn checkpoint missing");
            isDeathSequencePlaying = false;
            respawnSequenceCoroutine = null;
            yield break;
        }

        LogRespawn($"Respawn checkpoint resolved: {checkpoint.name}");
        RespawnAt(checkpoint.position);
        LogRespawn("Player respawn complete");
        respawnSequenceCoroutine = null;
    }

    private void RespawnAt(Vector3 worldPosition)
    {
        ResetForRespawn();

        if (rb != null)
        {
            rb.position = worldPosition;
            rb.linearVelocity = Vector3.zero;
        }
        else
        {
            transform.position = worldPosition;
        }
    }

    private void ResetForRespawn()
    {
        currentHealth = MaxHealth;
        invincibilityTimer = 0.0f;

        isKnockback = false;
        knockbackTimer = 0.0f;
        knockbackInitialVelocity = Vector3.zero;
        knockbackVelocity = Vector3.zero;

        reactionState = PlayerReactionState.Normal;
        reactionStateTimer = 0.0f;
        currentGrabAnchor = null;

        isGrounded = false;
        isTouchingWall = false;
        wallSide = 0;
        wallJumpControlLockTimer = 0.0f;
        wallReattachLockTimer = 0.0f;
        isWallSliding = false;
        isStepping = false;
        isFastFalling = false;
        stepTimer = 0.0f;
        stepCooldownTimer = 0.0f;
        stepStartVerticalVelocity = 0.0f;
        jumpRequested = false;
        stepRequested = false;
        stepBufferTimer = 0.0f;
        coyoteTimer = 0.0f;
        jumpBufferTimer = 0.0f;
        jumpHoldTimer = 0.0f;
        justLandedThisFrame = false;
        justJumpedThisFrame = false;
        justWallJumpedThisFrame = false;
        justCrossedApexThisFrame = false;

        // 将来の演出追加ポイント: 見た目回転リセット処理をここへ差し込む。

        isDeathSequencePlaying = false;
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