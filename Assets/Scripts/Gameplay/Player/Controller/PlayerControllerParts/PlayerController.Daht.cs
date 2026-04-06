using UnityEngine;

public sealed partial class PlayerController
{
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

        // すでにダッシュ中なら開始しない。
        if (isDashing)
        {
            return;
        }

        // クールダウン中は開始しない。
        if (dashCooldownTimer > 0f)
        {
            return;
        }

        // レールに乗っているときはダッシュを開始しない。
        if (isGrinding)
        {
            return;
        }

        // 空中ダッシュ無効時は、非接地なら開始しない。
        if (!isGrounded && !movementSettings.allowAirDash)
        {
            return;
        }
        // ダッシュ開始時は壁捕まり状態を解除する。
        ExitWallGrab();
        // ダッシュ開始状態へ入る。
        isDashing = true;
        isFastFalling = false;
        dashTimer = movementSettings.dashDuration;
        dashCooldownTimer = movementSettings.dashCooldown;
        dashStartVerticalVelocity = rb.linearVelocity.y;
        Vector2 dashStartVelocity = rb.linearVelocity;
        dashStartVelocity.y = 0f;
        rb.linearVelocity = dashStartVelocity;
        dashDirection = facing;
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
}