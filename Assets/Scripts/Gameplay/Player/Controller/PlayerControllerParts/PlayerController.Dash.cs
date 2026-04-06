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

        // 次フレームの接地遷移検出用に状態を保存する。
        wasGroundedLastFrame = isGrounded;
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

        // ダッシュのクールダウン残り時間を減らす。
        dashCooldownTimer = Mathf.Max(0f, dashCooldownTimer - deltaTime);

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

        // 再入力ロック中は消費不可。
        if (dashCooldownTimer > 0f)
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

        // 残数・状態・再入力ロック条件を満たすか確認する。
        if (!CanConsumeDash())
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
        dashCooldownTimer = movementSettings.dashRetryLockTime;
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