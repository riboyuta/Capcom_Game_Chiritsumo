using UnityEngine;

public sealed partial class PlayerController
{
    private void UpdateWallJumpLockTimer(float deltaTime)
    {
        wallJumpControlLockTimer = Mathf.Max(0f, wallJumpControlLockTimer - deltaTime);
    }

    private void ApplyHorizontalMovement(float deltaTime)
    {
        if (isStepping)
        {
            Vector3 steppingVelocity = rb.linearVelocity;
            steppingVelocity.x = facing * movementSettings.stepSpeed;
            rb.linearVelocity = steppingVelocity;
            return;
        }

        float inputX = Mathf.Clamp(playerInputReader.Move.x, -1f, 1f);
        float targetSpeed = inputX * movementSettings.moveMaxSpeed;
        bool hasMoveInput = Mathf.Abs(inputX) > 0.01f;

        float accel = hasMoveInput
            ? (isGrounded ? movementSettings.groundAcceleration : movementSettings.airAcceleration)
            : (isGrounded ? movementSettings.groundDeceleration : movementSettings.airDeceleration);

        // 壁キック直後は入力上書きを抑えて初速を維持する。
        if (wallJumpControlLockTimer > 0f)
        {
            return;
        }

        Vector3 velocity = rb.linearVelocity;
        velocity.x = Mathf.MoveTowards(velocity.x, targetSpeed, accel * deltaTime);
        rb.linearVelocity = velocity;
    }

    private void ApplyCustomGravity()
    {
        float gravityMultiplier = movementSettings.gravityScale;
        if (rb.linearVelocity.y < 0f)
        {
            gravityMultiplier *= movementSettings.fallGravityMultiplier;
        }

        if (!Mathf.Approximately(gravityMultiplier, 1f))
        {
            Vector3 extraGravity = Physics.gravity * (gravityMultiplier - 1f);
            rb.AddForce(extraGravity, ForceMode.Acceleration);
        }

        Vector3 velocity = rb.linearVelocity;
        float minVelocityY = -movementSettings.maxFallSpeed;
        if (velocity.y < minVelocityY)
        {
            velocity.y = minVelocityY;
            rb.linearVelocity = velocity;
        }
    }
}