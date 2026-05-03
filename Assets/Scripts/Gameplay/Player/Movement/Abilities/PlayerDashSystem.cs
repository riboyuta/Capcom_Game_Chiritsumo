using UnityEngine;

// ダッシュ全般を管理するシステム。
internal sealed class PlayerDashSystem
{
    private readonly PlayerLocomotionDependencies deps;

    // ダッシュバッファタイマー。
    private float dashBufferTimer;

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
    }

    // ============================================================
    // タイマー更新
    // ============================================================

    // ダッシュ継続タイマーを更新する。
    internal void UpdateDashTimers(float deltaTime, System.Action onDashEnd)
    {
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
        deps.RuntimeState.isFastFalling = false;
        deps.RuntimeState.dashTimer = deps.Settings.Dash.Duration;
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
        tryApplyDashCornerCorrection();
        deps.Rb.linearVelocity = deps.RuntimeState.dashDirection * (deps.Settings.Dash.Speed * modifier.dashSpeedMultiplier);
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

        {
            Vector3 velocity = deps.Rb.linearVelocity;
            velocity.x *= deps.Settings.Dash.DashEndHorizontalCarryMultiplier;
            deps.Rb.linearVelocity = velocity;
        }

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
