using UnityEngine;

public sealed partial class PlayerController
{
    private void UpdateJumpAssistTimers(float deltaTime)
    {
        // 接地中はコヨーテタイマーを最大値へ戻す。
        // 無効時は 0 に固定する。
        if (isGrounded)
        {
            coyoteTimer = movementSettings.useCoyoteTime ? movementSettings.coyoteTime : 0f;
        }
        // 非接地中は残り時間を減らす。
        else
        {
            coyoteTimer = Mathf.Max(0f, coyoteTimer - deltaTime);
        }

        // ジャンプバッファ無効時は常に 0 にする。
        if (!movementSettings.useJumpBuffer)
        {
            jumpBufferTimer = 0f;
        }
        else
        {
            // ジャンプバッファの残り時間を減らす。
            jumpBufferTimer = Mathf.Max(0f, jumpBufferTimer - deltaTime);
        }

        // ジャンプ上昇維持タイマーの残り時間を減らす。
        jumpHoldTimer = Mathf.Max(0f, jumpHoldTimer - deltaTime);
    }

    private void ApplyJump()
    {
        // 前ステップ中はジャンプを受け付けない。
        // バッファ有効時でもここで破棄する。
        if (isStepping)
        {
            jumpRequested = false;
            if (movementSettings.useJumpBuffer)
            {
                jumpBufferTimer = 0f;
            }

            return;
        }

        // 今フレームのジャンプリクエストを読み取り、
        // 読み取った後は消費済みとして false に戻す。
        bool requested = jumpRequested;
        jumpRequested = false;

        // ジャンプバッファ有効時は、
        // 押された瞬間に一定時間だけ要求を保持する。
        if (requested && movementSettings.useJumpBuffer)
        {
            jumpBufferTimer = movementSettings.jumpBufferTime;
        }

        // バッファ有効時はタイマーで判定する。
        // 無効時はこのフレームの入力だけを見る。
        bool hasJumpRequest = movementSettings.useJumpBuffer ? jumpBufferTimer > 0f : requested;
        if (!hasJumpRequest)
        {
            return;
        }

        // 非接地かつ壁接触中なら壁キックを優先する。
        if (TryApplyWallKick())
        {
            jumpBufferTimer = 0f;
            return;
        }

        // 接地中、またはコヨーテ時間内なら通常ジャンプ可能。
        bool canJump = movementSettings.useCoyoteTime ? coyoteTimer > 0f : isGrounded;
        if (!canJump)
        {
            return;
        }

        // Y 速度をジャンプ初速へ置き換える。
        Vector3 velocity = rb.linearVelocity;
        velocity.y = movementSettings.jumpVelocity;
        rb.linearVelocity = velocity;

        // ジャンプ成立後は地上扱いと補助タイマーを解除する。
        isGrounded = false;
        coyoteTimer = 0f;
        jumpBufferTimer = 0f;
        jumpHoldTimer = movementSettings.maxJumpHoldTime;
        justJumpedThisFrame = true;
        PlayJumpSound();
    }

    private bool TryApplyWallKick()
    {
        // 前ステップ中は壁キックしない。
        if (isStepping)
        {
            return false;
        }

        // 機能が無効なら何もしない。
        if (!movementSettings.useWallKick)
        {
            return false;
        }

        // 接地中、壁未接触、壁方向不明のときは不可。
        if (isGrounded || !isTouchingWall || wallSide == 0)
        {
            return false;
        }

        // 再付着ロック中は壁キック候補にしない。
        if (wallReattachLockTimer > 0f)
        {
            return false;
        }
        float inputX = Mathf.Clamp(playerInputReader.Move.x, -1f, 1f);
        bool hasHorizontalInput = Mathf.Abs(inputX) >= movementSettings.wallInputThreshold;
        if (!hasHorizontalInput)
        {
            return false;
        }
        // 壁と反対方向へ横速度を与え、
        // 上方向には壁ジャンプ用の初速を与える。
        Vector3 velocity = rb.linearVelocity;
        velocity.x = -wallSide * movementSettings.wallJumpHorizontalVelocity;
        velocity.y = movementSettings.wallJumpVerticalVelocity;
        rb.linearVelocity = velocity;

        // 壁ジャンプ直後は一定時間だけ横制御と再付着を制限する。
        wallJumpControlLockTimer = movementSettings.wallJumpControlLockTime;
        wallReattachLockTimer = movementSettings.wallReattachLockTime;
        coyoteTimer = 0f;
        jumpHoldTimer = movementSettings.maxJumpHoldTime;
        isGrounded = false;
        justWallJumpedThisFrame = true;
        //振動
        PlayWallKickVibration();
        //音声
        PlayWallKickSound();

        return true;
    }

    private void ApplyVariableJumpCut()
    {
        // 可変ジャンプ無効なら何もしない。
        if (!movementSettings.useVariableJump)
        {
            return;
        }

        // 外部要因（バネ床など）で打ち上げられた場合はカットしない。
        if (isExternalLaunched)
        {
            return;
        }

        // ボタンを押し続けている間は切らない。
        if (playerInputReader.JumpHeld)
        {
            return;
        }

        Vector3 velocity = rb.linearVelocity;

        // 上昇中だけジャンプカットを適用する。
        if (velocity.y <= 0f)
        {
            return;
        }

        // 上向き速度を一定値まで切り詰めて、
        // 早離し時の低いジャンプを作る。
        float cutVelocityY = movementSettings.jumpVelocity * movementSettings.jumpCutMultiplier;
        velocity.y = Mathf.Min(velocity.y, cutVelocityY);
        rb.linearVelocity = velocity;
    }

    private void ApplyWallSlide()
    {
        // 毎フレーム先に false へ戻し、
        // 条件を満たしたときだけ true にする。
        isWallSliding = false;

        // 機能が無効なら何もしない。
        if (!movementSettings.useWallSlide)
        {
            return;
        }

        // 再付着ロック中は壁滑りへ入れない。
        if (wallReattachLockTimer > 0f)
        {
            return;
        }

        // 接地中、壁未接触、壁方向不明のときは不可。
        if (isGrounded || !isTouchingWall || wallSide == 0)
        {
            return;
        }

        Vector3 velocity = rb.linearVelocity;

        // 落下中だけ壁滑りを許可する。
        if (velocity.y >= 0f)
        {
            return;
        }

        // 壁方向へ入力しているときだけ壁滑りに入る。
        float inputX = Mathf.Clamp(playerInputReader.Move.x, -1f, 1f);
        float wallDirection = wallSide;
        bool pushingToWall = inputX * wallDirection >= movementSettings.wallInputThreshold;
        if (!pushingToWall)
        {
            return;
        }

        // 落下速度が速すぎる場合は上限まで抑える。
        float minVelocityY = -movementSettings.wallSlideMaxSpeed;
        if (velocity.y < minVelocityY)
        {
            velocity.y = minVelocityY;
            rb.linearVelocity = velocity;
        }

        isWallSliding = true;
        isFastFalling = false;
    }

    private void TryStartFastFall()
    {
        // 機能が無効なら何もしない。
        if (!movementSettings.useFastFall)
        {
            return;
        }

        // 接地中・前ステ中・壁滑り中は開始しない。
        if (isGrounded || isStepping || isWallSliding)
        {
            return;
        }

        // 下入力押下時のみ急降下状態へ入る。
        if (!playerInputReader.DownPressed)
        {
            return;
        }

        isFastFalling = true;
    }
}