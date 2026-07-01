using UnityEngine;

// 基本移動と重力を管理するコアシステム。
internal sealed class PlayerMovementCore
{
    private readonly PlayerLocomotionDependencies deps;
    private float landingControlAssistTimer;
    private bool autoStepInterpolationActive;
    private Vector3 autoStepInterpolationStartPosition;
    private Vector3 autoStepInterpolationTargetPosition;
    private Vector3 autoStepInterpolationUp;
    private float autoStepInterpolationElapsed;
    private float autoStepInterpolationDuration;
    private float autoStepInterpolationStartTime;

    private const float MoveInputThreshold = 0.01f;
    private const float AutoStepProbeRadiusScale = 0.85f;
    private const float AutoStepLowerProbeHeightRatio = 0.45f;
    private const float AutoStepGroundCheckStartPadding = 0.05f;
    private const float DashVerticalThreshold = 0.1f;
    private const float AutoStepInterpolationStaleTime = 0.1f;


    internal PlayerMovementCore(PlayerLocomotionDependencies deps)
    {
        this.deps = deps;
    }

    // ============================================================
    // タイマー更新
    // ============================================================

    // 着地直後だけ横制御を強める補正タイマーを更新する。
    internal void UpdateLandingAssistTimer(float deltaTime)
    {
        landingControlAssistTimer = Mathf.Max(0f, landingControlAssistTimer - deltaTime);
    }

    // 着地時に地上ダッシュ連続制限を解除する。
    internal void HandleGroundDashCooldownOnLanding()
    {
        if (!deps.RuntimeState.wasGroundedLastFrame && deps.RuntimeState.isGrounded)
        {
            deps.RuntimeState.groundDashCooldownTimer = 0f;
            landingControlAssistTimer = deps.Settings.Move.LandingControlAssistTime;
        }
    }

    // ============================================================
    // 向き更新
    // ============================================================

    // 移動入力から向きを更新する。
    internal void UpdateFacingFromMoveInput()
    {
        if (deps.IsActionLocked())
        {
            return;
        }

        if (deps.RuntimeState.isDashing || deps.RuntimeState.isWallGrabbing)
        {
            return;
        }

        if (!deps.CanAcceptMoveInput())
        {
            return;
        }

        float inputX = Mathf.Clamp(deps.InputReader.Move.x, -1f, 1f);
        const float facingThreshold = 0.1f;

        if (inputX > facingThreshold)
        {
            deps.RuntimeState.facing = 1;
        }
        else if (inputX < -facingThreshold)
        {
            deps.RuntimeState.facing = -1;
        }
    }

    // ============================================================
    // 横移動
    // ============================================================

    // 通常の横移動速度を更新する。
    internal void ApplyHorizontalMovement(float deltaTime, PlayerLocomotionModifierRequest modifier, bool isNearApex)
    {
        if (!deps.CanAcceptMoveInput())
        {
            return;
        }

        bool updatedAutoStepInterpolation = UpdateAutoStepInterpolation(deltaTime, allowWhileDashing: false);

        float rawInputX = Mathf.Clamp(deps.InputReader.Move.x, -1f, 1f);
        float moveDirection = ResolveHorizontalMoveDirection(rawInputX);

        float targetSpeed = moveDirection * (deps.Settings.Move.MaxSpeed * modifier.moveSpeedMultiplier);
        bool hasMoveInput = Mathf.Abs(moveDirection) > MoveInputThreshold;

        if (hasMoveInput && !updatedAutoStepInterpolation)
        {
            TryAutoStepUp(moveDirection, deltaTime);
        }

        bool isLandingAssistActive = deps.RuntimeState.isGrounded && landingControlAssistTimer > 0f;

        float accel;
        if (hasMoveInput)
        {
            bool isTurning = deps.Rb.linearVelocity.x * moveDirection < 0f;
            if (isTurning)
            {
                accel = deps.RuntimeState.isGrounded
                    ? deps.Settings.Move.GroundTurnAcceleration * modifier.groundAccelerationMultiplier
                    : deps.Settings.Move.AirTurnAcceleration * modifier.airAccelerationMultiplier;
            }
            else
            {
                accel = deps.RuntimeState.isGrounded
                    ? deps.Settings.Move.GroundAcceleration * modifier.groundAccelerationMultiplier
                    : deps.Settings.Move.AirAcceleration * modifier.airAccelerationMultiplier;
            }

            if (isLandingAssistActive)
            {
                accel *= deps.Settings.Move.LandingGroundAccelerationMultiplier;
            }
        }
        else
        {
            accel = deps.RuntimeState.isGrounded
                ? deps.Settings.Move.GroundDeceleration * modifier.groundAccelerationMultiplier
                : deps.Settings.Move.AirDeceleration * modifier.airAccelerationMultiplier;

            if (isLandingAssistActive)
            {
                accel *= deps.Settings.Move.LandingGroundDecelerationMultiplier;
            }
        }

        if (isNearApex)
        {
            accel *= deps.Settings.Jump.ApexHorizontalControlMultiplier;
        }

        if (deps.RuntimeState.wallJumpControlLockTimer > 0f)
        {
            return;
        }

        Vector3 velocity = deps.Rb.linearVelocity;
        velocity.x = Mathf.MoveTowards(velocity.x, targetSpeed, accel * deltaTime);
        deps.Rb.linearVelocity = velocity;
    }

    // 通常移動用に、横入力を -1 / 0 / 1 に変換する。
    // 入力の強さではなく「左右どちらへ向かうか」だけを速度計算に使う。
    private static float ResolveHorizontalMoveDirection(float rawInputX)
    {
        const float horizontalMoveThreshold = 0.5f;

        if (rawInputX >= horizontalMoveThreshold)
        {
            return 1f;
        }

        if (rawInputX <= -horizontalMoveThreshold)
        {
            return -1f;
        }

        return 0f;
    }

    // ============================================================
    // 重力
    // ============================================================

    // カスタム重力と落下上限を適用する。
    internal void ApplyCustomGravity(PlayerLocomotionModifierRequest modifier, float jumpHoldTimer, bool isNearApex)
    {
        Vector3 velocity = deps.Rb.linearVelocity;
        bool isFalling = velocity.y < 0f;
        bool isRising = velocity.y > 0f;

        float gravityMultiplier = deps.Settings.Jump.GravityScale * modifier.gravityScaleMultiplier;

        if (isRising && deps.InputReader.JumpHeld && jumpHoldTimer > 0f)
        {
            gravityMultiplier *= deps.Settings.Jump.RiseGravityMultiplier;
        }

        if (isNearApex)
        {
            gravityMultiplier *= deps.Settings.Jump.ApexGravityMultiplier;
        }

        if (isFalling)
        {
            gravityMultiplier *= deps.Settings.Fall.GravityMultiplier;
        }

        if (!Mathf.Approximately(gravityMultiplier, 1f))
        {
            Vector3 extraGravity = Physics.gravity * (gravityMultiplier - 1f);
            deps.Rb.AddForce(extraGravity, ForceMode.Acceleration);
        }

        float maxFallSpeed = deps.Settings.Fall.MaxSpeed;
        float minVelocityY = -maxFallSpeed;
        if (velocity.y < minVelocityY)
        {
            velocity.y = minVelocityY;
            deps.Rb.linearVelocity = velocity;
        }
    }

    // ============================================================
    // 自動段差乗り上げ
    // ============================================================

    internal bool TryAutoStepUpFromDash()
    {
        if (!deps.RuntimeState.isDashing)
        {
            return false;
        }

        Vector2 dashDirection = deps.RuntimeState.dashDirection;

        if (!IsHorizontalDashDirection(dashDirection))
        {
            CancelAutoStepInterpolation();
            return false;
        }

        if (UpdateAutoStepInterpolation(Time.fixedDeltaTime, allowWhileDashing: true))
        {
            return true;
        }

        return TryAutoStepUp(dashDirection.x, true, Time.fixedDeltaTime);
    }

    private bool TryAutoStepUp(float rawInputX, float deltaTime)
    {
        return TryAutoStepUp(rawInputX, false, deltaTime);
    }

    private bool TryAutoStepUp(float rawInputX, bool allowWhileDashing, float deltaTime)
    {
        if (UpdateAutoStepInterpolation(deltaTime, allowWhileDashing))
        {
            return true;
        }

        AutoStepSettings autoStep = deps.Settings.AutoStep;

        if (!autoStep.UseAutoStep || !CanAutoStep(allowWhileDashing))
        {
            return false;
        }

        Vector3 direction = GetHorizontalDirection(rawInputX);
        Vector3 up = deps.Transform.up;
        Vector3 rbPosition = deps.Rb.position;

        GetCapsulePoints(rbPosition, out Vector3 bottomSphereCenter, out _, out float worldRadius);

        if (!DetectStepObstacle(bottomSphereCenter, direction, up, worldRadius, autoStep))
        {
            return false;
        }

        if (!TryFindStepGround(bottomSphereCenter, direction, up, worldRadius, autoStep, out float raiseAmount))
        {
            return false;
        }

        if (!IsValidStepHeight(raiseAmount, autoStep))
        {
            return false;
        }

        Vector3 targetPosition = rbPosition + up * (raiseAmount + autoStep.ClearanceMargin);

        if (!CanFitAtPositions(targetPosition, direction, worldRadius, autoStep))
        {
            return false;
        }

        BeginStepUpInterpolation(targetPosition, allowWhileDashing, deltaTime);
        return true;
    }

    private bool CanAutoStep(bool allowWhileDashing)
    {
        PlayerRuntimeState state = deps.RuntimeState;

        return state.isGrounded
            && (allowWhileDashing || !state.isDashing)
            && !state.isWallGrabbing
            && !state.isWallSliding
            && !state.isLedgeClimbing
            && !state.isStomping
            && state.wallJumpControlLockTimer <= 0f
            && !IsAutoStepInterruptedByUpwardVelocity()
            && !deps.IsActionLocked()
            && !deps.IsExternallyControlled();
    }

    private void GetCapsulePoints(
        Vector3 rbPosition,
        out Vector3 bottomSphereCenter,
        out Vector3 topSphereCenter,
        out float worldRadius)
    {
        Vector3 up = deps.Transform.up;
        Vector3 worldCenter = CalculateWorldCenterAt(rbPosition);

        worldRadius = deps.GetWorldCapsuleRadius();
        float sphereOffset = CalculateSphereOffset(worldRadius);

        bottomSphereCenter = worldCenter - up * sphereOffset;
        topSphereCenter = worldCenter + up * sphereOffset;
    }

    private bool WouldCapsuleOverlapAt(Vector3 rbPosition)
    {
        GetCapsulePoints(rbPosition, out Vector3 bottomSphereCenter, out Vector3 topSphereCenter, out float worldRadius);

        float checkRadius = Mathf.Max(0.01f, worldRadius - deps.Settings.AutoStep.ClearanceMargin);
        int solidLayerMask = GetSolidLayerMask();

        return Physics.CheckCapsule(
            bottomSphereCenter,
            topSphereCenter,
            checkRadius,
            solidLayerMask,
            QueryTriggerInteraction.Ignore);
    }

    private bool IsHorizontalDashDirection(Vector2 dashDirection)
    {
        return Mathf.Abs(dashDirection.x) > MoveInputThreshold
            && Mathf.Abs(dashDirection.y) <= DashVerticalThreshold;
    }

    private Vector3 GetHorizontalDirection(float rawInputX)
    {
        int directionSign = rawInputX > 0f ? 1 : -1;
        return deps.Transform.right * directionSign;
    }

    private bool DetectStepObstacle(
        Vector3 bottomSphereCenter,
        Vector3 direction,
        Vector3 up,
        float worldRadius,
        AutoStepSettings autoStep)
    {
        float lowerProbeHeight = autoStep.MaxHeight * AutoStepLowerProbeHeightRatio;
        Vector3 lowerProbeOrigin = bottomSphereCenter + up * lowerProbeHeight;
        float probeRadius = Mathf.Max(0.01f, worldRadius * AutoStepProbeRadiusScale);
        float lowerCheckDistance = worldRadius + autoStep.ForwardCheckDistance;

        return Physics.SphereCast(
            lowerProbeOrigin,
            probeRadius,
            direction,
            out _,
            lowerCheckDistance,
            deps.Settings.Detection.WallLayerMask,
            QueryTriggerInteraction.Ignore);
    }

    private bool TryFindStepGround(
        Vector3 bottomSphereCenter,
        Vector3 direction,
        Vector3 up,
        float worldRadius,
        AutoStepSettings autoStep,
        out float raiseAmount)
    {
        raiseAmount = 0f;

        Vector3 lowestPoint = bottomSphereCenter - up * worldRadius;
        float lowerCheckDistance = worldRadius + autoStep.ForwardCheckDistance;

        Vector3 groundCheckOrigin = lowestPoint
            + direction * lowerCheckDistance
            + up * (autoStep.MaxHeight + AutoStepGroundCheckStartPadding);

        float groundCheckDistance = autoStep.MaxHeight
            + AutoStepGroundCheckStartPadding
            + deps.Settings.Detection.GroundCheckDistance;

        bool hasStepGround = Physics.Raycast(
            groundCheckOrigin,
            -up,
            out RaycastHit groundHit,
            groundCheckDistance,
            deps.Settings.Detection.GroundLayerMask,
            QueryTriggerInteraction.Ignore);

        if (hasStepGround)
        {
            raiseAmount = Vector3.Dot(groundHit.point - lowestPoint, up);
        }

        return hasStepGround;
    }

    private bool IsValidStepHeight(float raiseAmount, AutoStepSettings autoStep)
    {
        return raiseAmount > 0f
            && raiseAmount <= autoStep.MaxHeight + autoStep.ClearanceMargin;
    }

    private bool CanFitAtPositions(
        Vector3 targetPosition,
        Vector3 direction,
        float worldRadius,
        AutoStepSettings autoStep)
    {
        if (WouldCapsuleOverlapAt(targetPosition))
        {
            return false;
        }

        float forwardStandCheckDistance = worldRadius
            + autoStep.ForwardCheckDistance
            + autoStep.ClearanceMargin;

        Vector3 forwardStandPosition = targetPosition + direction * forwardStandCheckDistance;

        return !WouldCapsuleOverlapAt(forwardStandPosition);
    }

    private void BeginStepUpInterpolation(Vector3 targetPosition, bool allowWhileDashing, float deltaTime)
    {
        autoStepInterpolationActive = true;
        autoStepInterpolationStartPosition = deps.Rb.position;
        autoStepInterpolationTargetPosition = targetPosition;
        autoStepInterpolationUp = deps.Transform.up;
        autoStepInterpolationElapsed = 0f;
        autoStepInterpolationDuration = ResolveAutoStepSmoothDuration(allowWhileDashing);
        autoStepInterpolationStartTime = Time.fixedTime;

        UpdateAutoStepInterpolation(deltaTime, allowWhileDashing);
    }

    private bool UpdateAutoStepInterpolation(float deltaTime, bool allowWhileDashing)
    {
        if (!autoStepInterpolationActive)
        {
            return false;
        }

        if (!CanContinueAutoStepInterpolation(allowWhileDashing) || IsAutoStepInterpolationStale())
        {
            CancelAutoStepInterpolation();
            return false;
        }

        float safeDuration = Mathf.Max(Time.fixedDeltaTime, autoStepInterpolationDuration);
        autoStepInterpolationElapsed = Mathf.Min(
            safeDuration,
            autoStepInterpolationElapsed + Mathf.Max(0f, deltaTime));

        float progress = Mathf.Clamp01(autoStepInterpolationElapsed / safeDuration);
        ApplyAutoStepInterpolatedPosition(progress);
        ClampDownwardVelocityForAutoStep();

        if (progress >= 1f)
        {
            CancelAutoStepInterpolation();
        }

        return true;
    }

    private void ApplyAutoStepInterpolatedPosition(float progress)
    {
        Vector3 currentPosition = deps.Rb.position;
        float startHeight = Vector3.Dot(autoStepInterpolationStartPosition, autoStepInterpolationUp);
        float targetHeight = Vector3.Dot(autoStepInterpolationTargetPosition, autoStepInterpolationUp);
        float currentHeight = Vector3.Dot(currentPosition, autoStepInterpolationUp);
        float nextHeight = Mathf.Lerp(startHeight, targetHeight, progress);
        Vector3 nextPosition = currentPosition + autoStepInterpolationUp * (nextHeight - currentHeight);

        deps.Rb.MovePosition(nextPosition);
    }

    private bool CanContinueAutoStepInterpolation(bool allowWhileDashing)
    {
        PlayerRuntimeState state = deps.RuntimeState;

        return (allowWhileDashing || !state.isDashing)
            && !state.isWallGrabbing
            && !state.isWallSliding
            && !state.isLedgeClimbing
            && !state.isStomping
            && state.wallJumpControlLockTimer <= 0f
            && !IsAutoStepInterruptedByUpwardVelocity()
            && !deps.IsActionLocked()
            && !deps.IsExternallyControlled();
    }

    private bool IsAutoStepInterruptedByUpwardVelocity()
    {
        return !deps.RuntimeState.isDashing
            && !deps.RuntimeState.isGrounded
            && deps.Rb.linearVelocity.y > 0f;
    }

    private bool IsAutoStepInterpolationStale()
    {
        float maxAge = Mathf.Max(AutoStepInterpolationStaleTime, autoStepInterpolationDuration * 4f);
        return Time.fixedTime - autoStepInterpolationStartTime > maxAge;
    }

    private float ResolveAutoStepSmoothDuration(bool allowWhileDashing)
    {
        AutoStepSettings autoStep = deps.Settings.AutoStep;
        return allowWhileDashing
            ? autoStep.DashStepSmoothDuration
            : autoStep.StepSmoothDuration;
    }

    private void ClampDownwardVelocityForAutoStep()
    {
        Vector3 velocity = deps.Rb.linearVelocity;
        if (velocity.y < 0f)
        {
            velocity.y = 0f;
            deps.Rb.linearVelocity = velocity;
        }
    }

    private void CancelAutoStepInterpolation()
    {
        autoStepInterpolationActive = false;
        autoStepInterpolationElapsed = 0f;
    }

    private Vector3 CalculateWorldCenterAt(Vector3 rbPosition)
    {
        Vector3 currentWorldCenter = deps.Transform.TransformPoint(deps.CapsuleCollider.center);
        Vector3 centerOffsetFromRb = currentWorldCenter - deps.Rb.position;
        return rbPosition + centerOffsetFromRb;
    }

    private float CalculateSphereOffset(float worldRadius)
    {
        float halfHeight = Mathf.Max(deps.CapsuleCollider.height * 0.5f, deps.CapsuleCollider.radius);
        float worldHalfHeight = halfHeight * Mathf.Abs(deps.Transform.lossyScale.y);
        return Mathf.Max(0f, worldHalfHeight - worldRadius);
    }

    private int GetSolidLayerMask()
    {
        return deps.Settings.Detection.GroundLayerMask.value
            | deps.Settings.Detection.WallLayerMask.value;
    }

}
