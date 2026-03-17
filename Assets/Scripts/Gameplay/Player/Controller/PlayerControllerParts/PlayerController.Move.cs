using UnityEngine;

public sealed partial class PlayerController
{
    private void ApplyStepVelocity()
    {
        int movingDirection = movementSettings.allowTurnDuringStep ? facing : stepDirection;

        Vector2 steppingVelocity = rb.linearVelocity;
        steppingVelocity.x = movingDirection * movementSettings.stepSpeed;

        // 前ステップ中の縦挙動は専用処理を優先し、
        // 通常の重力/落下/壁滑り処理で上書きしない。
        if (movementSettings.stepGravityMultiplier <= 0f)
        {
            steppingVelocity.y = 0f;
        }
        else
        {
            float stepGravityScale = movementSettings.gravityScale * movementSettings.stepGravityMultiplier;
            steppingVelocity.y += Physics.gravity.y * stepGravityScale * Time.fixedDeltaTime;
        }

        rb.linearVelocity = steppingVelocity;
    }

    private void UpdateWallJumpLockTimer(float deltaTime)
    {
        // 壁キック後の入力ロック残り時間を減らす。
        wallJumpControlLockTimer = Mathf.Max(0f, wallJumpControlLockTimer - deltaTime);

        // 壁キック後の再付着ロック残り時間を減らす。
        wallReattachLockTimer = Mathf.Max(0f, wallReattachLockTimer - deltaTime);
    }

    private void ApplyHorizontalMovement(float deltaTime)
    {
        // 移動入力の X 成分を -1 から 1 の範囲に収める。
        float inputX = Mathf.Clamp(playerInputReader.Move.x, -1f, 1f);

        // 入力方向に応じた目標横速度を求める。
        float targetSpeed = inputX * movementSettings.moveMaxSpeed;

        // 移動入力があるかどうかを判定する。
        bool hasMoveInput = Mathf.Abs(inputX) > 0.01f;

        // 入力ありなら加速、入力なしなら減速を使う。
        // 反転入力時は専用加速度へ切り替える。
        float accel;
        if (hasMoveInput)
        {
            // 入力方向と現在速度の符号が逆なら反転中とみなす。
            bool isTurning = rb.linearVelocity.x * inputX < 0f;
            if (isTurning)
            {
                accel = isGrounded ? movementSettings.groundTurnAcceleration : movementSettings.airTurnAcceleration;
            }
            else
            {
                accel = isGrounded ? movementSettings.groundAcceleration : movementSettings.airAcceleration;
            }
        }
        else
        {
            accel = isGrounded ? movementSettings.groundDeceleration : movementSettings.airDeceleration;
        }

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
        bool isRising = velocity.y > 0f;

        float gravityMultiplier = movementSettings.gravityScale;

        // 上昇中は、長押し中かつ有効時間内のみ上昇用倍率を掛ける。
        if (isRising && playerInputReader.JumpHeld && jumpHoldTimer > 0f)
        {
            gravityMultiplier *= movementSettings.riseGravityMultiplier;
        }

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