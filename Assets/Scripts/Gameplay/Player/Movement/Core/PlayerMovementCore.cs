using UnityEngine;

// 基本移動と重力を管理するコアシステム。
internal sealed class PlayerMovementCore
{
    private readonly PlayerLocomotionDependencies deps;
    private float landingControlAssistTimer;

    private const float MoveInputThreshold = 0.01f;
    private const float AutoStepProbeRadiusScale = 0.85f;
    private const float AutoStepLowerProbeHeightRatio = 0.45f;
    private const float AutoStepGroundCheckStartPadding = 0.05f;

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

        float inputX = Mathf.Clamp(deps.InputReader.Move.x, -1f, 1f);
        float targetSpeed = inputX * (deps.Settings.Move.MaxSpeed * modifier.moveSpeedMultiplier);
        bool hasMoveInput = Mathf.Abs(inputX) > MoveInputThreshold;

        if (hasMoveInput)
        {
            TryAutoStepUp(inputX);
        }

        bool isLandingAssistActive = deps.RuntimeState.isGrounded && landingControlAssistTimer > 0f;

        float accel;
        if (hasMoveInput)
        {
            bool isTurning = deps.Rb.linearVelocity.x * inputX < 0f;
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

    // 地上移動中に、1段分の段差を自動で乗り上げる。
    private bool TryAutoStepUp(float inputX)
    {
        AutoStepSettings autoStep = deps.Settings.AutoStep;

        if (!autoStep.UseAutoStep)
        {
            return false;
        }

        if (!CanAutoStep())
        {
            return false;
        }

        int directionSign = inputX > 0f ? 1 : -1;
        Vector3 direction = deps.Transform.right * directionSign;
        Vector3 up = deps.Transform.up;

        Vector3 rbPosition = deps.Rb.position;
        GetCapsulePoints(rbPosition, out Vector3 bottomSphereCenter, out Vector3 topSphereCenter, out float worldRadius);

        float lowerProbeHeight = autoStep.MaxHeight * AutoStepLowerProbeHeightRatio;
        Vector3 lowerProbeOrigin = bottomSphereCenter + up * lowerProbeHeight;

        float probeRadius = Mathf.Max(0.01f, worldRadius * AutoStepProbeRadiusScale);
        float lowerCheckDistance = worldRadius + autoStep.ForwardCheckDistance;

        bool hasLowerObstacle = Physics.SphereCast(
            lowerProbeOrigin,
            probeRadius,
            direction,
            out _,
            lowerCheckDistance,
            deps.Settings.Detection.WallLayerMask,
            QueryTriggerInteraction.Ignore);

        if (!hasLowerObstacle)
        {
            return false;
        }

        Vector3 lowestPoint = bottomSphereCenter - up * worldRadius;
        Vector3 groundCheckOrigin =
            lowestPoint +
            direction * lowerCheckDistance +
            up * (autoStep.MaxHeight + AutoStepGroundCheckStartPadding);

        float groundCheckDistance =
            autoStep.MaxHeight +
            AutoStepGroundCheckStartPadding +
            deps.Settings.Detection.GroundCheckDistance;

        bool hasStepGround = Physics.Raycast(
            groundCheckOrigin,
            -up,
            out RaycastHit groundHit,
            groundCheckDistance,
            deps.Settings.Detection.GroundLayerMask,
            QueryTriggerInteraction.Ignore);

        if (!hasStepGround)
        {
            return false;
        }

        float raiseAmount = Vector3.Dot(groundHit.point - lowestPoint, up);

        if (raiseAmount <= 0f)
        {
            return false;
        }

        if (raiseAmount > autoStep.MaxHeight + autoStep.ClearanceMargin)
        {
            return false;
        }

        Vector3 targetPosition = rbPosition + up * (raiseAmount + autoStep.ClearanceMargin);

        if (WouldCapsuleOverlapAt(targetPosition))
        {
            return false;
        }

        // 1段上がった後、前方へ進んだ位置にもプレイヤーが入れるか確認する。
        // ここで引っかかる場合は、1段段差ではなく2段以上の壁として扱う。
        float forwardStandCheckDistance =
            worldRadius +
            autoStep.ForwardCheckDistance +
            autoStep.ClearanceMargin;

        Vector3 forwardStandPosition =
            targetPosition +
            direction * forwardStandCheckDistance;

        if (WouldCapsuleOverlapAt(forwardStandPosition))
        {
            return false;
        }

        deps.Rb.MovePosition(targetPosition);

        deps.Rb.MovePosition(targetPosition);

        Vector3 velocity = deps.Rb.linearVelocity;
        if (velocity.y < 0f)
        {
            velocity.y = 0f;
            deps.Rb.linearVelocity = velocity;
        }

        return true;
    }

    // 自動段差乗り上げを実行してよい状態か判定する。
    private bool CanAutoStep()
    {
        if (!deps.RuntimeState.isGrounded)
        {
            return false;
        }

        if (deps.RuntimeState.isDashing)
        {
            return false;
        }

        if (deps.RuntimeState.isWallGrabbing)
        {
            return false;
        }

        if (deps.RuntimeState.isWallSliding)
        {
            return false;
        }

        if (deps.RuntimeState.isLedgeClimbing)
        {
            return false;
        }

        if (deps.RuntimeState.isStomping)
        {
            return false;
        }

        if (deps.RuntimeState.wallJumpControlLockTimer > 0f)
        {
            return false;
        }

        if (deps.IsActionLocked())
        {
            return false;
        }

        if (deps.IsExternallyControlled())
        {
            return false;
        }

        return true;
    }

    // 指定位置にプレイヤーカプセルを置いた場合の上下球中心と半径を求める。
    private void GetCapsulePoints(Vector3 rbPosition, out Vector3 bottomSphereCenter, out Vector3 topSphereCenter, out float worldRadius)
    {
        Vector3 up = deps.Transform.up;

        Vector3 currentWorldCenter = deps.Transform.TransformPoint(deps.CapsuleCollider.center);
        Vector3 centerOffsetFromRb = currentWorldCenter - deps.Rb.position;
        Vector3 worldCenter = rbPosition + centerOffsetFromRb;

        worldRadius = deps.GetWorldCapsuleRadius();

        float halfHeight = Mathf.Max(deps.CapsuleCollider.height * 0.5f, deps.CapsuleCollider.radius);
        float worldHalfHeight = halfHeight * Mathf.Abs(deps.Transform.lossyScale.y);
        float sphereOffset = Mathf.Max(0f, worldHalfHeight - worldRadius);

        bottomSphereCenter = worldCenter - up * sphereOffset;
        topSphereCenter = worldCenter + up * sphereOffset;
    }

    // 指定位置に移動したとき、プレイヤーカプセルが地形に重なるか確認する。
    private bool WouldCapsuleOverlapAt(Vector3 rbPosition)
    {
        GetCapsulePoints(rbPosition, out Vector3 bottomSphereCenter, out Vector3 topSphereCenter, out float worldRadius);

        float checkRadius = Mathf.Max(0.01f, worldRadius - deps.Settings.AutoStep.ClearanceMargin);
        int solidLayerMask =
            deps.Settings.Detection.GroundLayerMask.value |
            deps.Settings.Detection.WallLayerMask.value;

        return Physics.CheckCapsule(
            bottomSphereCenter,
            topSphereCenter,
            checkRadius,
            solidLayerMask,
            QueryTriggerInteraction.Ignore);
    }
}
