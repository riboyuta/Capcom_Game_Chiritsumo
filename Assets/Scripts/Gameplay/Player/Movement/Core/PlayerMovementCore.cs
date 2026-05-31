using UnityEngine;

// 基本移動と重力を管理するコアシステム。
internal sealed class PlayerMovementCore
{
    private readonly PlayerLocomotionDependencies deps;
    private float landingControlAssistTimer;

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
    // 通常の横移動速度を更新する。
    internal void ApplyHorizontalMovement(float deltaTime, PlayerLocomotionModifierRequest modifier, bool isNearApex)
    {
        if (!deps.CanAcceptMoveInput())
        {
            return;
        }

        float rawInputX = Mathf.Clamp(deps.InputReader.Move.x, -1f, 1f);
        float moveDirection = ResolveHorizontalMoveDirection(rawInputX);

        float targetSpeed = moveDirection * (deps.Settings.Move.MaxSpeed * modifier.moveSpeedMultiplier);
        bool hasMoveInput = moveDirection != 0f;
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
}
