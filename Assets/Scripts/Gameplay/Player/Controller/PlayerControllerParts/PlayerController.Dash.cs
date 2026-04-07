using UnityEngine;

public sealed partial class PlayerController
{
    public enum DashRefillReason
    {
        Grounded,
        Gimmick,
        Scripted
    }

    private void UpdateDashResourceState()
    {
        // 接地/状態に応じたダッシュ回復を処理する。
        HandleGroundDashRefill();

    }

    private void HandleGroundDashRefill()
    {
        // 接地回復が無効なら何もしない。
        if (!movementSettings.useGroundDashRefill)
        {
            return;
        }

        // 接地していない間は回復しない。
        if (!isGrounded)
        {
            return;
        }

        // レール滑走中や壁捕まり中は接地回復対象外とする。
        if (isGrinding || isWallGrabbing)
        {
            return;
        }

        // 残数不足時だけ回復を試みる。
        if (currentDashCharges < Mathf.Max(1, movementSettings.maxDashCharges))
        {
            TryRefillDash(DashRefillReason.Grounded);
        }
    }

    private void UpdateDashTimers(float deltaTime)
    {



        // ダッシュ中でなければダッシュ時間は 0 に戻す。
        if (!isDashing)
        {
            dashTimer = 0f;
            return;
        }

        // ダッシュ中は残り時間を減らす。
        dashTimer = Mathf.Max(0f, dashTimer - deltaTime);

        // 残り時間がなくなったらダッシュ終了とする。
        if (dashTimer <= 0f)
        {
            EndDash();
        }
    }
    private bool CanConsumeDash()
    {
        // ダッシュ機能が無効なら消費不可。
        if (!movementSettings.useDash)
        {
            return false;
        }

        // 残数がないなら消費不可。
        if (currentDashCharges <= 0)
        {
            return false;
        }

        // すでにダッシュ中なら消費不可。
        if (isDashing)
        {
            return false;
        }

        // レール滑走中は消費不可。
        if (isGrinding)
        {
            return false;
        }

        return true;
    }

    private bool CanStartDash()
    {
        // 残数・状態など、基本の消費条件を満たしているかを確認する。
        if (!CanConsumeDash())
        {
            return false;
        }



        // 地上中だけ地上ダッシュ連続制限タイマーを確認する。
        if (isGrounded && groundDashCooldownTimer > 0f)
        {
            return false;
        }

        return true;
    }

    private bool TryConsumeDash()
    {
        if (!CanConsumeDash())
        {
            return false;
        }

        currentDashCharges = Mathf.Max(0, currentDashCharges - 1);
        return true;
    }

    private bool CanRefillDash(DashRefillReason reason)
    {
        // 将来理由ごとに制限を追加できるよう、分岐構造だけ先に置く。
        switch (reason)
        {
            case DashRefillReason.Grounded:
            case DashRefillReason.Gimmick:
            case DashRefillReason.Scripted:
            default:
                return true;
        }
    }

    public bool TryRefillDash(DashRefillReason reason)
    {
        if (!CanRefillDash(reason))
        {
            return false;
        }

        int maxCharges = Mathf.Max(1, movementSettings.maxDashCharges);
        if (currentDashCharges >= maxCharges)
        {
            return false;
        }

        currentDashCharges = maxCharges;
        return true;
    }
    private void EndDash()
    {
        isDashing = false;
        TrySnapToGroundAfterDash();

        if (!movementSettings.restoreDashStartVerticalVelocity)
        {
            return;
        }

        Vector3 velocity = rb.linearVelocity;
        velocity.y = dashStartVerticalVelocity;
        rb.linearVelocity = velocity;
    }

    private void UpdateDashBufferTimer(float deltaTime)
    {
        // ダッシュバッファ無効時は常に 0 にする。
        if (!movementSettings.useDashBuffer)
        {
            dashBufferTimer = 0f;
            return;
        }

        // ダッシュバッファの残り時間を減らす。
        dashBufferTimer = Mathf.Max(0f, dashBufferTimer - deltaTime);
    }

    private void TryStartDash()
    {
        // 機能が無効なら入力とバッファを破棄する。
        if (!movementSettings.useDash)
        {
            dashRequested = false;
            dashBufferTimer = 0f;
            return;
        }

        // 今フレームのダッシュ要求を読み取る。
        bool immediateDashRequest = dashRequested;

        // 入力があった場合は、必要ならバッファ時間を開始する。
        if (immediateDashRequest)
        {
            if (movementSettings.useDashBuffer)
            {
                dashBufferTimer = movementSettings.dashBufferTime;
            }

            // 読み取った入力は消費済みにする。
            dashRequested = false;
        }

        // バッファ中なら過去の入力でもダッシュ要求ありとみなす。
        bool hasBufferedDash = movementSettings.useDashBuffer && dashBufferTimer > 0f;
        bool hasDashRequest = immediateDashRequest || hasBufferedDash;

        // 要求がなければ何もしない。
        if (!hasDashRequest)
        {
            return;
        }

        // 残数・状態・再入力ロック・地上連続制限条件を満たすか確認する。
        if (!CanStartDash())
        {
            return;
        }

        // ダッシュリソースを消費できなければ開始しない。
        if (!TryConsumeDash())
        {
            return;
        }

        // ダッシュ開始時は壁捕まり状態を解除する。
        ExitWallGrab();
        // ダッシュ開始状態へ入る。
        isDashing = true;
        isFastFalling = false;
        dashTimer = movementSettings.dashDuration;
        if (isGrounded)
        {
            groundDashCooldownTimer = movementSettings.groundDashCooldownTime;
        }
        dashStartVerticalVelocity = rb.linearVelocity.y;
        Vector2 dashStartVelocity = rb.linearVelocity;

        dashStartVelocity.y = 0f;
        rb.linearVelocity = dashStartVelocity;
        dashDirection = ResolveDashStartDirection();
        if (dashDirection.x > 0f)
        {
            facing = 1;
        }
        else if (dashDirection.x < 0f)
        {
            facing = -1;
        }
        isWallSliding = false;

        // ダッシュ入力とバッファを消費する。
        dashRequested = false;
        dashBufferTimer = 0f;

        // ダッシュ開始時はジャンプ要求も破棄する。
        jumpRequested = false;
        if (movementSettings.useJumpBuffer)
        {
            jumpBufferTimer = 0f;
        }
    }

    private void UpdateGroundDashCooldownTimer(float deltaTime)
    {
        groundDashCooldownTimer = Mathf.Max(0f, groundDashCooldownTimer - deltaTime);
    }

    private void HandleGroundDashCooldownOnLanding()
    {
        // 空中から接地へ戻った瞬間のみ地上ダッシュ連続制限を解除する。
        if (!wasGroundedLastFrame && isGrounded)
        {
            groundDashCooldownTimer = 0f;
        }
    }

    private bool CanSnapToGroundAfterDash()
    {
        if (!movementSettings.useDashGroundSnap)
        {
            return false;
        }

        if (isGrounded || isWallGrabbing || isGrinding)
        {
            return false;
        }

        if (dashDirection.y > 0f)
        {
            return false;
        }

        return TryGetDashGroundSnapTarget(out _);
    }

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

        Vector3 position = rb.position;
        float capsuleBottomY = capsuleCollider.bounds.min.y;
        float targetPositionY = hit.point.y + (position.y - capsuleBottomY);
        float snapDeltaY = targetPositionY - position.y;

        if (snapDeltaY < 0f || snapDeltaY > movementSettings.dashGroundSnapDistance)
        {
            return;
        }

        rb.position = new Vector3(position.x, targetPositionY, position.z);
        isGrounded = true;
    }

    private bool TryGetDashGroundSnapTarget(out RaycastHit hit)
    {
        Vector3 up = transform.up;
        Vector3 worldCenter = transform.TransformPoint(capsuleCollider.center);
        float worldRadius = GetWorldCapsuleRadius();
        float halfHeight = Mathf.Max(capsuleCollider.height * 0.5f, capsuleCollider.radius);
        float worldHalfHeight = halfHeight * Mathf.Abs(transform.lossyScale.y);
        Vector3 bottomSphereCenter = worldCenter - up * (worldHalfHeight - worldRadius);
        float castDistance = movementSettings.dashGroundSnapDistance + 0.01f;

        return Physics.SphereCast(
            bottomSphereCenter,
            worldRadius * 0.95f,
            -up,
            out hit,
            castDistance,
            movementSettings.groundLayerMask,
            QueryTriggerInteraction.Ignore);
    }
    private Vector2 ResolveDashStartDirection()
    {
        Vector2 requestedDirection = playerInputReader.DashDirectionInput;
        if (requestedDirection == Vector2.zero)
        {
            return new Vector2(facing, 0f);
        }

        return requestedDirection.normalized;
    }
}