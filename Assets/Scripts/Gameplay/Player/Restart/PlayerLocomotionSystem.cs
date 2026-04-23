using System;
using Game.Input;
using UnityEngine;

// PlayerController 内部専用の通常移動システム。
internal sealed class PlayerLocomotionSystem
{
    // ============================================================
    // 依存注入されるオブジェクト
    // ============================================================

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

    // ============================================================
    // 入力・状態判定デリゲート
    // ============================================================

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

    // ============================================================
    // 音響・演出デリゲート
    // ============================================================

    // ジャンプSE 再生要求デリゲート。
    private readonly Action playJumpSound;

    // 壁キックSE 再生要求デリゲート。
    private readonly Action playWallKickSound;

    // 壁キック振動要求デリゲート。
    private readonly Action playWallKickVibration;

    // ============================================================
    // 内部状態（タイマー・フラグ・補正情報）
    // ============================================================

    // コヨーテタイマー。
    private float coyoteTimer;

    // ジャンプバッファタイマー。
    private float jumpBufferTimer;

    // ジャンプ長押し維持タイマー。
    private float jumpHoldTimer;

    // ダッシュバッファタイマー。
    private float dashBufferTimer;

    // ダッシュ終了直後のジャンプカット無効タイマー。
    private float dashEndJumpCutLockTimer;

    // 着地直後だけ横制御を強める補正タイマー。
    private float landingControlAssistTimer;

    // 壁から離れた直後でも壁キックを許可する猶予タイマー。
    private float wallDetachGraceTimer;

    // 壁離脱猶予中に参照する最後の壁面方向。
    private int lastWallDetachSide;

    // 可変ジャンプカット抑制フラグ。
    private bool suppressVariableJumpCutThisTick;

    // 今Tickで解決済みの移動補正。
    private PlayerLocomotionModifierRequest resolvedLocomotionModifier = PlayerLocomotionModifierRequest.Identity;

    // この物理フレームで通常ジャンプしたか。
    private bool justJumpedThisFrame;

    // この物理フレームで壁ジャンプしたか。
    private bool justWallJumpedThisFrame;

    // ============================================================
    // デバッグ・外部公開プロパティ
    // ============================================================

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

    // 復帰時に Locomotion 内部タイマーを初期化する。
    internal void ResetRuntimeTimers()
    {
        coyoteTimer = 0f;
        jumpBufferTimer = 0f;
        jumpHoldTimer = 0f;
        dashBufferTimer = 0f;
        dashEndJumpCutLockTimer = 0f;
        landingControlAssistTimer = 0f;
        wallDetachGraceTimer = 0f;
        lastWallDetachSide = 0;
        justJumpedThisFrame = false;
        justWallJumpedThisFrame = false;
        suppressVariableJumpCutThisTick = false;
        resolvedLocomotionModifier = PlayerLocomotionModifierRequest.Identity;
        runtimeState.wallGrabRemainingTime = movementSettings.Wall.WallGrabMaxHoldTime;
        runtimeState.isLedgeClimbing = false;
        runtimeState.ledgeClimbStartTime = 0f;
    }

    // 物理Tick用の移動補正を解決する。
    internal void ResolveLocomotionModifiersThisTick()
    {
        resolvedLocomotionModifier = frameRequests.requestedLocomotionModifierThisTick;
        frameRequests.requestedLocomotionModifierThisTick = PlayerLocomotionModifierRequest.Identity;
    }

    // ============================================================
    // タイマー更新
    // ============================================================

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
        dashEndJumpCutLockTimer = Mathf.Max(0f, dashEndJumpCutLockTimer - deltaTime);
        landingControlAssistTimer = Mathf.Max(0f, landingControlAssistTimer - deltaTime);
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

    // ============================================================
    // ジャンプ処理
    // ============================================================

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

    // 壁キックに使う壁面方向を解決する。
    private int ResolveWallKickSide()
    {
        if (runtimeState.isWallGrabbing && runtimeState.wallGrabSide != 0)
        {
            return runtimeState.wallGrabSide;
        }

        if (runtimeState.isTouchingWall && runtimeState.wallSide != 0)
        {
            return runtimeState.wallSide;
        }

        if (wallDetachGraceTimer > 0f)
        {
            return lastWallDetachSide;
        }

        return 0;
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

        if (runtimeState.isGrounded)
        {
            return false;
        }

        if (runtimeState.wallReattachLockTimer > 0f)
        {
            return false;
        }

        int side = ResolveWallKickSide();
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

        wallDetachGraceTimer = 0f;
        lastWallDetachSide = 0;

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

        if (dashEndJumpCutLockTimer > 0f)
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

    // ============================================================
    // 壁アクション（壁滑り・壁掴まり・壁キック）
    // ============================================================

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

        if (runtimeState.isGrounded)
        {
            wallDetachGraceTimer = 0f;
            lastWallDetachSide = 0;
            return;
        }

        if (runtimeState.isTouchingWall && runtimeState.wallSide != 0)
        {
            wallDetachGraceTimer = movementSettings.Wall.WallDetachGraceTime;
            lastWallDetachSide = runtimeState.wallSide;
            return;
        }

        wallDetachGraceTimer = Mathf.Max(0f, wallDetachGraceTimer - deltaTime);
        if (wallDetachGraceTimer <= 0f)
        {
            lastWallDetachSide = 0;
        }
    }

    // ジャンプ頂点付近かを判定する。
    private bool IsNearJumpApex(float velocityY)
    {
        return Mathf.Abs(velocityY) <= movementSettings.Jump.ApexThreshold;
    }

    // ============================================================
    // 角補正（ジャンプ時・ダッシュ時）
    // ============================================================

    // 角補正の試行方向を解決する。
    private int ResolveCornerCorrectionDirection()
    {
        float inputX = Mathf.Clamp(playerInputReader.Move.x, -1f, 1f);
        if (inputX > 0.01f)
        {
            return 1;
        }

        if (inputX < -0.01f)
        {
            return -1;
        }

        float velocityX = rb.linearVelocity.x;
        if (velocityX > 0.01f)
        {
            return 1;
        }

        if (velocityX < -0.01f)
        {
            return -1;
        }

        return 0;
    }

    // 指定位置で頭の指定側が天井に当たっているか確認する。
    // sideSign:
    // -1 = 左角
    //  0 = 頭中央
    //  1 = 右角
    private bool IsHeadBlockedAtPosition(Vector3 targetPosition, int sideSign)
    {
        Bounds bounds = capsuleCollider.bounds;
        Vector3 center = bounds.center + (targetPosition - rb.position);
        Vector3 up = transform.up;

        Vector3 origin = center;
        origin += up * (bounds.extents.y - 0.02f);
        origin += Vector3.right * sideSign * (bounds.extents.x - 0.01f);

        return Physics.Raycast(
            origin,
            up,
            movementSettings.InputAssist.CornerCorrectionUpCheckDistance,
            movementSettings.Detection.GroundLayerMask,
            QueryTriggerInteraction.Ignore);
    }

    // 指定位置にプレイヤーのカプセルを置けるか確認する。
    private bool IsCapsuleFreeAtPosition(Vector3 targetPosition)
    {
        GetCapsuleWorldPointsAtPosition(
            targetPosition,
            out Vector3 topPoint,
            out Vector3 bottomPoint,
            out float worldRadius);

        return !Physics.CheckCapsule(
            topPoint,
            bottomPoint,
            worldRadius * 0.98f,
            movementSettings.Detection.GroundLayerMask,
            QueryTriggerInteraction.Ignore);
    }

    // ジャンプ角補正用の横オフセットを探す。
    // Celeste っぽく、一発テレポートではなく小刻みに横へ試す。
    private bool TryFindJumpCornerCorrectionOffset(int preferredDirection, out float resolvedOffsetX)
    {
        resolvedOffsetX = 0f;

        int firstDirection = preferredDirection != 0 ? preferredDirection : 1;
        int secondDirection = -firstDirection;

        return TryFindJumpCornerCorrectionOffsetInDirection(firstDirection, out resolvedOffsetX)
            || TryFindJumpCornerCorrectionOffsetInDirection(secondDirection, out resolvedOffsetX);
    }

    private bool TryFindJumpCornerCorrectionOffsetInDirection(int direction, out float resolvedOffsetX)
    {
        resolvedOffsetX = 0f;

        // 平天井に頭中央が当たっているなら角補正しない。
        if (IsHeadBlockedAtPosition(rb.position, 0))
        {
            return false;
        }

        // その方向の角が当たっていないなら、この方向には補正しない。
        if (!IsHeadBlockedAtPosition(rb.position, direction))
        {
            return false;
        }

        int probeCount = movementSettings.InputAssist.CornerCorrectionProbeCount;
        float maxDistance = movementSettings.InputAssist.CornerCorrectionDistance;

        for (int probeIndex = 1; probeIndex <= probeCount; probeIndex++)
        {
            float t = probeIndex / (float)probeCount;
            float offsetX = maxDistance * t * direction;

            Vector3 candidatePosition = rb.position + Vector3.right * offsetX;

            // 横へずらした後も頭中央がまだ詰まるなら不採用。
            if (IsHeadBlockedAtPosition(candidatePosition, 0))
            {
                continue;
            }

            if (!IsCapsuleFreeAtPosition(candidatePosition))
            {
                continue;
            }

            resolvedOffsetX = offsetX;
            return true;
        }

        return false;
    }

    // 指定位置にカプセルを置いたときのワールド座標を求める。
    private void GetCapsuleWorldPointsAtPosition(
        Vector3 targetPosition,
        out Vector3 topPoint,
        out Vector3 bottomPoint,
        out float worldRadius)
    {
        Vector3 worldCenter = transform.TransformPoint(capsuleCollider.center);
        Vector3 positionDelta = targetPosition - rb.position;
        worldCenter += positionDelta;

        Vector3 up = transform.up;
        worldRadius = getWorldCapsuleRadius();

        float halfHeight = Mathf.Max(capsuleCollider.height * 0.5f, capsuleCollider.radius);
        float worldHalfHeight = halfHeight * Mathf.Abs(transform.lossyScale.y);
        float sphereOffset = Mathf.Max(0f, worldHalfHeight - worldRadius);

        topPoint = worldCenter + up * sphereOffset;
        bottomPoint = worldCenter - up * sphereOffset;
    }

    // 上昇中に天井角へ引っかかったとき、横へ少しずらして抜けやすくする。
    private bool TryApplyCornerCorrection()
    {
        if (!movementSettings.InputAssist.UseCornerCorrection)
        {
            return false;
        }

        if (runtimeState.isGrounded || runtimeState.isDashing || runtimeState.isWallGrabbing || runtimeState.isLedgeClimbing)
        {
            return false;
        }

        Vector3 originalVelocity = rb.linearVelocity;
        if (originalVelocity.y <= 0f)
        {
            return false;
        }

        int preferredDirection = ResolveCornerCorrectionDirection();

        if (!TryFindJumpCornerCorrectionOffset(preferredDirection, out float offsetX))
        {
            return false;
        }

        Vector3 correctedPosition = rb.position;
        correctedPosition.x += offsetX;
        rb.position = correctedPosition;

        // 補正そのものでは速度を変えない。
        // 「横にずれた分の力で斜めに飛ぶ」状態を作らないため、速度は元のまま戻す。
        rb.linearVelocity = originalVelocity;
        return true;
    }

    // ダッシュ中に壁角へ引っかかったとき、少し上へずらして抜けやすくする。
    private bool TryApplyDashCornerCorrection()
    {
        if (!movementSettings.Dash.UseDashCornerCorrection)
        {
            return false;
        }

        if (!runtimeState.isDashing)
        {
            return false;
        }

        if (runtimeState.isWallGrabbing || runtimeState.isLedgeClimbing)
        {
            return false;
        }

        if (!runtimeState.isTouchingWall || runtimeState.wallSide == 0)
        {
            return false;
        }

        // 水平成分がなく、真上/真下ダッシュなら補正しない。
        if (Mathf.Abs(runtimeState.dashDirection.x) <= 0.01f)
        {
            return false;
        }

        // 下方向ダッシュでは使わない。
        if (runtimeState.dashDirection.y < 0f)
        {
            return false;
        }

        // 実際に壁へ向かってダッシュしているときだけ発動。
        bool movingIntoWall = runtimeState.dashDirection.x * runtimeState.wallSide > 0.01f;
        if (!movingIntoWall)
        {
            return false;
        }

        Vector3 candidatePosition = rb.position + transform.up * movementSettings.Dash.DashCornerCorrectionUpDistance;

        GetCapsuleWorldPointsAtPosition(
            candidatePosition,
            out Vector3 topPoint,
            out Vector3 bottomPoint,
            out float worldRadius);

        bool blockedAtCandidate = Physics.CheckCapsule(
            topPoint,
            bottomPoint,
            worldRadius * 0.98f,
            movementSettings.Detection.GroundLayerMask,
            QueryTriggerInteraction.Ignore);

        if (blockedAtCandidate)
        {
            return false;
        }

        rb.position = candidatePosition;
        return true;
    }

    // ============================================================
    // 基本移動（横移動・重力）
    // ============================================================

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
        bool isNearApex = !runtimeState.isGrounded && IsNearJumpApex(rb.linearVelocity.y);
        bool isLandingAssistActive = runtimeState.isGrounded && landingControlAssistTimer > 0f;

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

            if (isLandingAssistActive)
            {
                accel *= movementSettings.Move.LandingGroundAccelerationMultiplier;
            }
        }
        else
        {
            accel = runtimeState.isGrounded
                ? movementSettings.Move.GroundDeceleration * resolvedLocomotionModifier.groundAccelerationMultiplier
                : movementSettings.Move.AirDeceleration * resolvedLocomotionModifier.airAccelerationMultiplier;

            if (isLandingAssistActive)
            {
                accel *= movementSettings.Move.LandingGroundDecelerationMultiplier;
            }
        }

        if (isNearApex)
        {
            accel *= movementSettings.Jump.ApexHorizontalControlMultiplier;
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
        TryApplyCornerCorrection();

        Vector3 velocity = rb.linearVelocity;
        bool isFalling = velocity.y < 0f;
        bool isRising = velocity.y > 0f;
        bool isNearApex = !runtimeState.isGrounded && IsNearJumpApex(velocity.y);

        float gravityMultiplier = movementSettings.Jump.GravityScale * resolvedLocomotionModifier.gravityScaleMultiplier;

        if (isRising && playerInputReader.JumpHeld && jumpHoldTimer > 0f)
        {
            gravityMultiplier *= movementSettings.Jump.RiseGravityMultiplier;
        }

        if (isNearApex)
        {
            gravityMultiplier *= movementSettings.Jump.ApexGravityMultiplier;
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
        // 崖乗り上げ中は壁掴まり状態を更新しない。
        if (runtimeState.isLedgeClimbing)
        {
            return;
        }

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

        // 崖乗り上げ中は壁掴まりに進入しない。
        if (runtimeState.isLedgeClimbing)
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

            if (TryStartLedgeClimb())
            {
                return;
            }
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

    // ============================================================
    // ダッシュ処理
    // ============================================================

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
    // 処理順序:
    // 1. 接地スナップ
    // 2. 縦速度の復元または制限
    // 3. 横速度の減衰
    // 4. ジャンプカット無効タイマー設定
    internal void EndDash()
    {
        runtimeState.isDashing = false;
        TrySnapToGroundAfterDash();

        if (movementSettings.Dash.RestoreStartVerticalVelocity)
        {
            Vector3 restoredVelocity = rb.linearVelocity;
            restoredVelocity.y = runtimeState.dashStartVerticalVelocity;
            rb.linearVelocity = restoredVelocity;
        }

        if (runtimeState.dashDirection.y > 0f)
        {
            Vector3 velocity = rb.linearVelocity;
            float maxUpwardVelocity = movementSettings.Dash.UpwardDashEndVerticalSpeedClamp;

            if (velocity.y > maxUpwardVelocity)
            {
                velocity.y = maxUpwardVelocity;
                rb.linearVelocity = velocity;
            }
        }

        {
            Vector3 velocity = rb.linearVelocity;
            velocity.x *= movementSettings.Dash.DashEndHorizontalCarryMultiplier;
            rb.linearVelocity = velocity;
        }

        dashEndJumpCutLockTimer = movementSettings.Dash.DashEndJumpCutLockTime;
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
            landingControlAssistTimer = movementSettings.Move.LandingControlAssistTime;
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

        // PlayerInputReaderで既に正規化済み8方向なのでそのまま使う
        return requestedDirection;
    }

    // ダッシュ中の専用速度を適用する。
    internal void ApplyDashVelocity()
    {
        TryApplyDashCornerCorrection();
        rb.linearVelocity = runtimeState.dashDirection * (movementSettings.Dash.Speed * resolvedLocomotionModifier.dashSpeedMultiplier);
    }

    // ============================================================
    // 崖乗り上げ（レッジクライム）
    // ============================================================

    // 崖乗り上げを開始できるか判定し、開始する。
    internal bool TryStartLedgeClimb()
    {
        if (!movementSettings.Wall.UseLedgeClimb)
        {
            return false;
        }

        if (!runtimeState.isWallGrabbing)
        {
            return false;
        }

        if (runtimeState.isLedgeClimbing)
        {
            return false;
        }

        if (!CanDetectLedge(out Vector3 ledgeTopPosition))
        {
            return false;
        }

        StartLedgeClimb(ledgeTopPosition);
        return true;
    }

    // 崖の頂上を検出できるか判定する。
    // 検出手順:
    // 1. 頭上に障害物がないか確認
    // 2. 前方に壁が続いていないか確認
    // 3. 前方上部に地面があるか確認
    internal bool CanDetectLedge(out Vector3 ledgeTopPosition)
    {
        ledgeTopPosition = Vector3.zero;

        int side = runtimeState.wallGrabSide;
        if (side == 0)
        {
            return false;
        }

        Bounds bounds = capsuleCollider.bounds;
        Vector3 wallDir = Vector3.right * side;

        Vector3 playerHead = bounds.center;
        playerHead.y = bounds.max.y;

        Vector3 headCheckOrigin = playerHead;
        headCheckOrigin.x += wallDir.x * (bounds.extents.x - 0.01f);

        bool hasObstacleAbove = Physics.Raycast(
            headCheckOrigin,
            Vector3.up,
            movementSettings.Wall.LedgeDetectUpDistance,
            movementSettings.Detection.GroundLayerMask,
            QueryTriggerInteraction.Ignore);

        if (hasObstacleAbove)
        {
            return false;
        }

        Vector3 forwardCheckOrigin = bounds.center;
        forwardCheckOrigin.y += bounds.extents.y * 0.3f;
        forwardCheckOrigin.x += wallDir.x * (bounds.extents.x - 0.01f);

        bool hasWallAhead = Physics.Raycast(
            forwardCheckOrigin,
            wallDir,
            movementSettings.Wall.LedgeDetectForwardDistance,
            movementSettings.Detection.GroundLayerMask,
            QueryTriggerInteraction.Ignore);

        if (hasWallAhead)
        {
            return false;
        }

        Vector3 groundCheckOrigin = forwardCheckOrigin;
        groundCheckOrigin.x += wallDir.x * movementSettings.Wall.LedgeDetectForwardDistance;

        float groundCheckDistance = movementSettings.Wall.LedgeGroundCheckDistance + bounds.extents.y;

        bool foundGround = Physics.Raycast(
            groundCheckOrigin,
            Vector3.down,
            out RaycastHit hit,
            groundCheckDistance,
            movementSettings.Detection.GroundLayerMask,
            QueryTriggerInteraction.Ignore);

        if (foundGround)
        {
            ledgeTopPosition = hit.point;
            return true;
        }

        return false;
    }

    // 崖乗り上げを開始する。
    internal void StartLedgeClimb(Vector3 ledgeTopPosition)
    {
        runtimeState.isLedgeClimbing = true;
        runtimeState.ledgeClimbStartTime = Time.time;
        runtimeState.ledgeClimbStartPosition = rb.position;

        int side = runtimeState.wallGrabSide;
        Vector3 targetPosition = ledgeTopPosition;
        targetPosition.x += side * movementSettings.Wall.LedgeClimbForwardOffset;
        targetPosition.y += movementSettings.Wall.LedgeClimbUpOffset;

        runtimeState.ledgeClimbTargetPosition = targetPosition;

        ExitWallGrab();

        runtimeState.wallReattachLockTimer = movementSettings.Wall.LedgeClimbDuration + 0.2f;

        rb.linearVelocity = Vector3.zero;
        rb.isKinematic = true;
        capsuleCollider.enabled = false;
    }

    // 崖乗り上げ中の移動を更新する。
    internal void UpdateLedgeClimb()
    {
        if (!runtimeState.isLedgeClimbing)
        {
            return;
        }

        float elapsed = Time.time - runtimeState.ledgeClimbStartTime;
        float duration = movementSettings.Wall.LedgeClimbDuration;

        if (elapsed >= duration)
        {
            CompleteLedgeClimb();
            return;
        }

        float t = elapsed / duration;
        float easedT = EaseOutCubic(t);

        Vector3 currentPosition = Vector3.Lerp(
            runtimeState.ledgeClimbStartPosition,
            runtimeState.ledgeClimbTargetPosition,
            easedT);

        transform.position = currentPosition;
    }

    // 崖乗り上げを完了する。
    internal void CompleteLedgeClimb()
    {
        runtimeState.isLedgeClimbing = false;
        transform.position = runtimeState.ledgeClimbTargetPosition;

        capsuleCollider.enabled = true;
        rb.isKinematic = false;
        rb.linearVelocity = Vector3.zero;
        runtimeState.isGrounded = false;

        runtimeState.isWallGrabbing = false;
        runtimeState.wallGrabSide = 0;
        runtimeState.isWallSliding = false;
    }

    // イージング関数: EaseOutCubic（滑らかな減速）
    private float EaseOutCubic(float t)
    {
        float f = t - 1f;
        return f * f * f + 1f;
    }
}