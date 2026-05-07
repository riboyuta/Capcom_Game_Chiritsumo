using UnityEngine;

// ジャンプ全般（通常ジャンプ、壁ジャンプ、可変ジャンプ）を管理するシステム。
internal sealed class PlayerJumpSystem
{
    private readonly PlayerLocomotionDependencies deps;

    // ============================================================
    // 内部状態（タイマー・フラグ）
    // ============================================================

    private float coyoteTimer;
    private float jumpBufferTimer;
    private float jumpHoldTimer;
    private float dashEndJumpCutLockTimer;
    private bool suppressVariableJumpCutThisTick;
    private bool justJumpedThisFrame;
    private bool justWallJumpedThisFrame;

    // ============================================================
    // デバッグ・外部公開プロパティ
    // ============================================================

    internal float CoyoteTimer => coyoteTimer;
    internal float JumpBufferTimer => jumpBufferTimer;
    internal float JumpHoldTimer => jumpHoldTimer;
    internal bool JustJumpedThisFrame => justJumpedThisFrame;
    internal bool JustWallJumpedThisFrame => justWallJumpedThisFrame;

    internal PlayerJumpSystem(PlayerLocomotionDependencies deps)
    {
        this.deps = deps;
    }

    // ============================================================
    // 初期化・リセット
    // ============================================================

    // 可変ジャンプカット抑制フラグを更新する。
    internal void SetSuppressVariableJumpCutThisTick(bool value)
    {
        suppressVariableJumpCutThisTick = value;
    }

    // 見た目用単発フラグをリセットする。
    internal void ResetOneShotFlags()
    {
        justJumpedThisFrame = false;
        justWallJumpedThisFrame = false;
    }

    // 復帰時にジャンプ内部タイマーを初期化する。
    internal void ResetRuntimeTimers()
    {
        coyoteTimer = 0f;
        jumpBufferTimer = 0f;
        jumpHoldTimer = 0f;
        dashEndJumpCutLockTimer = 0f;
        justJumpedThisFrame = false;
        justWallJumpedThisFrame = false;
        suppressVariableJumpCutThisTick = false;
    }

    // ============================================================
    // タイマー更新
    // ============================================================

    // ジャンプ補助タイマーを更新する。
    internal void UpdateJumpAssistTimers(float deltaTime)
    {
        if (deps.RuntimeState.isGrounded)
        {
            coyoteTimer = deps.Settings.Jump.UseCoyoteTime ? deps.Settings.Jump.CoyoteTime : 0f;
        }
        else
        {
            coyoteTimer = Mathf.Max(0f, coyoteTimer - deltaTime);
        }

        if (!deps.Settings.Jump.UseJumpBuffer)
        {
            jumpBufferTimer = 0f;
        }
        else
        {
            jumpBufferTimer = Mathf.Max(0f, jumpBufferTimer - deltaTime);
        }

        jumpHoldTimer = Mathf.Max(0f, jumpHoldTimer - deltaTime);
        dashEndJumpCutLockTimer = Mathf.Max(0f, dashEndJumpCutLockTimer - deltaTime);
    }

    // ============================================================
    // ジャンプ判定補助
    // ============================================================

    // ジャンプ頂点付近かを判定する。
    internal bool IsNearJumpApex(float velocityY)
    {
        return Mathf.Abs(velocityY) <= deps.Settings.Jump.ApexThreshold;
    }

    // ============================================================
    // ジャンプ処理
    // ============================================================

    // ジャンプ入力を処理して速度へ反映する。
    internal void ApplyJump(System.Func<bool> tryApplyWallKick)
    {
        if (!deps.CanAcceptJumpInput())
        {
            deps.FrameRequests.jumpRequested = false;
            if (deps.Settings.Jump.UseJumpBuffer)
            {
                jumpBufferTimer = 0f;
            }

            return;
        }

        if (deps.RuntimeState.isDashing)
        {
            deps.FrameRequests.jumpRequested = false;
            if (deps.Settings.Jump.UseJumpBuffer)
            {
                jumpBufferTimer = 0f;
            }

            return;
        }

        bool requested = deps.FrameRequests.jumpRequested;
        deps.FrameRequests.jumpRequested = false;

        if (requested && deps.Settings.Jump.UseJumpBuffer)
        {
            jumpBufferTimer = deps.Settings.Jump.JumpBufferTime;
        }

        bool hasJumpRequest = deps.Settings.Jump.UseJumpBuffer ? jumpBufferTimer > 0f : requested;
        if (!hasJumpRequest)
        {
            return;
        }

        // ジャンプ種類の優先順位:
        // 1. 壁掴まり中の真上ジャンプ
        // 2. 壁キック
        // 3. 通常ジャンプ（コヨーテタイム含む）

        // 壁掴まり中の真上ジャンプを先に判定
        if (TryApplyWallGrabVerticalJump())
        {
            jumpBufferTimer = 0f;
            return;
        }

        // その次に壁キック（WallActionSystemから提供される）
        if (tryApplyWallKick())
        {
            jumpBufferTimer = 0f;
            SetWallJumpTimers();
            justWallJumpedThisFrame = true;
            return;
        }

        bool canJump = deps.Settings.Jump.UseCoyoteTime ? coyoteTimer > 0f : deps.RuntimeState.isGrounded;
        if (!canJump)
        {
            return;
        }

        Vector3 velocity = deps.Rb.linearVelocity;
        velocity.y = deps.Settings.Jump.JumpVelocity;
        deps.Rb.linearVelocity = velocity;

        deps.RuntimeState.isGrounded = false;
        coyoteTimer = 0f;
        jumpBufferTimer = 0f;
        jumpHoldTimer = deps.Settings.Jump.MaxJumpHoldTime;
        justJumpedThisFrame = true;
        deps.PlayJumpSound?.Invoke();
    }

    // 壁掴まり中の真上ジャンプを試みる。
    private bool TryApplyWallGrabVerticalJump()
    {
        if (!deps.RuntimeState.isWallGrabbing)
        {
            return false;
        }

        if (deps.RuntimeState.isDashing)
        {
            return false;
        }

        int side = deps.RuntimeState.wallGrabSide != 0 ? deps.RuntimeState.wallGrabSide : deps.RuntimeState.wallSide;
        if (side == 0)
        {
            return false;
        }

        float inputX = Mathf.Clamp(deps.InputReader.Move.x, -1f, 1f);
        float threshold = deps.Settings.Detection.WallInputThreshold;

        bool pushingAwayFromWall = inputX * side < -threshold;
        if (pushingAwayFromWall)
        {
            return false;
        }

        // 壁掴まり状態を解除
        deps.RuntimeState.isWallGrabbing = false;
        deps.RuntimeState.wallGrabSide = 0;
        deps.Rb.useGravity = true;

        deps.RuntimeState.wallGrabRemainingTime = Mathf.Max(
            0f,
            deps.RuntimeState.wallGrabRemainingTime - deps.Settings.Wall.WallGrabJumpCost);

        Vector3 velocity = deps.Rb.linearVelocity;
        velocity.x = 0f;
        velocity.y = deps.Settings.Wall.WallGrabJumpVerticalVelocity;
        deps.Rb.linearVelocity = velocity;

        deps.RuntimeState.isGrounded = false;
        deps.RuntimeState.isWallSliding = false;
        deps.RuntimeState.wallJumpControlLockTimer = deps.Settings.Wall.WallGrabJumpHorizontalLockTime;
        deps.RuntimeState.wallReattachLockTimer = deps.Settings.Wall.WallGrabJumpReattachLockTime;

        coyoteTimer = 0f;
        jumpBufferTimer = 0f;
        jumpHoldTimer = deps.Settings.Jump.MaxJumpHoldTime;
        justJumpedThisFrame = true;

        deps.PlayJumpSound?.Invoke();
        return true;
    }

    // 壁ジャンプ用のタイマー設定（WallActionSystem から呼ばれる）
    internal void SetWallJumpTimers()
    {
        coyoteTimer = 0f;
        jumpBufferTimer = 0f;
        jumpHoldTimer = deps.Settings.Jump.MaxJumpHoldTime;
    }

    // 可変ジャンプの早離しカットを適用する。
    internal void ApplyVariableJumpCut()
    {
        if (!deps.Settings.Jump.UseVariableJump)
        {
            return;
        }

        if (suppressVariableJumpCutThisTick)
        {
            return;
        }

        if (dashEndJumpCutLockTimer > 0f)
        {
            return;
        }

        if (deps.InputReader.JumpHeld)
        {
            return;
        }

        Vector3 velocity = deps.Rb.linearVelocity;
        if (velocity.y <= 0f)
        {
            return;
        }

        float cutVelocityY = deps.Settings.Jump.JumpVelocity * deps.Settings.Jump.JumpCutMultiplier;
        velocity.y = Mathf.Min(velocity.y, cutVelocityY);
        deps.Rb.linearVelocity = velocity;
    }

    // ダッシュ終了時のジャンプカットロックタイマーを設定する。
    internal void SetDashEndJumpCutLockTimer()
    {
        dashEndJumpCutLockTimer = deps.Settings.Dash.DashEndJumpCutLockTime;
    }
}
