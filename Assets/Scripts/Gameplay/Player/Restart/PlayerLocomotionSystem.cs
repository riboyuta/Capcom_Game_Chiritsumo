using System;
using Game.Input;
using UnityEngine;

// PlayerController 内部専用の通常移動システム。
internal sealed class PlayerLocomotionSystem
{
    // 継続的なプレイヤー実行時状態。
    private readonly PlayerRuntimeState runtimeState;

    // 1フレーム/1Tick 限定の要求状態。
    private readonly PlayerFrameRequests frameRequests;

    // 移動設定パラメータ。
    private readonly PlayerMovementSettings movementSettings;

    // 物理挙動を適用する Rigidbody。
    private readonly Rigidbody rb;

    // 接地スナップなどで使うカプセル。
    private readonly CapsuleCollider capsuleCollider;

    // ローカル姿勢・スケール参照用 Transform。
    private readonly Transform transform;

    // 入力読み取りコンポーネント。
    private readonly PlayerInputReader playerInputReader;

    // 移動入力受付可否の判定デリゲート。
    private readonly Func<bool> canAcceptMoveInput;

    // ジャンプ入力受付可否の判定デリゲート。
    private readonly Func<bool> canAcceptJumpInput;

    // ダッシュ入力受付可否の判定デリゲート。
    private readonly Func<bool> canAcceptDashInput;

    // 掴まり入力受付可否の判定デリゲート。
    private readonly Func<bool> canAcceptGrabInput;

    // 行動不能状態判定デリゲート。
    private readonly Func<bool> isActionLocked;

    // 外部制御中判定デリゲート。
    private readonly Func<bool> isExternallyControlled;


    // カプセル半径のワールド値取得デリゲート。
    private readonly Func<float> getWorldCapsuleRadius;

    // ジャンプSE 再生要求デリゲート。
    private readonly Action playJumpSound;

    // 壁キックSE 再生要求デリゲート。
    private readonly Action playWallKickSound;

    // 壁キック振動要求デリゲート。
    private readonly Action playWallKickVibration;

    // コヨーテタイマー。
    private float coyoteTimer;

    // ジャンプバッファタイマー。
    private float jumpBufferTimer;

    // ジャンプ長押し維持タイマー。
    private float jumpHoldTimer;

    // ダッシュバッファタイマー。
    private float dashBufferTimer;

    // 可変ジャンプカット抑制フラグ。
    private bool suppressVariableJumpCutThisTick;

    // 今Tickで解決済みの移動補正。
    private PlayerLocomotionModifierRequest resolvedLocomotionModifier = PlayerLocomotionModifierRequest.Identity;

    // この物理フレームで通常ジャンプしたか。
    private bool justJumpedThisFrame;

    // この物理フレームで壁ジャンプしたか。
    private bool justWallJumpedThisFrame;

    // デバッグ表示用のコヨーテタイマー。
    internal float CoyoteTimer => coyoteTimer;

    // デバッグ表示用のジャンプバッファタイマー。
    internal float JumpBufferTimer => jumpBufferTimer;

    // デバッグ表示用のジャンプホールドタイマー。
    internal float JumpHoldTimer => jumpHoldTimer;

    // デバッグ表示用のダッシュバッファタイマー。
    internal float DashBufferTimer => dashBufferTimer;

    // 見た目更新用の通常ジャンプ単発フラグ。
    internal bool JustJumpedThisFrame => justJumpedThisFrame;

    // 見た目更新用の壁ジャンプ単発フラグ。
    internal bool JustWallJumpedThisFrame => justWallJumpedThisFrame;

    // LocomotionSystem の依存を受け取るコンストラクタ。
    internal PlayerLocomotionSystem(
        PlayerRuntimeState runtimeState,
        PlayerFrameRequests frameRequests,
        PlayerMovementSettings movementSettings,
        Rigidbody rb,
        CapsuleCollider capsuleCollider,
        Transform transform,
        PlayerInputReader playerInputReader,
        Func<bool> canAcceptMoveInput,
        Func<bool> canAcceptJumpInput,
        Func<bool> canAcceptDashInput,
        Func<bool> canAcceptGrabInput,
        Func<bool> isActionLocked,
        Func<bool> isExternallyControlled,
        Func<float> getWorldCapsuleRadius,
        Action playJumpSound,
        Action playWallKickSound,
        Action playWallKickVibration)
    {
        // 受け取った依存をそのまま保持する。
        this.runtimeState = runtimeState;
        this.frameRequests = frameRequests;
        this.movementSettings = movementSettings;
        this.rb = rb;
        this.capsuleCollider = capsuleCollider;
        this.transform = transform;
        this.playerInputReader = playerInputReader;
        this.canAcceptMoveInput = canAcceptMoveInput;
        this.canAcceptJumpInput = canAcceptJumpInput;
        this.canAcceptDashInput = canAcceptDashInput;
        this.canAcceptGrabInput = canAcceptGrabInput;
        this.isActionLocked = isActionLocked;
        this.isExternallyControlled = isExternallyControlled;
        this.getWorldCapsuleRadius = getWorldCapsuleRadius;
        this.playJumpSound = playJumpSound;
        this.playWallKickSound = playWallKickSound;
        this.playWallKickVibration = playWallKickVibration;
    }

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

    // 復帰時に Locomotion 内部タイマーを初期化する。
    internal void ResetRuntimeTimers()
    {
        coyoteTimer = 0f;
        jumpBufferTimer = 0f;
        jumpHoldTimer = 0f;
        dashBufferTimer = 0f;
        justJumpedThisFrame = false;
        justWallJumpedThisFrame = false;
        suppressVariableJumpCutThisTick = false;
        resolvedLocomotionModifier = PlayerLocomotionModifierRequest.Identity;
        runtimeState.wallGrabRemainingTime = movementSettings.Wall.WallGrabMaxHoldTime;
    }

    // 物理Tick用の移動補正を解決する。
    internal void ResolveLocomotionModifiersThisTick()
    {
        resolvedLocomotionModifier = frameRequests.requestedLocomotionModifierThisTick;
        frameRequests.requestedLocomotionModifierThisTick = PlayerLocomotionModifierRequest.Identity;
    }

    // ジャンプ補助タイマーを更新する。
    internal void UpdateJumpAssistTimers(float deltaTime)
    {
        if (runtimeState.isGrounded)
        {
            coyoteTimer = movementSettings.Jump.UseCoyoteTime ? movementSettings.Jump.CoyoteTime : 0f;
        }
        else
        {
            coyoteTimer = Mathf.Max(0f, coyoteTimer - deltaTime);
        }

        if (!movementSettings.Jump.UseJumpBuffer)
        {
            jumpBufferTimer = 0f;
        }
        else
        {
            jumpBufferTimer = Mathf.Max(0f, jumpBufferTimer - deltaTime);
        }

        jumpHoldTimer = Mathf.Max(0f, jumpHoldTimer - deltaTime);
    }

    internal void UpdateWallGrabLimitTimer(float deltaTime)
    {
        if (runtimeState.isGrounded)
        {
            runtimeState.wallGrabRemainingTime = movementSettings.Wall.WallGrabMaxHoldTime;
            return;
        }

        if (!runtimeState.isWallGrabbing)
        {
            return;
        }

        float inputY = Mathf.Clamp(playerInputReader.Move.y, -1f, 1f);
        float threshold = movementSettings.Wall.WallClimbInputThreshold;

        bool isClimbing =
            inputY > threshold ||
            inputY < -threshold;

        float drainPerSecond = isClimbing
            ? movementSettings.Wall.WallGrabClimbDrainPerSecond
            : movementSettings.Wall.WallGrabIdleDrainPerSecond;

        runtimeState.wallGrabRemainingTime = Mathf.Max(
            0f,
            runtimeState.wallGrabRemainingTime - drainPerSecond * deltaTime);
    }

    // 移動入力から向きを更新する。
    internal void UpdateFacingFromMoveInput()
    {
        if (isActionLocked())
        {
            return;
        }

        if (runtimeState.isDashing || runtimeState.isWallGrabbing)
        {
            return;
        }

        if (!canAcceptMoveInput())
        {
            return;
        }

        float inputX = Mathf.Clamp(playerInputReader.Move.x, -1f, 1f);
        const float facingThreshold = 0.1f;

        if (inputX > facingThreshold)
        {
            runtimeState.facing = 1;
        }
        else if (inputX < -facingThreshold)
        {
            runtimeState.facing = -1;
        }
    }
    // ジャンプ入力を処理して速度へ反映する。
    internal void ApplyJump()
    {
        if (!canAcceptJumpInput())
        {
            frameRequests.jumpRequested = false;
            if (movementSettings.Jump.UseJumpBuffer)
            {
                jumpBufferTimer = 0f;
            }

            return;
        }

        if (runtimeState.isDashing)
        {
            frameRequests.jumpRequested = false;
            if (movementSettings.Jump.UseJumpBuffer)
            {
                jumpBufferTimer = 0f;
            }

            return;
        }

        bool requested = frameRequests.jumpRequested;
        frameRequests.jumpRequested = false;

        if (requested && movementSettings.Jump.UseJumpBuffer)
        {
            jumpBufferTimer = movementSettings.Jump.JumpBufferTime;
        }

        bool hasJumpRequest = movementSettings.Jump.UseJumpBuffer ? jumpBufferTimer > 0f : requested;
        if (!hasJumpRequest)
        {
            return;
        }

        // 壁掴まり中の真上ジャンプを先に判定
        if (TryApplyWallGrabVerticalJump())
        {
            jumpBufferTimer = 0f;
            return;
        }

        // その次に壁キック
        if (TryApplyWallKick())
        {
            jumpBufferTimer = 0f;
            return;
        }

        bool canJump = movementSettings.Jump.UseCoyoteTime ? coyoteTimer > 0f : runtimeState.isGrounded;
        if (!canJump)
        {
            return;
        }

        Vector3 velocity = rb.linearVelocity;
        velocity.y = movementSettings.Jump.JumpVelocity;
        rb.linearVelocity = velocity;

        runtimeState.isGrounded = false;
        coyoteTimer = 0f;
        jumpBufferTimer = 0f;
        jumpHoldTimer = movementSettings.Jump.MaxJumpHoldTime;
        justJumpedThisFrame = true;
        playJumpSound?.Invoke();
    }

    internal bool TryApplyWallGrabVerticalJump()
    {
        if (!runtimeState.isWallGrabbing)
        {
            return false;
        }

        if (runtimeState.isDashing)
        {
            return false;
        }

        int side = runtimeState.wallGrabSide != 0 ? runtimeState.wallGrabSide : runtimeState.wallSide;
        if (side == 0)
        {
            return false;
        }

        float inputX = Mathf.Clamp(playerInputReader.Move.x, -1f, 1f);
        float threshold = movementSettings.Detection.WallInputThreshold;

        bool pushingAwayFromWall = inputX * side < -threshold;
        if (pushingAwayFromWall)
        {
            return false;
        }

        ExitWallGrab();

        runtimeState.wallGrabRemainingTime = Mathf.Max(
            0f,
            runtimeState.wallGrabRemainingTime - movementSettings.Wall.WallGrabJumpCost);

        Vector3 velocity = rb.linearVelocity;
        velocity.x = 0f;
        velocity.y = movementSettings.Wall.WallGrabJumpVerticalVelocity;
        rb.linearVelocity = velocity;

        runtimeState.isGrounded = false;
        runtimeState.isWallSliding = false;
        runtimeState.isFastFalling = false;
        runtimeState.wallJumpControlLockTimer = movementSettings.Wall.WallGrabJumpHorizontalLockTime;
        runtimeState.wallReattachLockTimer = movementSettings.Wall.WallGrabJumpReattachLockTime;

        coyoteTimer = 0f;
        jumpBufferTimer = 0f;
        jumpHoldTimer = movementSettings.Jump.MaxJumpHoldTime;
        justJumpedThisFrame = true;

        playJumpSound?.Invoke();
        return true;
    }

    // 壁キックを開始できる場合に適用する。
    internal bool TryApplyWallKick()
    {
        if (runtimeState.isDashing)
        {
            return false;
        }

        if (!movementSettings.Wall.UseWallKick)
        {
            return false;
        }

        if (runtimeState.isGrounded || !runtimeState.isTouchingWall || runtimeState.wallSide == 0)
        {
            return false;
        }

        if (runtimeState.wallReattachLockTimer > 0f)
        {
            return false;
        }

        int side = runtimeState.isWallGrabbing
            ? runtimeState.wallGrabSide
            : runtimeState.wallSide;

        if (side == 0)
        {
            return false;
        }

        float inputX = Mathf.Clamp(playerInputReader.Move.x, -1f, 1f);
        float threshold = movementSettings.Detection.WallInputThreshold;
        bool hasHorizontalInput = Mathf.Abs(inputX) >= threshold;

        if (!hasHorizontalInput)
        {
            return false;
        }

        if (runtimeState.isWallGrabbing)
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

        Vector3 velocity = rb.linearVelocity;
        velocity.x = -side * movementSettings.Wall.WallJumpHorizontalVelocity;
        velocity.y = movementSettings.Wall.WallJumpVerticalVelocity;
        rb.linearVelocity = velocity;

        runtimeState.wallJumpControlLockTimer = movementSettings.Wall.WallJumpControlLockTime;
        runtimeState.wallReattachLockTimer = movementSettings.Wall.WallReattachLockTime;
        coyoteTimer = 0f;
        jumpBufferTimer = 0f;
        jumpHoldTimer = movementSettings.Jump.MaxJumpHoldTime;
        runtimeState.isGrounded = false;
        runtimeState.isWallSliding = false;
        runtimeState.isFastFalling = false;
        justWallJumpedThisFrame = true;

        playWallKickVibration?.Invoke();
        playWallKickSound?.Invoke();
        return true;
    }

    // 可変ジャンプの早離しカットを適用する。
    internal void ApplyVariableJumpCut()
    {
        if (!movementSettings.Jump.UseVariableJump)
        {
            return;
        }

        if (suppressVariableJumpCutThisTick)
        {
            return;
        }

        if (playerInputReader.JumpHeld)
        {
            return;
        }

        Vector3 velocity = rb.linearVelocity;
        if (velocity.y <= 0f)
        {
            return;
        }

        float cutVelocityY = movementSettings.Jump.JumpVelocity * movementSettings.Jump.JumpCutMultiplier;
        velocity.y = Mathf.Min(velocity.y, cutVelocityY);
        rb.linearVelocity = velocity;
    }

    // 壁滑り状態と落下速度上限を更新する。
    internal void ApplyWallSlide()
    {
        runtimeState.isWallSliding = false;

        if (!movementSettings.Wall.UseWallSlide)
        {
            return;
        }

        if (runtimeState.wallReattachLockTimer > 0f)
        {
            return;
        }

        if (runtimeState.isGrounded || !runtimeState.isTouchingWall || runtimeState.wallSide == 0)
        {
            return;
        }

        Vector3 velocity = rb.linearVelocity;
        if (velocity.y >= 0f)
        {
            return;
        }

        float inputX = Mathf.Clamp(playerInputReader.Move.x, -1f, 1f);
        float wallDirection = runtimeState.wallSide;
        bool pushingToWall = inputX * wallDirection >= movementSettings.Detection.WallInputThreshold;
        if (!pushingToWall)
        {
            return;
        }

        float minVelocityY = -movementSettings.Wall.WallSlideMaxSpeed;
        if (velocity.y < minVelocityY)
        {
            velocity.y = minVelocityY;
            rb.linearVelocity = velocity;
        }

        runtimeState.isWallSliding = true;
        runtimeState.isFastFalling = false;
    }

    // 急降下開始条件を満たす場合に状態を切り替える。
    internal void TryStartFastFall()
    {
        if (!canAcceptMoveInput())
        {
            return;
        }

        if (!movementSettings.Fall.UseFastFall)
        {
            return;
        }

        if (runtimeState.isGrounded || runtimeState.isDashing || runtimeState.isWallSliding)
        {
            return;
        }

        if (!playerInputReader.DownPressed)
        {
            return;
        }

        runtimeState.isFastFalling = true;
    }

    // 壁キック関連ロックタイマーを更新する。
    internal void UpdateWallJumpLockTimer(float deltaTime)
    {
        runtimeState.wallJumpControlLockTimer = Mathf.Max(0f, runtimeState.wallJumpControlLockTimer - deltaTime);
        runtimeState.wallReattachLockTimer = Mathf.Max(0f, runtimeState.wallReattachLockTimer - deltaTime);
    }

    // 通常の横移動速度を更新する。
    internal void ApplyHorizontalMovement(float deltaTime)
    {
        if (!canAcceptMoveInput())
        {
            return;
        }

        float inputX = Mathf.Clamp(playerInputReader.Move.x, -1f, 1f);
        float targetSpeed = inputX * (movementSettings.Move.MaxSpeed * resolvedLocomotionModifier.moveSpeedMultiplier);
        bool hasMoveInput = Mathf.Abs(inputX) > 0.01f;

        float accel;
        if (hasMoveInput)
        {
            bool isTurning = rb.linearVelocity.x * inputX < 0f;
            if (isTurning)
            {
                accel = runtimeState.isGrounded
                    ? movementSettings.Move.GroundTurnAcceleration * resolvedLocomotionModifier.groundAccelerationMultiplier
                    : movementSettings.Move.AirTurnAcceleration * resolvedLocomotionModifier.airAccelerationMultiplier;
            }
            else
            {
                accel = runtimeState.isGrounded
                    ? movementSettings.Move.GroundAcceleration * resolvedLocomotionModifier.groundAccelerationMultiplier
                    : movementSettings.Move.AirAcceleration * resolvedLocomotionModifier.airAccelerationMultiplier;
            }
        }
        else
        {
            accel = runtimeState.isGrounded
                ? movementSettings.Move.GroundDeceleration * resolvedLocomotionModifier.groundAccelerationMultiplier
                : movementSettings.Move.AirDeceleration * resolvedLocomotionModifier.airAccelerationMultiplier;
        }

        if (runtimeState.wallJumpControlLockTimer > 0f)
        {
            return;
        }

        Vector3 velocity = rb.linearVelocity;
        velocity.x = Mathf.MoveTowards(velocity.x, targetSpeed, accel * deltaTime);
        rb.linearVelocity = velocity;
    }

    // カスタム重力と落下上限を適用する。
    internal void ApplyCustomGravity()
    {
        Vector3 velocity = rb.linearVelocity;
        bool isFalling = velocity.y < 0f;
        bool isRising = velocity.y > 0f;

        float gravityMultiplier = movementSettings.Jump.GravityScale * resolvedLocomotionModifier.gravityScaleMultiplier;
        if (isRising && playerInputReader.JumpHeld && jumpHoldTimer > 0f)
        {
            gravityMultiplier *= movementSettings.Jump.RiseGravityMultiplier;
        }

        if (isFalling)
        {
            float fallingMultiplier = movementSettings.Fall.GravityMultiplier;
            if (runtimeState.isFastFalling)
            {
                fallingMultiplier = movementSettings.Fall.FastFallGravityMultiplier;
            }

            gravityMultiplier *= fallingMultiplier;
        }

        if (!Mathf.Approximately(gravityMultiplier, 1f))
        {
            Vector3 extraGravity = Physics.gravity * (gravityMultiplier - 1f);
            rb.AddForce(extraGravity, ForceMode.Acceleration);
        }

        float maxFallSpeed = runtimeState.isFastFalling && isFalling
            ? movementSettings.Fall.FastFallMaxSpeed
            : movementSettings.Fall.MaxSpeed;
        float minVelocityY = -maxFallSpeed;
        if (velocity.y < minVelocityY)
        {
            velocity.y = minVelocityY;
            rb.linearVelocity = velocity;
        }
    }

    // 壁捕まり状態の進入・離脱判定を更新する。
    internal void UpdateWallGrabState()
    {
        if (runtimeState.isWallGrabbing)
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
    internal bool CanEnterWallGrab()
    {
        if (!canAcceptGrabInput())
        {
            return false;
        }

        if (!movementSettings.Wall.UseWallGrab)
        {
            return false;
        }

        if (isActionLocked() || runtimeState.isGrounded || runtimeState.isDashing || isExternallyControlled())
        {
            return false;
        }

        if (!runtimeState.isTouchingWall || runtimeState.wallSide == 0)
        {
            return false;
        }

        if (runtimeState.wallReattachLockTimer > 0f)
        {
            return false;
        }

        if (!playerInputReader.GrabHeld)
        {
            return false;
        }

        if (runtimeState.wallGrabRemainingTime <= 0f)
        {
            return false;
        }

        if (!IsWithinWallGrabRange(movementSettings.Wall.WallGrabEnterDistance))
        {
            return false;
        }

        float inputY = Mathf.Clamp(playerInputReader.Move.y, -1f, 1f);
        if (inputY > movementSettings.Wall.LedgeClimbUpInputThreshold)
        {
            int side = runtimeState.wallSide;
            if (side != 0 && TryFindLedgeTop(side, out _))
            {
                return false;
            }
        }

        return true;
    }

    // 壁捕まり離脱可否を判定する。
    internal bool ShouldExitWallGrab()
    {
        if (isActionLocked())
        {
            return true;
        }

        if (!canAcceptGrabInput())
        {
            return true;
        }

        if (!playerInputReader.GrabHeld)
        {
            return true;
        }

        if (runtimeState.isGrounded || runtimeState.isDashing || isExternallyControlled())
        {
            return true;
        }

        if (!runtimeState.isTouchingWall || runtimeState.wallSide == 0)
        {
            return true;
        }

        if (runtimeState.wallReattachLockTimer > 0f)
        {
            return true;
        }

        if (runtimeState.wallGrabRemainingTime <= 0f)
        {
            return true;
        }

        if (!IsWithinWallGrabRange(movementSettings.Wall.WallGrabExitDistance))
        {
            return true;
        }

        return false;
    }

    private bool IsWithinWallGrabRange(float maxDistance)
    {
        if (!runtimeState.isTouchingWall || runtimeState.wallSide == 0)
        {
            return false;
        }

        Bounds bounds = capsuleCollider.bounds;
        Vector3 wallDir = Vector3.right * runtimeState.wallSide;

        // プレイヤーの側面ギリギリ少し内側から前方へ飛ばす
        Vector3 origin = bounds.center;
        origin.y += bounds.extents.y * 0.15f;
        origin.x += wallDir.x * (bounds.extents.x - 0.01f);

        return Physics.Raycast(
            origin,
            wallDir,
            maxDistance + 0.01f,
            movementSettings.Detection.GroundLayerMask,
            QueryTriggerInteraction.Ignore);
    }

    // 壁捕まり状態へ遷移させる。
    internal void EnterWallGrab()
    {
        runtimeState.isWallGrabbing = true;
        runtimeState.wallGrabSide = runtimeState.wallSide;
        runtimeState.isWallSliding = false;
        runtimeState.isFastFalling = false;

        rb.useGravity = false;

        Vector3 velocity = rb.linearVelocity;
        velocity.x = 0f;
        velocity.y = 0f;
        rb.linearVelocity = velocity;
    }

    // 壁捕まり状態を解除する。
    internal void ExitWallGrab()
    {
        runtimeState.isWallGrabbing = false;
        runtimeState.wallGrabSide = 0;
        rb.useGravity = true;
    }

    // 壁捕まり中の専用移動を適用する。
    internal void ApplyWallGrabMovement()
    {
        if (!runtimeState.isWallGrabbing)
        {
            return;
        }

        runtimeState.facing = runtimeState.wallGrabSide;

        float inputY = Mathf.Clamp(playerInputReader.Move.y, -1f, 1f);
        float threshold = movementSettings.Wall.WallClimbInputThreshold;

        float targetVerticalSpeed = 0f;

        if (inputY > threshold)
        {
            targetVerticalSpeed = movementSettings.Wall.WallClimbUpSpeed;
        }
        else if (inputY < -threshold)
        {
            targetVerticalSpeed = -movementSettings.Wall.WallClimbDownSpeed;
        }

        Vector3 velocity = rb.linearVelocity;
        velocity.x = 0f;
        velocity.y = targetVerticalSpeed;
        rb.linearVelocity = velocity;

        runtimeState.isWallSliding = false;
        runtimeState.isFastFalling = false;
    }

    internal bool TryStartLedgeClimb()
    { 
        if (!runtimeState.isWallGrabbing)
        {
            return false;
        }

        if (!movementSettings.Wall.UseLedgeClimb)
        {
            return false;
        }

        float inputY = Mathf.Clamp(playerInputReader.Move.y, -1f, 1f);
        if (inputY <= movementSettings.Wall.LedgeClimbUpInputThreshold)
        {
            return false;
        }

        int side = runtimeState.wallGrabSide != 0 ? runtimeState.wallGrabSide : runtimeState.wallSide;
        if (!TryFindLedgeTop(side, out RaycastHit groundHit))
        {
            return false;
        }

        BeginLedgeClimb(groundHit, side);
        return true;
    }

    private bool TryFindLedgeTop(int side, out RaycastHit groundHit)
    {
        groundHit = default;

        Bounds bounds = capsuleCollider.bounds;
        Vector3 wallDir = Vector3.right * side;
        LayerMask mask = movementSettings.Detection.GroundLayerMask;

        float forwardDistance = bounds.extents.x + movementSettings.Wall.LedgeClimbForwardCheckDistance;

        // 今より下側に置く。頂上付近から始めても壁を拾いやすくする
        Vector3 lowerProbeOrigin = bounds.center - Vector3.up * (bounds.extents.y * 0.15f);

        // 頭より少し上。ここは壁がなくなっていてほしい
        Vector3 upperProbeOrigin = new Vector3(
            bounds.center.x,
            bounds.max.y + 0.05f,
            bounds.center.z);

        bool lowerHitsWall = Physics.Raycast(
            lowerProbeOrigin,
            wallDir,
            forwardDistance,
            mask,
            QueryTriggerInteraction.Ignore);

        bool upperHitsWall = Physics.Raycast(
            upperProbeOrigin,
            wallDir,
            forwardDistance,
            mask,
            QueryTriggerInteraction.Ignore);

        // 下には壁がある / 上には壁がない
        if (!lowerHitsWall || upperHitsWall)
        {
            return false;
        }

        Vector3 topProbeOrigin =
            upperProbeOrigin
            + wallDir * forwardDistance
            + Vector3.up * 0.2f;

        if (!Physics.Raycast(
            topProbeOrigin,
            Vector3.down,
            out groundHit,
            movementSettings.Wall.LedgeClimbTopDownCheckDistance,
            mask,
            QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        // 自分より十分上の床だけを ledge とみなす
        if (groundHit.point.y <= bounds.center.y)
        {
            return false;
        }

        return true;
    }

    internal bool TryStartLedgeClimbFromWallContact()
    {
        if (runtimeState.isLedgeClimbing)
        {
            return false;
        }

        if (!movementSettings.Wall.UseLedgeClimb)
        {
            return false;
        }

        if (runtimeState.isGrounded || runtimeState.isDashing || isExternallyControlled())
        {
            return false;
        }

        if (!playerInputReader.GrabHeld)
        {
            return false;
        }

        float inputY = Mathf.Clamp(playerInputReader.Move.y, -1f, 1f);
        if (inputY <= movementSettings.Wall.LedgeClimbUpInputThreshold)
        {
            return false;
        }

        int side = runtimeState.isWallGrabbing ? runtimeState.wallGrabSide : runtimeState.wallSide;
        if (side == 0)
        {
            return false;
        }

        if (!TryFindLedgeTop(side, out RaycastHit groundHit))
        {
            return false;
        }

        BeginLedgeClimb(groundHit, side);
        return true;
    }

    private void BeginLedgeClimb(in RaycastHit groundHit, int side)
    {
        Bounds bounds = capsuleCollider.bounds;
        Vector3 wallDir = Vector3.right * side;

        Vector3 start = rb.position;

        Vector3 end = groundHit.point;
        end.y += bounds.extents.y + 0.02f;
        end.x += wallDir.x * movementSettings.Wall.LedgeClimbInnerInset;
        end.z = rb.position.z;

        Vector3 control = start;
        control.y = Mathf.Max(start.y, end.y) + movementSettings.Wall.LedgeClimbLiftHeight;
        control.x += wallDir.x * movementSettings.Wall.LedgeClimbControlForwardInset;
        control.z = rb.position.z;

        runtimeState.isLedgeClimbing = true;
        runtimeState.ledgeClimbStart = start;
        runtimeState.ledgeClimbControl = control;
        runtimeState.ledgeClimbEnd = end;
        runtimeState.ledgeClimbTimer = 0f;
        runtimeState.ledgeClimbDuration = movementSettings.Wall.LedgeClimbDuration;
        runtimeState.ledgeClimbSide = side;

        ExitWallGrab();

        rb.useGravity = false;
        rb.linearVelocity = Vector3.zero;

        runtimeState.isFastFalling = false;
        runtimeState.isWallSliding = false;
    }

    internal void ApplyLedgeClimbMovement(float deltaTime)
    {
        if (!runtimeState.isLedgeClimbing)
        {
            return;
        }

        runtimeState.ledgeClimbTimer += deltaTime;

        float duration = Mathf.Max(0.01f, runtimeState.ledgeClimbDuration);
        float t = Mathf.Clamp01(runtimeState.ledgeClimbTimer / duration);
        float easedT = EaseOutQuad(t);

        Vector3 nextPosition = EvaluateQuadraticBezier(
            runtimeState.ledgeClimbStart,
            runtimeState.ledgeClimbControl,
            runtimeState.ledgeClimbEnd,
            easedT);

        rb.position = nextPosition;
        rb.linearVelocity = Vector3.zero;
        runtimeState.facing = runtimeState.ledgeClimbSide;

        if (t >= 0.75f)
        {
            EndLedgeClimb();
        }
    }

    private void EndLedgeClimb()
    {
        rb.position = runtimeState.ledgeClimbEnd;
        rb.linearVelocity = Vector3.zero;
        rb.useGravity = true;

        runtimeState.isLedgeClimbing = false;
        runtimeState.isGrounded = true;
        runtimeState.isWallSliding = false;
        runtimeState.isFastFalling = false;
    }

    private static Vector3 EvaluateQuadraticBezier(Vector3 start, Vector3 control, Vector3 end, float t)
    {
        float u = 1f - t;
        return u * u * start
            + 2f * u * t * control
            + t * t * end;
    }

    private static float EaseOutQuad(float t)
    {
        return 1f - (1f - t) * (1f - t);
    }

    // ダッシュリソース回復状態を更新する。
    internal void UpdateDashResourceState()
    {
        HandleGroundDashRefill();
    }

    // 接地時ダッシュ回復を処理する。
    internal void HandleGroundDashRefill()
    {
        if (!movementSettings.Dash.UseGroundRefill)
        {
            return;
        }

        if (!runtimeState.isGrounded)
        {
            return;
        }

        if (isExternallyControlled() || runtimeState.isWallGrabbing)
        {
            return;
        }

        if (runtimeState.currentDashCharges < Mathf.Max(1, movementSettings.Dash.MaxCharges))
        {
            TryRefillDash(DashRefillReason.Grounded);
        }
    }

    // ダッシュ継続タイマーを更新する。
    internal void UpdateDashTimers(float deltaTime)
    {
        if (!runtimeState.isDashing)
        {
            runtimeState.dashTimer = 0f;
            return;
        }

        runtimeState.dashTimer = Mathf.Max(0f, runtimeState.dashTimer - deltaTime);
        if (runtimeState.dashTimer <= 0f)
        {
            EndDash();
        }
    }

    // ダッシュ消費可否を判定する。
    internal bool CanConsumeDash()
    {
        if (!movementSettings.Dash.UseDash)
        {
            return false;
        }

        if (runtimeState.currentDashCharges <= 0)
        {
            return false;
        }

        if (runtimeState.isDashing)
        {
            return false;
        }

        if (isExternallyControlled())
        {
            return false;
        }

        return true;
    }

    // ダッシュ開始可否を判定する。
    internal bool CanStartDash()
    {
        if (!CanConsumeDash())
        {
            return false;
        }

        if (runtimeState.isGrounded && runtimeState.groundDashCooldownTimer > 0f)
        {
            return false;
        }

        return true;
    }

    // 現在ダッシュを使用可能かを判定する。
    internal bool CanUseDashNowInternal()
    {
        if (!canAcceptDashInput())
        {
            return false;
        }

        return CanStartDash();
    }

    // ダッシュ残数消費を試みる。
    internal bool TryConsumeDash()
    {
        if (!CanConsumeDash())
        {
            return false;
        }

        runtimeState.currentDashCharges = Mathf.Max(0, runtimeState.currentDashCharges - 1);
        return true;
    }

    // 指定理由でダッシュ回復可能か判定する。
    internal bool CanRefillDash(DashRefillReason reason)
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

    // 指定理由でダッシュ回復を試みる。
    internal bool TryRefillDash(DashRefillReason reason)
    {
        if (!CanRefillDash(reason))
        {
            return false;
        }

        int maxCharges = Mathf.Max(1, movementSettings.Dash.MaxCharges);
        if (runtimeState.currentDashCharges >= maxCharges)
        {
            return false;
        }

        runtimeState.currentDashCharges = maxCharges;
        return true;
    }

    // ダッシュ終了処理を実行する。
    internal void EndDash()
    {
        runtimeState.isDashing = false;
        TrySnapToGroundAfterDash();

        if (!movementSettings.Dash.RestoreStartVerticalVelocity)
        {
            return;
        }

        Vector3 velocity = rb.linearVelocity;
        velocity.y = runtimeState.dashStartVerticalVelocity;
        rb.linearVelocity = velocity;
    }

    // ダッシュ入力バッファタイマーを更新する。
    internal void UpdateDashBufferTimer(float deltaTime)
    {
        if (!movementSettings.Dash.UseDashBuffer)
        {
            dashBufferTimer = 0f;
            return;
        }

        dashBufferTimer = Mathf.Max(0f, dashBufferTimer - deltaTime);
    }

    // ダッシュ開始を試みる。
    internal void TryStartDash()
    {
        if (!canAcceptDashInput())
        {
            frameRequests.dashRequested = false;
            dashBufferTimer = 0f;
            return;
        }

        if (!movementSettings.Dash.UseDash)
        {
            frameRequests.dashRequested = false;
            dashBufferTimer = 0f;
            return;
        }

        bool immediateDashRequest = frameRequests.dashRequested;
        if (immediateDashRequest)
        {
            if (movementSettings.Dash.UseDashBuffer)
            {
                dashBufferTimer = movementSettings.Dash.DashBufferTime;
            }

            frameRequests.dashRequested = false;
        }

        bool hasBufferedDash = movementSettings.Dash.UseDashBuffer && dashBufferTimer > 0f;
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

        ExitWallGrab();
        runtimeState.isDashing = true;
        runtimeState.isFastFalling = false;
        runtimeState.dashTimer = movementSettings.Dash.Duration;
        if (runtimeState.isGrounded)
        {
            runtimeState.groundDashCooldownTimer = movementSettings.Dash.GroundCooldownTime;
        }

        runtimeState.dashStartVerticalVelocity = rb.linearVelocity.y;
        Vector2 dashStartVelocity = rb.linearVelocity;
        dashStartVelocity.y = 0f;
        rb.linearVelocity = dashStartVelocity;

        runtimeState.dashDirection = ResolveDashStartDirection();
        if (runtimeState.dashDirection.x > 0f)
        {
            runtimeState.facing = 1;
        }
        else if (runtimeState.dashDirection.x < 0f)
        {
            runtimeState.facing = -1;
        }

        runtimeState.isWallSliding = false;
        frameRequests.dashRequested = false;
        dashBufferTimer = 0f;
        frameRequests.jumpRequested = false;
        if (movementSettings.Jump.UseJumpBuffer)
        {
            jumpBufferTimer = 0f;
        }
    }

    // 地上ダッシュ連続制限タイマーを更新する。
    internal void UpdateGroundDashCooldownTimer(float deltaTime)
    {
        runtimeState.groundDashCooldownTimer = Mathf.Max(0f, runtimeState.groundDashCooldownTimer - deltaTime);
    }

    // 着地時に地上ダッシュ連続制限を解除する。
    internal void HandleGroundDashCooldownOnLanding()
    {
        if (!runtimeState.wasGroundedLastFrame && runtimeState.isGrounded)
        {
            runtimeState.groundDashCooldownTimer = 0f;
        }
    }

    // ダッシュ終了後の接地スナップ可否を判定する。
    internal bool CanSnapToGroundAfterDash()
    {
        if (!movementSettings.Dash.UseGroundSnap)
        {
            return false;
        }

        if (runtimeState.isGrounded || runtimeState.isWallGrabbing || isExternallyControlled())
        {
            return false;
        }

        if (runtimeState.dashDirection.y > 0f)
        {
            return false;
        }

        return TryGetDashGroundSnapTarget(out _);
    }

    // ダッシュ終了後の接地スナップを試みる。
    internal void TrySnapToGroundAfterDash()
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

        if (snapDeltaY < 0f || snapDeltaY > movementSettings.Dash.GroundSnapDistance)
        {
            return;
        }

        rb.position = new Vector3(position.x, targetPositionY, position.z);
        runtimeState.isGrounded = true;
    }

    // ダッシュ接地スナップ先を取得する。
    internal bool TryGetDashGroundSnapTarget(out RaycastHit hit)
    {
        Vector3 up = transform.up;
        Vector3 worldCenter = transform.TransformPoint(capsuleCollider.center);
        float worldRadius = getWorldCapsuleRadius();
        float halfHeight = Mathf.Max(capsuleCollider.height * 0.5f, capsuleCollider.radius);
        float worldHalfHeight = halfHeight * Mathf.Abs(transform.lossyScale.y);
        Vector3 bottomSphereCenter = worldCenter - up * (worldHalfHeight - worldRadius);
        float castDistance = movementSettings.Dash.GroundSnapDistance + 0.01f;

        return Physics.SphereCast(
            bottomSphereCenter,
            worldRadius * 0.95f,
            -up,
            out hit,
            castDistance,
            movementSettings.Detection.GroundLayerMask,
            QueryTriggerInteraction.Ignore);
    }

    // ダッシュ開始方向を解決する。
    internal Vector2 ResolveDashStartDirection()
    {
        Vector2 requestedDirection = playerInputReader.DashDirectionInput;
        if (requestedDirection == Vector2.zero)
        {
            return new Vector2(runtimeState.facing, 0f);
        }

        return requestedDirection.normalized;
    }

    // ダッシュ中の専用速度を適用する。
    internal void ApplyDashVelocity()
    {
        rb.linearVelocity = runtimeState.dashDirection * (movementSettings.Dash.Speed * resolvedLocomotionModifier.dashSpeedMultiplier);
    }
}