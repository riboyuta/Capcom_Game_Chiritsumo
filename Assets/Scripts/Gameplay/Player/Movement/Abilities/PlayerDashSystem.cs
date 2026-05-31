using UnityEngine;

// ダッシュ全般を管理するシステム。
internal sealed class PlayerDashSystem
{
    private readonly PlayerLocomotionDependencies deps;

    // ダッシュバッファタイマー。
    private float dashBufferTimer;

    // HoodRecover 要求を PlayerModelView が拾えるように短時間保持する。
    private float hoodRecoverRequestHoldTimer;

    // 外部トリガー由来の回復は PlayerModelView.Update とタイミングがずれる可能性があるため、
    // 1フレームではなく短時間だけ要求を保持する。
    private const float HoodRecoverRequestHoldDuration = 0.12f;

    // ダッシュ方向ベクトルの成分が有効かを判定するしきい値。
    // 入力デッドゾーンではなく、0 に近い方向成分を無視するために使う。
    private const float DashDirectionConversionThreshold = 0.1f;

    // デバッグ表示用のダッシュバッファタイマー。
    internal float DashBufferTimer => dashBufferTimer;

    internal PlayerDashSystem(PlayerLocomotionDependencies deps)
    {
        this.deps = deps;
    }

    // ============================================================
    // 初期化・リセット
    // ============================================================

    // 復帰時にダッシュ内部タイマーを初期化する。
    internal void ResetRuntimeTimers()
    {
        dashBufferTimer = 0f;
        hoodRecoverRequestHoldTimer = 0f;

        deps.RuntimeState.justDashStartedThisFrame = false;
        deps.RuntimeState.requestHoodRecoverThisFrame = false;
        deps.RuntimeState.hoodVisualState = PlayerHoodVisualState.Up;
    }

    // 見た目用単発フラグをリセットする。
    internal void ResetOneShotFlags()
    {
        deps.RuntimeState.justDashStartedThisFrame = false;

        // HoodRecover 要求は外部ギミック接触などで FixedUpdate 外から立つことがあるため、
        // 通常の one-shot と同じタイミングでは消さない。
        if (hoodRecoverRequestHoldTimer <= 0f)
        {
            deps.RuntimeState.requestHoodRecoverThisFrame = false;
        }
    }

    // ============================================================
    // タイマー更新
    // ============================================================

    // ダッシュ継続タイマーを更新する。
    internal void UpdateDashTimers(float deltaTime, System.Action onDashEnd)
    {
        UpdateHoodRecoverRequestHoldTimer(deltaTime);

        if (!deps.RuntimeState.isDashing)
        {
            deps.RuntimeState.dashTimer = 0f;
            return;
        }

        deps.RuntimeState.dashTimer = Mathf.Max(0f, deps.RuntimeState.dashTimer - deltaTime);
        if (deps.RuntimeState.dashTimer <= 0f)
        {
            onDashEnd();
        }
    }

    private void UpdateHoodRecoverRequestHoldTimer(float deltaTime)
    {
        if (hoodRecoverRequestHoldTimer <= 0f)
        {
            return;
        }

        hoodRecoverRequestHoldTimer = Mathf.Max(0f, hoodRecoverRequestHoldTimer - deltaTime);

        if (hoodRecoverRequestHoldTimer <= 0f)
        {
            deps.RuntimeState.requestHoodRecoverThisFrame = false;
        }
    }

    // ダッシュ入力バッファタイマーを更新する。
    internal void UpdateDashBufferTimer(float deltaTime)
    {
        if (!deps.Settings.Dash.UseDashBuffer)
        {
            dashBufferTimer = 0f;
            return;
        }

        dashBufferTimer = Mathf.Max(0f, dashBufferTimer - deltaTime);
    }

    // 地上ダッシュ連続制限タイマーを更新する。
    internal void UpdateGroundDashCooldownTimer(float deltaTime)
    {
        deps.RuntimeState.groundDashCooldownTimer = Mathf.Max(0f, deps.RuntimeState.groundDashCooldownTimer - deltaTime);
    }

    // ============================================================
    // ダッシュリソース管理
    // ============================================================

    // ダッシュリソース回復状態を更新する。
    internal void UpdateDashResourceState()
    {
        HandleGroundDashRefill();
    }

    // 接地時ダッシュ回復を処理する。
    private void HandleGroundDashRefill()
    {
        if (!deps.Settings.Dash.UseGroundRefill)
        {
            return;
        }

        if (!deps.RuntimeState.isGrounded)
        {
            return;
        }

        if (deps.IsExternallyControlled() || deps.RuntimeState.isWallGrabbing)
        {
            return;
        }

        if (deps.RuntimeState.currentDashCharges < Mathf.Max(1, deps.Settings.Dash.MaxCharges))
        {
            TryRefillDash(DashRefillReason.Grounded);
        }
    }

    // 指定理由でダッシュ回復を試みる。
    internal bool TryRefillDash(DashRefillReason reason)
    {
        if (!CanRefillDash(reason))
        {
            return false;
        }

        int maxCharges = Mathf.Max(1, deps.Settings.Dash.MaxCharges);
        if (deps.RuntimeState.currentDashCharges >= maxCharges)
        {
            return false;
        }

        deps.RuntimeState.currentDashCharges = maxCharges;

        // フードが下がっている状態から回復したときだけ、
        // 上半身の HoodRecover 演出要求を立てる。
        if (deps.RuntimeState.hoodVisualState == PlayerHoodVisualState.Down)
        {
            deps.RuntimeState.requestHoodRecoverThisFrame = true;
            hoodRecoverRequestHoldTimer = HoodRecoverRequestHoldDuration;
        }

        return true;
    }

    // 指定理由でダッシュ回復可能か判定する。
    private bool CanRefillDash(DashRefillReason reason)
    {
        switch (reason)
        {
            case DashRefillReason.Grounded:
            case DashRefillReason.Gimmick:
            case DashRefillReason.Scripted:
            default:
                return true;
        }
    }

    // ============================================================
    // ダッシュ開始
    // ============================================================

    // ダッシュ開始を試みる。
    internal void TryStartDash(System.Action<int> onWallGrabExit)
    {
        if (!deps.CanAcceptDashInput())
        {
            deps.FrameRequests.dashRequested = false;
            dashBufferTimer = 0f;
            return;
        }

        if (!deps.Settings.Dash.UseDash)
        {
            deps.FrameRequests.dashRequested = false;
            dashBufferTimer = 0f;
            return;
        }

        bool immediateDashRequest = deps.FrameRequests.dashRequested;
        if (immediateDashRequest)
        {
            if (deps.Settings.Dash.UseDashBuffer)
            {
                dashBufferTimer = deps.Settings.Dash.DashBufferTime;
            }

            deps.FrameRequests.dashRequested = false;
        }

        bool hasBufferedDash = deps.Settings.Dash.UseDashBuffer && dashBufferTimer > 0f;
        bool hasDashRequest = immediateDashRequest || hasBufferedDash;
        if (!hasDashRequest)
        {
            return;
        }

        if (!CanStartDash())
        {
            return;
        }

        if (!TryConsumeDash())
        {
            return;
        }

        onWallGrabExit?.Invoke(0);
        deps.RuntimeState.isDashing = true;
        deps.RuntimeState.dashTimer = deps.Settings.Dash.Duration;

        // 見た目用の単発イベントとフード状態を更新する。
        deps.RuntimeState.justDashStartedThisFrame = true;
        deps.RuntimeState.requestHoodRecoverThisFrame = false;
        deps.RuntimeState.hoodVisualState = PlayerHoodVisualState.Down;

        if (deps.RuntimeState.isGrounded)
        {
            deps.RuntimeState.groundDashCooldownTimer = deps.Settings.Dash.GroundCooldownTime;
        }

        deps.RuntimeState.dashStartVerticalVelocity = deps.Rb.linearVelocity.y;
        Vector2 dashStartVelocity = deps.Rb.linearVelocity;
        dashStartVelocity.y = 0f;
        deps.Rb.linearVelocity = dashStartVelocity;

        deps.RuntimeState.dashDirection = ResolveDashStartDirection();
        if (deps.RuntimeState.dashDirection.x > 0f)
        {
            deps.RuntimeState.facing = 1;
        }
        else if (deps.RuntimeState.dashDirection.x < 0f)
        {
            deps.RuntimeState.facing = -1;
        }

        deps.RuntimeState.isWallSliding = false;
        deps.FrameRequests.dashRequested = false;
        dashBufferTimer = 0f;
        deps.FrameRequests.jumpRequested = false;
        if (deps.Settings.Jump.UseJumpBuffer)
        {
            // JumpBufferTimer のクリアは JumpSystem 側で処理
        }
    }

    // ダッシュ消費可否を判定する。
    private bool CanConsumeDash()
    {
        if (!deps.Settings.Dash.UseDash)
        {
            return false;
        }

        if (deps.RuntimeState.currentDashCharges <= 0)
        {
            return false;
        }

        if (deps.RuntimeState.isDashing)
        {
            return false;
        }

        if (deps.IsExternallyControlled())
        {
            return false;
        }

        return true;
    }

    // ダッシュ開始可否を判定する。
    private bool CanStartDash()
    {
        if (!CanConsumeDash())
        {
            return false;
        }

        if (deps.RuntimeState.isGrounded && deps.RuntimeState.groundDashCooldownTimer > 0f)
        {
            return false;
        }

        return true;
    }

    // ダッシュ残数消費を試みる。
    private bool TryConsumeDash()
    {
        if (!CanConsumeDash())
        {
            return false;
        }

        deps.RuntimeState.currentDashCharges = Mathf.Max(0, deps.RuntimeState.currentDashCharges - 1);
        return true;
    }

    // 現在ダッシュを使用可能かを判定する。
    internal bool CanUseDashNowInternal()
    {
        if (!deps.CanAcceptDashInput())
        {
            return false;
        }

        return CanStartDash();
    }

    // ダッシュ開始方向を解決する。
    private Vector2 ResolveDashStartDirection()
    {
        Vector2 requestedDirection = deps.InputReader.DashDirectionInput;
        if (requestedDirection == Vector2.zero)
        {
            return new Vector2(deps.RuntimeState.facing, 0f);
        }

        // PlayerInputReaderで既に正規化済み8方向なのでそのまま使う
        return requestedDirection;
    }

    // ============================================================
    // ダッシュ中・終了
    // ============================================================

    // ダッシュ中の専用速度を適用する。
    internal void ApplyDashVelocity(PlayerLocomotionModifierRequest modifier, System.Func<bool> tryApplyDashCornerCorrection)
    {
        // ダッシュ中に壁角へ引っかかった場合、先に位置補正を試す。
        // 成功した場合は、この Tick の壁接触情報が補正前の古い情報になるため、後続の壁方向変換では使わない。
        bool appliedDashCornerCorrection = tryApplyDashCornerCorrection();

        // 斜め下ダッシュで地面に触れた場合、下方向成分を消して横ダッシュへ変換する。
        // 既存仕様を維持するため、壁接触による縦変換より先に処理する。
        ConvertDiagonalDownDashOnGround();

        // 角補正に成功していない場合だけ、壁接触による縦ダッシュ変換を試す。
        // 角補正成功時に古い wallSide / isTouchingWall を使って誤変換するのを避ける。
        if (!appliedDashCornerCorrection)
        {
            ConvertDiagonalDashOnWall();
        }

        // ダッシュ時間が 0 に近すぎると除算が不安定になるため、最低値を保証する。
        float dashDuration = Mathf.Max(0.0001f, deps.Settings.Dash.Duration);

        // ダッシュ開始からどれだけ進んだかを 0 ～ 1 に正規化する。
        float dashProgress = 1f - Mathf.Clamp01(deps.RuntimeState.dashTimer / dashDuration);

        // EaseOutCubic。
        // ダッシュ後半ほど速度倍率が 1 に近づく。
        float ease = 1f - Mathf.Pow(1f - dashProgress, 3f);

        // 初速を 85% 残して、入力直後の重さを避ける。
        float dashCurveMultiplier = Mathf.Lerp(0.85f, 1f, ease);

        // 基本ダッシュ速度に、時間カーブと外部補正を掛けた最終速度を求める。
        float dashSpeed = deps.Settings.Dash.Speed
            * dashCurveMultiplier
            * modifier.dashSpeedMultiplier;

        // 現在のダッシュ方向へ速度を適用する。
        // dashDirection は長さ 1 の方向ベクトル前提なので、斜めだけ速くならない。
        deps.Rb.linearVelocity = deps.RuntimeState.dashDirection * dashSpeed;
    }

    // 斜め下ダッシュ中に地面へ接触したら、横ダッシュへ変換する。
    private void ConvertDiagonalDownDashOnGround()
    {
        // 現在のダッシュ方向をローカル変数へ退避する。
        // 判定中に deps.RuntimeState.dashDirection を何度も直接読むのを避け、条件の読みやすさを上げる。
        Vector2 dashDirection = deps.RuntimeState.dashDirection;

        // 下方向へ向かっていないなら、地面接触による横変換の対象外。
        if (dashDirection.y >= -DashDirectionConversionThreshold)
        {
            return;
        }

        // 横成分がほぼ無い真下ダッシュなら、横方向へ変換できないので対象外。
        if (Mathf.Abs(dashDirection.x) <= DashDirectionConversionThreshold)
        {
            return;
        }

        // 地面に触れていないなら、既存の地面変換は行わない。
        if (!deps.RuntimeState.isGrounded)
        {
            return;
        }

        // 右下なら右、左下なら左へ横ダッシュ化する。
        // 下方向成分を消すことで、地面へ押し付け続ける挙動を避ける。
        float horizontalSign = Mathf.Sign(dashDirection.x);
        deps.RuntimeState.dashDirection = new Vector2(horizontalSign, 0f);
    }

    // 斜めダッシュ中に進行方向側の壁へ接触したら、縦ダッシュへ変換する。
    private void ConvertDiagonalDashOnWall()
    {
        // ダッシュ中以外は、ダッシュ方向変換の対象外。
        if (!deps.RuntimeState.isDashing)
        {
            return;
        }

        // 壁掴まり中や崖登り中は、それぞれ専用挙動が主導するため変換しない。
        if (deps.RuntimeState.isWallGrabbing || deps.RuntimeState.isLedgeClimbing)
        {
            return;
        }

        // 壁に触れていない、または壁の左右方向が確定していない場合は変換しない。
        if (!deps.RuntimeState.isTouchingWall || deps.RuntimeState.wallSide == 0)
        {
            return;
        }

        // 現在のダッシュ方向をローカル変数へ退避する。
        Vector2 dashDirection = deps.RuntimeState.dashDirection;

        // 横成分と縦成分の両方がある場合だけ、斜めダッシュとして扱う。
        // 横・上・下などの単方向ダッシュは変換対象にしない。
        bool isDiagonalDash =
            Mathf.Abs(dashDirection.x) > DashDirectionConversionThreshold &&
            Mathf.Abs(dashDirection.y) > DashDirectionConversionThreshold;

        if (!isDiagonalDash)
        {
            return;
        }

        // 進行方向側の壁に向かっているときだけ変換する。
        // 例:
        // - 右壁 + 右向き成分あり → 変換する
        // - 右壁 + 左向き成分あり → 壁から離れているので変換しない
        bool movingIntoWall =
            dashDirection.x * deps.RuntimeState.wallSide > DashDirectionConversionThreshold;

        if (!movingIntoWall)
        {
            return;
        }

        // 壁で潰れる横成分だけを捨て、縦方向の意図を維持する。
        // Sign を使って (0, 1) または (0, -1) にすることで、速度の大きさを dashSpeed のまま保つ。
        float verticalSign = Mathf.Sign(dashDirection.y);
        deps.RuntimeState.dashDirection = new Vector2(0f, verticalSign);
    }

    internal void CancelDashForStomp()
    {
        if (!deps.RuntimeState.isDashing)
        {
            return;
        }

        deps.RuntimeState.isDashing = false;
        deps.RuntimeState.dashTimer = 0f;
    }

    // ダッシュ終了処理を実行する。
    internal void EndDash(System.Action setDashEndJumpCutLockTimer)
    {
        deps.RuntimeState.isDashing = false;
        TrySnapToGroundAfterDash();

        if (deps.Settings.Dash.RestoreStartVerticalVelocity)
        {
            Vector3 restoredVelocity = deps.Rb.linearVelocity;
            restoredVelocity.y = deps.RuntimeState.dashStartVerticalVelocity;
            deps.Rb.linearVelocity = restoredVelocity;
        }

        if (deps.RuntimeState.dashDirection.y > 0f)
        {
            Vector3 velocity = deps.Rb.linearVelocity;
            float maxUpwardVelocity = deps.Settings.Dash.UpwardDashEndVerticalSpeedClamp;

            if (velocity.y > maxUpwardVelocity)
            {
                velocity.y = maxUpwardVelocity;
                deps.Rb.linearVelocity = velocity;
            }
        }

        Vector3 finalVelocity = deps.Rb.linearVelocity;
        finalVelocity.x *= deps.Settings.Dash.DashEndHorizontalCarryMultiplier;
        deps.Rb.linearVelocity = finalVelocity;

        setDashEndJumpCutLockTimer();
    }

    // ダッシュ終了後の接地スナップを試みる。
    private void TrySnapToGroundAfterDash()
    {
        if (!CanSnapToGroundAfterDash())
        {
            return;
        }

        if (!TryGetDashGroundSnapTarget(out RaycastHit hit))
        {
            return;
        }

        Vector3 position = deps.Rb.position;
        float capsuleBottomY = deps.CapsuleCollider.bounds.min.y;
        float targetPositionY = hit.point.y + (position.y - capsuleBottomY);
        float snapDeltaY = targetPositionY - position.y;

        if (snapDeltaY < 0f || snapDeltaY > deps.Settings.Dash.GroundSnapDistance)
        {
            return;
        }

        deps.Rb.position = new Vector3(position.x, targetPositionY, position.z);
        deps.RuntimeState.isGrounded = true;
    }

    // ダッシュ終了後の接地スナップ可否を判定する。
    private bool CanSnapToGroundAfterDash()
    {
        if (!deps.Settings.Dash.UseGroundSnap)
        {
            return false;
        }

        if (deps.RuntimeState.isGrounded || deps.RuntimeState.isWallGrabbing || deps.IsExternallyControlled())
        {
            return false;
        }

        if (deps.RuntimeState.dashDirection.y > 0f)
        {
            return false;
        }

        return TryGetDashGroundSnapTarget(out _);
    }

    // ダッシュ接地スナップ先を取得する。
    private bool TryGetDashGroundSnapTarget(out RaycastHit hit)
    {
        Vector3 up = deps.Transform.up;
        Vector3 worldCenter = deps.Transform.TransformPoint(deps.CapsuleCollider.center);
        float worldRadius = deps.GetWorldCapsuleRadius();
        float halfHeight = Mathf.Max(deps.CapsuleCollider.height * 0.5f, deps.CapsuleCollider.radius);
        float worldHalfHeight = halfHeight * Mathf.Abs(deps.Transform.lossyScale.y);
        Vector3 bottomSphereCenter = worldCenter - up * (worldHalfHeight - worldRadius);
        float castDistance = deps.Settings.Dash.GroundSnapDistance + 0.01f;

        return Physics.SphereCast(
            bottomSphereCenter,
            worldRadius * 0.95f,
            -up,
            out hit,
            castDistance,
            deps.Settings.Detection.GroundLayerMask,
            QueryTriggerInteraction.Ignore);
    }
}