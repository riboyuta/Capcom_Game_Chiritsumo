using UnityEngine;

public sealed partial class PlayerController
{
    private void UpdateWallJumpLockTimer(float deltaTime)
    {
        // 壁キック後の入力ロック残り時間を減らす。
        wallJumpControlLockTimer = Mathf.Max(0f, wallJumpControlLockTimer - deltaTime);
    }

    private void ApplyHorizontalMovement(float deltaTime)
    {
        // 前ステップ中は通常移動を行わず、
        // 向いている方向へ一定速度で移動する。
        if (isStepping)
        {
            Vector3 steppingVelocity = rb.linearVelocity;
            steppingVelocity.x = facing * movementSettings.stepSpeed;
            rb.linearVelocity = steppingVelocity;
            return;
        }

        // 移動入力の X 成分を -1 から 1 の範囲に収める。
        float inputX = Mathf.Clamp(playerInputReader.Move.x, -1f, 1f);

        // 入力方向に応じた目標横速度を求める。
        float targetSpeed = inputX * movementSettings.moveMaxSpeed;

        // 移動入力があるかどうかを判定する。
        bool hasMoveInput = Mathf.Abs(inputX) > 0.01f;

        // 入力ありなら加速、入力なしなら減速を使う。
        // 地上と空中で使う値も切り替える。
        float accel = hasMoveInput
            ? (isGrounded ? movementSettings.groundAcceleration : movementSettings.airAcceleration)
            : (isGrounded ? movementSettings.groundDeceleration : movementSettings.airDeceleration);

        // 壁キック直後の入力ロック中は横移動入力を受け付けない。
        if (wallJumpControlLockTimer > 0f)
        {
            return;
        }

        // 現在速度を目標速度へ徐々に近づける。
        Vector3 velocity = rb.linearVelocity;
        velocity.x = Mathf.MoveTowards(velocity.x, targetSpeed, accel * deltaTime);
        rb.linearVelocity = velocity;
    }

    private void ApplyCustomGravity()
    {
        Vector3 velocity = rb.linearVelocity;

        // 下向き速度なら落下中とみなす。
        bool isFalling = velocity.y < 0f;

        // 基本の重力倍率を取得する。
        float gravityMultiplier = movementSettings.gravityScale;

        // 落下中は落下用の重力倍率を掛ける。
        // 急降下中なら専用倍率を優先する。
        if (isFalling)
        {
            float fallingMultiplier = movementSettings.fallGravityMultiplier;
            if (isFastFalling)
            {
                fallingMultiplier = movementSettings.fastFallGravityMultiplier;
            }

            gravityMultiplier *= fallingMultiplier;
        }

        // 標準重力との差分だけを追加する。
        if (!Mathf.Approximately(gravityMultiplier, 1f))
        {
            Vector3 extraGravity = Physics.gravity * (gravityMultiplier - 1f);
            rb.AddForce(extraGravity, ForceMode.Acceleration);
        }

        // 落下速度の下限を設定して加速しすぎを防ぐ。
        float maxFallSpeed = isFastFalling && isFalling
            ? movementSettings.fastFallMaxSpeed
            : movementSettings.maxFallSpeed;
        float minVelocityY = -maxFallSpeed;
        if (velocity.y < minVelocityY)
        {
            velocity.y = minVelocityY;
            rb.linearVelocity = velocity;
        }
    }
}