using System.Collections;
using UnityEngine;

// 責務:
// - 死亡開始要求の統一入口を持つ
// - 二重死亡開始を防ぎ、最後に受理した死因を保持する
// - 黒トランジション中に復帰処理を進める
// - 復帰地点、ステージ状態、カメラ状態、プレイヤー内部状態を復帰用に整える
//
// 非責務:
// - 死亡アニメーションや VFX / SE の具体再生は担当しない
// - チェックポイントの保存ロジックは担当しない
// - ステージオブジェクト個別の復帰内容は担当しない
//
// 依存先:
// - CheckpointSystem: 復帰地点の解決
// - StageResetSystem: ステージ上オブジェクトの復帰
// - DeathTransitionView: 黒フェードの表示制御
// - PlayerCameraController: 復帰時のカメラ状態リセット
// - rb / reactionState / LogHealth(): 他 partial 側の状態と処理
//
// 前提条件:
// - この partial は PlayerController の一部として使われる
// - RequestDeathStart() が死亡開始の統一入口として使われる
// - 復帰前後の演出や入力制御の詳細は他 partial / 他コンポーネント側と協調する
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
    // Inspector 設定値
    // =====================================================================

    [Header("参照: CheckpointSystem")]
    [Tooltip("同一シーン内の復帰地点を解決するシステムです。CoRespawnSequence で現在のチェックポイント取得に使います。未設定時は実行時に探索を試み、見つからない場合は復帰地点を解決できません。")]
    [SerializeField] private CheckpointSystem checkpointSystem;

    [Header("参照: StageResetSystem")]
    [Tooltip("死亡復帰時にステージ上の敵やギミックを初期状態へ戻すシステムです。CoRespawnSequence で復帰前のステージ巻き戻しに使います。未設定時は実行時に探索を試み、見つからない場合はプレイヤーだけが復帰します。")]
    [SerializeField] private StageResetSystem stageResetSystem;

    [Header("参照: DeathTransitionView")]
    [Tooltip("死亡時の黒トランジション表示を担当する View です。CoRespawnSequence で黒フェードの開始と終了に使います。未設定時は実行時に探索を試み、見つからない場合は復帰処理が画面に見える可能性があります。")]
    [SerializeField] private DeathTransitionView deathTransitionView;

    [Header("参照: PlayerCameraController")]
    [Tooltip("復帰時に標準カメラ状態へ戻すためのカメラ制御コンポーネントです。ResetCameraToWorldDefaults で temporary target や各種上書きを解除するために使います。未設定時は実行時に探索を試みます。")]
    [SerializeField] private PlayerCameraController playerCameraController;

    // =====================================================================
    // 実行時状態
    // =====================================================================

    // 死亡シーケンス開始済みかどうか。
    // true の間は追加の死亡開始要求を無視し、二重発火を防ぐ。
    private bool isDeathSequencePlaying = false;

    // 最後に受理した死因。
    // デバッグ確認と、将来の死因別演出分岐の参照元として保持する。
    private DeathCause lastDeathCause = DeathCause.Damage;

    // 現在進行中の復帰シーケンス。
    // 死亡要求が重なったときに古いシーケンスを止めるために保持する。
    private Coroutine respawnSequenceCoroutine;

    // =====================================================================
    // 死亡開始入口
    // =====================================================================

    // 外部コンポーネント向けの環境死入口。
    // 内部の死亡統一入口へ Hazard で委譲する。
    public bool RequestHazardDeath()
    {
        return RequestDeathStart(DeathCause.Hazard);
    }

    // 死亡開始要求の統一入口。
    // 初回の要求だけを受理し、死因保存・Dead 状態遷移・復帰シーケンス開始までをまとめて行う。
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
        // 死亡入口を 1 箇所に寄せ、二重遷移を防ぐ。
        if (reactionState != PlayerReactionState.Dead)
        {
            ChangeReactionState(PlayerReactionState.Dead);
            LogHealth("Death state entered");
        }

        StartRespawnSequence();
        return true;
    }

    // =====================================================================
    // 復帰シーケンス開始
    // =====================================================================

    // 復帰シーケンスを開始する。
    // すでに進行中のものがあれば停止してから新しいコルーチンを起動する。
    private void StartRespawnSequence()
    {
        if (respawnSequenceCoroutine != null)
        {
            StopCoroutine(respawnSequenceCoroutine);
        }

        respawnSequenceCoroutine = StartCoroutine(CoRespawnSequence());
    }

    // =====================================================================
    // 復帰シーケンス本体
    // =====================================================================

    // 黒トランジションで復帰処理を隠しながら、ステージ復元・チェックポイント復帰・カメラ復元を進める。
    private IEnumerator CoRespawnSequence()
    {
        LogRespawn("Respawn sequence started");

        if (deathTransitionView == null)
        {
            deathTransitionView = FindFirstObjectByType<DeathTransitionView>();
        }

        if (deathTransitionView != null)
        {
            LogRespawn("Death transition in started");
            deathTransitionView.PlayTransitionIn();

            // 十分に黒くなるまで待ってから復帰処理へ進める。
            // これにより位置移動やステージ初期化を画面上で見せにくくする。
            yield return new WaitUntil(() =>
                deathTransitionView == null ||
                deathTransitionView.GetBlackAmount() >= deathTransitionView.BlackRespawnThreshold);

            LogRespawn("Death transition reached respawn threshold");
        }
        else
        {
            LogRespawnWarning("DeathTransitionView missing (respawn will be visible)");
        }

        if (stageResetSystem == null)
        {
            stageResetSystem = FindFirstObjectByType<StageResetSystem>();
        }

        if (stageResetSystem != null)
        {
            stageResetSystem.ResetAllToRespawnState();
        }
        else
        {
            LogRespawnWarning("StageResetSystem missing (stage objects were not reset)");
        }

        if (checkpointSystem == null)
        {
            checkpointSystem = FindFirstObjectByType<CheckpointSystem>();
        }

        if (checkpointSystem == null)
        {
            LogRespawnWarning("Respawn checkpoint missing");
            yield return FinishRespawnSequence();
            yield break;
        }

        Transform checkpoint = checkpointSystem.GetCurrentCheckpoint();
        if (checkpoint == null)
        {
            LogRespawnWarning("Respawn checkpoint missing");
            yield return FinishRespawnSequence();
            yield break;
        }

        ResetCameraToWorldDefaults();

        LogRespawn($"Respawn checkpoint resolved: {checkpoint.name}");
        RespawnAt(checkpoint.position);
        LogRespawn("Respawn hidden by black");

        yield return FinishRespawnSequence();
    }

    // 黒トランジションを通常状態へ戻し、死亡シーケンス進行フラグを解放する。
    private IEnumerator FinishRespawnSequence()
    {
        if (deathTransitionView != null)
        {
            LogRespawn("Death transition out started");
            deathTransitionView.PlayTransitionOut();

            yield return new WaitUntil(() =>
                deathTransitionView == null ||
                deathTransitionView.GetBlackAmount() <= 0.01f);

            deathTransitionView.ResetTransitionImmediate();
            LogRespawn("Death transition reset");
        }

        isDeathSequencePlaying = false;
        respawnSequenceCoroutine = null;
    }

    // =====================================================================
    // 復帰時のカメラ整理
    // =====================================================================

    // 復帰時にカメラをワールド基準の通常状態へ戻す。
    // 一時注視や Zone 上書きが残ったまま復帰しないようにする。
    private void ResetCameraToWorldDefaults()
    {
        if (playerCameraController == null)
        {
            playerCameraController = FindFirstObjectByType<PlayerCameraController>();
        }

        if (playerCameraController == null)
        {
            LogRespawnWarning("PlayerCameraController missing (camera reset skipped)");
            return;
        }

        playerCameraController.ClearTemporaryTarget();
        playerCameraController.ClearActiveOrthographicSizeOverride();
        playerCameraController.ClearActiveFollowSmoothingOverride();
        playerCameraController.ClearActiveOrthographicSizeSmoothTimeOverride();
    }

    // =====================================================================
    // 復帰位置反映
    // =====================================================================

    // プレイヤーを指定ワールド座標へ復帰させる。
    // 復帰前に内部状態を初期化し、その後 Rigidbody または Transform へ位置を反映する。
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

    // =====================================================================
    // 復帰用内部状態リセット
    // =====================================================================

    // 復帰時にプレイヤー内部状態を通常開始状態へ戻す。
    // 体力、ノックバック、リアクション、掴み、接地、壁、ステップ、入力バッファ類をまとめて初期化する。
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

        // 将来の演出追加ポイント:
        // 見た目回転リセットや追加演出リセット処理をここへ差し込む。
    }

    // =====================================================================
    // デバッグログ
    // =====================================================================

    // 復帰進行ログ。
    // 現在は常時出力で、死亡復帰シーケンスの追跡に使う。
    private void LogRespawn(string message)
    {
        Debug.Log($"[PlayerRespawn] {message}", this);
    }

    // 復帰関連の警告ログ。
    // 必須参照不足や復帰不能条件の観測に使う。
    private void LogRespawnWarning(string message)
    {
        Debug.LogWarning($"[PlayerRespawn] {message}", this);
    }
}