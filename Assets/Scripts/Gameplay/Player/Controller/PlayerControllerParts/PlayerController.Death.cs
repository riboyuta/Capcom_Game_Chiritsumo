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
// - PlayerDeathView: 倒れ演出と黒フェード窓口
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

    [Header("参照: PlayerCameraController")]
    [Tooltip("復帰時に標準カメラ状態へ戻すためのカメラ制御コンポーネントです。ResetCameraToWorldDefaults で temporary target や各種上書きを解除するために使います。未設定時は実行時に探索を試みます。")]
    [SerializeField] private PlayerCameraController playerCameraController;
    [Header("参照: PlayerDeathView")]
    [Tooltip("敵攻撃死の入口で viewRoot の倒れ演出を再生する見た目コンポーネントです。未設定時は実行時に探索を試みます。")]
    [SerializeField] private PlayerDeathView playerDeathView;
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


    private float ConfiguredDamageDeathIntroDuration => playerDeathView != null ? playerDeathView.DamageDeathIntroDuration : 0f;
    private float ConfiguredDamageDeathTiltAngle => playerDeathView != null ? playerDeathView.DamageDeathTiltAngle : 80f;
    private float ConfiguredDamageDeathZoomSizeOffset => playerDeathView != null ? playerDeathView.DamageDeathZoomSizeOffset : -0.35f;
    private float ConfiguredDamageDeathZoomSmoothTime => playerDeathView != null ? playerDeathView.DamageDeathZoomSmoothTime : 0.08f;
    private float ConfiguredBlackRespawnThreshold => playerDeathView != null ? playerDeathView.BlackRespawnThreshold : 0.85f;    // =====================================================================
    // 死亡開始入口
    // =====================================================================

    // 外部コンポーネント向けの環境死入口。
    // 内部の死亡統一入口へ Hazard で委譲する。
    public bool RequestHazardDeath()
    {
        return RequestDeathStart(DeathCause.Hazard);
    }

    // 外部コンポーネント向けのダメージ死入口。
    // 内部の死亡統一入口へ Damage で委譲する。
    public bool RequestDamageDeath()
    {
        return RequestDeathStart(DeathCause.Damage);
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
        // 死亡開始時点の向きを固定し、死亡演出中の見た目反転を防ぐ。
        CaptureDeathFacingForVisual();

        LogHealth($"Death requested: {cause}");

        // まだ Dead に入っていないときだけ状態遷移を要求する。
        // 死亡入口を 1 箇所に寄せ、二重遷移を防ぐ。
        if (reactionState != PlayerReactionState.Dead)
        {
            ChangeReactionState(PlayerReactionState.Dead);
            LogHealth("Death state entered");
        }

        // 死亡開始入口で 1 回だけ振動と音声を通知する。
        // 実際の停止/再生詳細は各 Controller 側へ委譲する。
        PlayDeathVibration(cause);
        PlayDeathSound(cause);

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

        if (lastDeathCause == DeathCause.Damage)
        {
            PlayDamageDeathZoom();
            PlayDamageDeathIntro();
            yield return WaitForDamageDeathIntro();
        }

        ResolvePlayerDeathViewIfNeeded();
        if (playerDeathView != null)
        {
            if (lastDeathCause == DeathCause.Hazard)
            {
                LogRespawn("Hazard death uses immediate black transition");
                LogRespawn("Hazard black in started");
            }
            else
            {
                LogRespawn("Death transition in started");
            }

            playerDeathView.PlayTransitionIn(lastDeathCause);

            // 十分に黒くなるまで待ってから復帰処理へ進める。
            // これにより位置移動やステージ初期化を画面上で見せにくくする。
            yield return new WaitUntil(() =>
                playerDeathView == null ||
                playerDeathView.GetBlackAmount() >= ConfiguredBlackRespawnThreshold);

            LogRespawn("Death transition reached respawn threshold");
        }
        else
        {
            LogRespawnWarning("PlayerDeathView missing (transition/intro unavailable)");
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
        if (lastDeathCause == DeathCause.Damage)
        {
            ResetDamageDeathPresentation();
        }

        if (playerDeathView != null)
        {
            if (lastDeathCause == DeathCause.Hazard)
            {
                LogRespawn("Hazard black out started");
            }
            else
            {
                LogRespawn("Death transition out started");
            }

            playerDeathView.PlayTransitionOut(lastDeathCause);

            yield return new WaitUntil(() =>
                playerDeathView == null ||
                playerDeathView.GetBlackAmount() <= 0.01f);

            playerDeathView.ResetTransitionImmediate();
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

        playerCameraController.ResetRuntimeStateForRespawn();
    }

    private void PlayDamageDeathZoom()
    {
        if (playerCameraController == null)
        {
            playerCameraController = FindFirstObjectByType<PlayerCameraController>();
        }

        if (playerCameraController == null)
        {
            LogRespawnWarning("PlayerCameraController missing (damage death zoom skipped)");
            return;
        }

        float targetSize = Mathf.Max(0.01f, playerCameraController.EffectiveSize + ConfiguredDamageDeathZoomSizeOffset);
        playerCameraController.SetActiveOrthographicSizeSmoothTimeOverride(ConfiguredDamageDeathZoomSmoothTime);
        playerCameraController.SetActiveOrthographicSizeOverride(targetSize);
        LogRespawn("Damage death zoom applied");
    }

    private void PlayDamageDeathIntro()
    {
        ResolvePlayerDeathViewIfNeeded();

        if (playerDeathView == null)
        {
            LogRespawnWarning("Damage death rotation target missing");
            return;
        }

        playerDeathView.ConfigureDamageDeathIntro(
            ConfiguredDamageDeathIntroDuration,
            ConfiguredDamageDeathTiltAngle);
        playerDeathView.PlayDamageDeathIntro();
    }

    private IEnumerator WaitForDamageDeathIntro()
    {
        float duration = ConfiguredDamageDeathIntroDuration;
        if (duration <= 0f)
        {
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (playerDeathView == null || playerDeathView.IsIntroComplete())
            {
                break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private void ResetDamageDeathPresentation()
    {
        ResolvePlayerDeathViewIfNeeded();

        if (playerDeathView == null)
        {
            return;
        }

        playerDeathView.ResetDeathPresentation();
    }

    private void ResolvePlayerDeathViewIfNeeded()
    {
        if (playerDeathView == null)
        {
            playerDeathView = GetComponentInChildren<PlayerDeathView>();
        }
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
    // 体力、ノックバック、リアクション、掴み、接地、壁、ダッシュ、入力バッファ類をまとめて初期化する。
    private void ResetForRespawn()
    {
        // 復帰初期化時の保険として、残留振動と音声を明示停止する。
        vibrationController?.StopAllRumble();
        audioController?.StopAllSounds();

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
        isDashing = false;
        isFastFalling = false;
        dashTimer = 0.0f;
        groundDashCooldownTimer = 0.0f;
        currentDashCharges = Mathf.Max(1, movementSettings.Dash.MaxCharges);
        wasGroundedLastFrame = false;
        dashStartVerticalVelocity = 0.0f;
        jumpRequested = false;
        dashRequested = false;
        dashBufferTimer = 0.0f;
        coyoteTimer = 0.0f;
        jumpBufferTimer = 0.0f;
        jumpHoldTimer = 0.0f;
        justLandedThisFrame = false;
        justJumpedThisFrame = false;
        justWallJumpedThisFrame = false;
        justCrossedApexThisFrame = false;

        ResetDamageDeathPresentation();
        // 復帰後は通常どおり facing 更新を使うため、死亡向き固定を解除する。
        ClearDeathFacingLock();
    }

    // 死亡開始時点の facing を描画向きとして固定する。
    // 0 が入る異常値にも備え、必ず -1 / +1 のどちらかへ補正する。
    private void CaptureDeathFacingForVisual()
    {
        fixedDeathFacing = NormalizeFacingSign(facing);
        isDeathFacingFixed = true;
    }

    // 復帰時に死亡向き固定を解除し、通常の facing 反映へ戻す。
    private void ClearDeathFacingLock()
    {
        isDeathFacingFixed = false;
    }

    // 向き値を -1 / +1 のいずれかへ補正する。
    // 0 や想定外の値が来た場合は右向き(+1)へ寄せて安全に扱う。
    private int NormalizeFacingSign(int direction)
    {
        if (direction < 0)
        {
            return -1;
        }

        return 1;
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