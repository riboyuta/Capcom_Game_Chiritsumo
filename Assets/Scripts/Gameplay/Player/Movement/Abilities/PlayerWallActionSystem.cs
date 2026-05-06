using UnityEngine;

// 壁滑り・壁掴まり・壁キックを管理するシステム。
internal sealed class PlayerWallActionSystem
{
    private readonly PlayerLocomotionDependencies deps;

    // 壁から離れた直後でも壁キックを許可する猶予タイマー。
    private float wallDetachGraceTimer;

    // 壁離脱猶予中に参照する最後の壁面方向。
    private int lastWallDetachSide;

    internal PlayerWallActionSystem(PlayerLocomotionDependencies deps)
    {
        this.deps = deps;
    }

    // ============================================================
    // 初期化・リセット
    // ============================================================

    // 復帰時に壁アクション内部タイマーを初期化する。
    internal void ResetRuntimeTimers()
    {
        wallDetachGraceTimer = 0f;
        lastWallDetachSide = 0;
        deps.RuntimeState.wallGrabRemainingTime = deps.Settings.Wall.WallGrabMaxHoldTime;
    }

    // ============================================================
    // タイマー更新
    // ============================================================

    // 壁掴まり制限時間を更新する。
    internal void UpdateWallGrabLimitTimer(float deltaTime)
    {
        if (deps.RuntimeState.isGrounded)
        {
            deps.RuntimeState.wallGrabRemainingTime = deps.Settings.Wall.WallGrabMaxHoldTime;
            return;
        }

        if (!deps.RuntimeState.isWallGrabbing)
        {
            return;
        }

        float inputY = Mathf.Clamp(deps.InputReader.Move.y, -1f, 1f);
        float threshold = deps.Settings.Wall.WallClimbInputThreshold;

        bool isClimbing =
            inputY > threshold ||
            inputY < -threshold;

        float drainPerSecond = isClimbing
            ? deps.Settings.Wall.WallGrabClimbDrainPerSecond
            : deps.Settings.Wall.WallGrabIdleDrainPerSecond;

        deps.RuntimeState.wallGrabRemainingTime = Mathf.Max(
            0f,
            deps.RuntimeState.wallGrabRemainingTime - drainPerSecond * deltaTime);
    }

    // 壁キック関連ロックタイマーを更新する。
    internal void UpdateWallJumpLockTimer(float deltaTime)
    {
        deps.RuntimeState.wallJumpControlLockTimer = Mathf.Max(0f, deps.RuntimeState.wallJumpControlLockTimer - deltaTime);
        deps.RuntimeState.wallReattachLockTimer = Mathf.Max(0f, deps.RuntimeState.wallReattachLockTimer - deltaTime);

        if (deps.RuntimeState.isGrounded)
        {
            wallDetachGraceTimer = 0f;
            lastWallDetachSide = 0;
            return;
        }

        if (deps.RuntimeState.isTouchingWall && deps.RuntimeState.wallSide != 0)
        {
            wallDetachGraceTimer = deps.Settings.Wall.WallDetachGraceTime;
            lastWallDetachSide = deps.RuntimeState.wallSide;
            return;
        }

        wallDetachGraceTimer = Mathf.Max(0f, wallDetachGraceTimer - deltaTime);
        if (wallDetachGraceTimer <= 0f)
        {
            lastWallDetachSide = 0;
        }
    }

    // ============================================================
    // 壁滑り
    // ============================================================

    // 壁滑り状態と落下速度上限を更新する。
    internal void ApplyWallSlide()
    {
        deps.RuntimeState.isWallSliding = false;

        if (!deps.Settings.Wall.UseWallSlide)
        {
            return;
        }

        if (deps.RuntimeState.wallReattachLockTimer > 0f)
        {
            return;
        }

        if (deps.RuntimeState.isGrounded || !deps.RuntimeState.isTouchingWall || deps.RuntimeState.wallSide == 0)
        {
            return;
        }

        Vector3 velocity = deps.Rb.linearVelocity;
        if (velocity.y >= 0f)
        {
            return;
        }

        float inputX = Mathf.Clamp(deps.InputReader.Move.x, -1f, 1f);
        float wallDirection = deps.RuntimeState.wallSide;
        bool pushingToWall = inputX * wallDirection >= deps.Settings.Detection.WallInputThreshold;
        if (!pushingToWall)
        {
            return;
        }

        float minVelocityY = -deps.Settings.Wall.WallSlideMaxSpeed;
        if (velocity.y < minVelocityY)
        {
            velocity.y = minVelocityY;
            deps.Rb.linearVelocity = velocity;
        }

        deps.RuntimeState.isWallSliding = true;
        deps.RuntimeState.isFastFalling = false;
    }

    // ============================================================
    // 壁掴まり
    // ============================================================

    // 壁捕まり状態の進入・離脱判定を更新する。
    internal void UpdateWallGrabState()
    {
        // 崖乗り上げ中は壁掴まり状態を更新しない。
        if (deps.RuntimeState.isLedgeClimbing)
        {
            return;
        }

        if (deps.RuntimeState.isWallGrabbing)
        {
            if (ShouldExitWallGrab())
            {
                ExitWallGrab();
            }

            return;
        }

        if (CanEnterWallGrab())
        {
            EnterWallGrab();
        }
    }

    // 壁捕まり進入可否を判定する。
    private bool CanEnterWallGrab()
    {
        if (!deps.CanAcceptGrabInput())
        {
            return false;
        }

        if (!deps.Settings.Wall.UseWallGrab)
        {
            return false;
        }

        if (deps.IsActionLocked() || deps.RuntimeState.isDashing || deps.IsExternallyControlled())
        {
            return false;
        }

        // 崖乗り上げ中は壁掴まりに進入しない。
        if (deps.RuntimeState.isLedgeClimbing)
        {
            return false;
        }

        if (!deps.RuntimeState.isTouchingWall || deps.RuntimeState.wallSide == 0)
        {
            return false;
        }

        if (deps.RuntimeState.wallReattachLockTimer > 0f)
        {
            return false;
        }

        if (!deps.InputReader.GrabHeld)
        {
            return false;
        }

        if (deps.RuntimeState.wallGrabRemainingTime <= 0f)
        {
            return false;
        }

        if (!IsWithinWallGrabRange(deps.Settings.Wall.WallGrabEnterDistance))
        {
            return false;
        }

        return true;
    }

    // 壁捕まり離脱可否を判定する。
    private bool ShouldExitWallGrab()
    {
        if (deps.IsActionLocked())
        {
            return true;
        }

        if (!deps.CanAcceptGrabInput())
        {
            return true;
        }

        if (!deps.InputReader.GrabHeld)
        {
            return true;
        }

        if (deps.RuntimeState.isDashing || deps.IsExternallyControlled())
        {
            return true;
        }

        if (!deps.RuntimeState.isTouchingWall || deps.RuntimeState.wallSide == 0)
        {
            return true;
        }

        if (deps.RuntimeState.wallReattachLockTimer > 0f)
        {
            return true;
        }

        if (deps.RuntimeState.wallGrabRemainingTime <= 0f)
        {
            return true;
        }

        if (!IsWithinWallGrabRange(deps.Settings.Wall.WallGrabExitDistance))
        {
            return true;
        }

        return false;
    }

    private bool IsWithinWallGrabRange(float maxDistance)
    {
        if (!deps.RuntimeState.isTouchingWall || deps.RuntimeState.wallSide == 0)
        {
            return false;
        }

        Bounds bounds = deps.CapsuleCollider.bounds;
        Vector3 wallDir = Vector3.right * deps.RuntimeState.wallSide;

        // プレイヤーの側面ギリギリ少し内側から前方へ飛ばす
        Vector3 origin = bounds.center;
        origin.y += bounds.extents.y * 0.15f;
        origin.x += wallDir.x * (bounds.extents.x - 0.01f);

        return Physics.Raycast(
            origin,
            wallDir,
            maxDistance + 0.01f,
            deps.Settings.Detection.GroundLayerMask,
            QueryTriggerInteraction.Ignore);
    }

    // 壁捕まり状態へ遷移させる。
    private void EnterWallGrab()
    {
        deps.RuntimeState.isWallGrabbing = true;
        deps.RuntimeState.wallGrabSide = deps.RuntimeState.wallSide;
        deps.RuntimeState.isWallSliding = false;
        deps.RuntimeState.isFastFalling = false;

        deps.Rb.useGravity = false;

        Vector3 velocity = deps.Rb.linearVelocity;
        velocity.x = 0f;
        velocity.y = 0f;
        deps.Rb.linearVelocity = velocity;
    }

    // 壁捕まり状態を解除する。
    internal void ExitWallGrab()
    {
        deps.RuntimeState.isWallGrabbing = false;
        deps.RuntimeState.wallGrabSide = 0;
        deps.Rb.useGravity = true;
    }

    // 壁捕まり中の専用移動を適用する。
    internal void ApplyWallGrabMovement(System.Func<bool> tryStartLedgeClimb)
    {
        if (!deps.RuntimeState.isWallGrabbing)
        {
            return;
        }

        deps.RuntimeState.facing = deps.RuntimeState.wallGrabSide;

        float inputY = Mathf.Clamp(deps.InputReader.Move.y, -1f, 1f);
        float threshold = deps.Settings.Wall.WallClimbInputThreshold;

        float targetVerticalSpeed = 0f;

        if (inputY > threshold)
        {
            targetVerticalSpeed = deps.Settings.Wall.WallClimbUpSpeed;

            if (tryStartLedgeClimb())
            {
                return;
            }
        }
        else if (inputY < -threshold)
        {
            targetVerticalSpeed = -deps.Settings.Wall.WallClimbDownSpeed;
        }

        Vector3 velocity = deps.Rb.linearVelocity;
        velocity.x = 0f;
        velocity.y = targetVerticalSpeed;
        deps.Rb.linearVelocity = velocity;

        deps.RuntimeState.isWallSliding = false;
        deps.RuntimeState.isFastFalling = false;
    }

    // ============================================================
    // 壁キック
    // ============================================================

    // 壁キックを開始できる場合に適用する。
    internal bool TryApplyWallKick()
    {
        if (deps.RuntimeState.isDashing)
        {
            return false;
        }

        if (!deps.Settings.Wall.UseWallKick)
        {
            return false;
        }

        if (deps.RuntimeState.isGrounded)
        {
            return false;
        }

        if (deps.RuntimeState.wallReattachLockTimer > 0f)
        {
            return false;
        }

        int side = ResolveWallKickSide();
        if (side == 0)
        {
            return false;
        }

        float inputX = Mathf.Clamp(deps.InputReader.Move.x, -1f, 1f);
        float threshold = deps.Settings.Detection.WallInputThreshold;
        bool hasHorizontalInput = Mathf.Abs(inputX) >= threshold;

        if (!hasHorizontalInput)
        {
            return false;
        }

        if (deps.RuntimeState.isWallGrabbing)
        {
            // 壁掴まり中だけ「壁と反対入力」で壁キック
            bool pushingAwayFromWall = inputX * side < -threshold;
            if (!pushingAwayFromWall)
            {
                return false;
            }
        }
        // 壁掴まり中ではないなら、左右どちら入力でも壁キック

        ExitWallGrab();

        Vector3 velocity = deps.Rb.linearVelocity;
        velocity.x = -side * deps.Settings.Wall.WallJumpHorizontalVelocity;
        velocity.y = deps.Settings.Wall.WallJumpVerticalVelocity;
        deps.Rb.linearVelocity = velocity;

        deps.RuntimeState.wallJumpControlLockTimer = deps.Settings.Wall.WallJumpControlLockTime;
        deps.RuntimeState.wallReattachLockTimer = deps.Settings.Wall.WallReattachLockTime;

        wallDetachGraceTimer = 0f;
        lastWallDetachSide = 0;

        deps.RuntimeState.isGrounded = false;
        deps.RuntimeState.isWallSliding = false;
        deps.RuntimeState.isFastFalling = false;

        deps.PlayWallKickVibration?.Invoke();
        deps.PlayWallKickSound?.Invoke();
        return true;
    }

    // 壁キックに使う壁面方向を解決する。
    private int ResolveWallKickSide()
    {
        if (deps.RuntimeState.isWallGrabbing && deps.RuntimeState.wallGrabSide != 0)
        {
            return deps.RuntimeState.wallGrabSide;
        }

        if (deps.RuntimeState.isTouchingWall && deps.RuntimeState.wallSide != 0)
        {
            return deps.RuntimeState.wallSide;
        }

        if (wallDetachGraceTimer > 0f)
        {
            return lastWallDetachSide;
        }

        return 0;
    }
}
