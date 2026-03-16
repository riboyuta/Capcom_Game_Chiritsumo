using UnityEngine;

public sealed partial class PlayerController
{
    private void UpdateStepTimers(float deltaTime)
    {
        // 前ステップのクールダウン残り時間を減らす。
        stepCooldownTimer = Mathf.Max(0f, stepCooldownTimer - deltaTime);

        // 前ステップ中でなければステップ時間は 0 に戻す。
        if (!isStepping)
        {
            stepTimer = 0f;
            return;
        }

        // 前ステップ中は残り時間を減らす。
        stepTimer = Mathf.Max(0f, stepTimer - deltaTime);

        // 残り時間がなくなったら前ステップ終了とする。
        if (stepTimer <= 0f)
        {
            EndStep();
        }
    }

    private void EndStep()
    {
        isStepping = false;

        if (!movementSettings.restoreStepStartVerticalVelocity)
        {
            return;
        }

        Vector3 velocity = rb.linearVelocity;
        velocity.y = stepStartVerticalVelocity;
        rb.linearVelocity = velocity;
    }

    private void UpdateStepBufferTimer(float deltaTime)
    {
        // ステップバッファ無効時は常に 0 にする。
        if (!movementSettings.useStepBuffer)
        {
            stepBufferTimer = 0f;
            return;
        }

        // ステップバッファの残り時間を減らす。
        stepBufferTimer = Mathf.Max(0f, stepBufferTimer - deltaTime);
    }

    private void TryStartStep()
    {
        // 機能が無効なら入力とバッファを破棄する。
        if (!movementSettings.useStep)
        {
            stepRequested = false;
            stepBufferTimer = 0f;
            return;
        }

        // 今フレームのステップ要求を読み取る。
        bool immediateStepRequest = stepRequested;

        // 入力があった場合は、必要ならバッファ時間を開始する。
        if (immediateStepRequest)
        {
            if (movementSettings.useStepBuffer)
            {
                stepBufferTimer = movementSettings.stepBufferTime;
            }

            // 読み取った入力は消費済みにする。
            stepRequested = false;
        }

        // バッファ中なら過去の入力でもステップ要求ありとみなす。
        bool hasBufferedStep = movementSettings.useStepBuffer && stepBufferTimer > 0f;
        bool hasStepRequest = immediateStepRequest || hasBufferedStep;

        // 要求がなければ何もしない。
        if (!hasStepRequest)
        {
            return;
        }

        // すでに前ステップ中なら開始しない。
        if (isStepping)
        {
            return;
        }

        // クールダウン中は開始しない。
        if (stepCooldownTimer > 0f)
        {
            return;
        }

        // 空中前ステップ無効時は、非接地なら開始しない。
        if (!isGrounded && !movementSettings.allowAirStep)
        {
            return;
        }

        // 前ステップ開始状態へ入る。
        isStepping = true;
        isFastFalling = false;
        stepTimer = movementSettings.stepDuration;
        stepCooldownTimer = movementSettings.stepCooldown;
        stepStartVerticalVelocity = rb.linearVelocity.y;

        // ステップ入力とバッファを消費する。
        stepRequested = false;
        stepBufferTimer = 0f;

        // 前ステップ開始時はジャンプ要求も破棄する。
        jumpRequested = false;
        if (movementSettings.useJumpBuffer)
        {
            jumpBufferTimer = 0f;
        }
    }
}