using UnityEngine;

// ストンプ開始判定・状態管理・速度計算を担当するシステム。
internal sealed class PlayerStompSystem
{
    private readonly PlayerLocomotionDependencies deps;

    internal PlayerStompSystem(PlayerLocomotionDependencies deps)
    {
        this.deps = deps;
    }

    // 復帰時やリセット時にストンプ関連状態を初期化する。
    internal void ResetRuntimeState()
    {
        deps.RuntimeState.isStomping = false;
        deps.RuntimeState.stompTimer = 0f;
        deps.RuntimeState.stompStartHorizontalVelocity = 0f;
    }

    // ストンプ開始可否を判定する。
    internal bool CanStartStomp()
    {
        if (!deps.Settings.Stomp.UseStomp)
        {
            return false;
        }

        if (!deps.FrameRequests.stompRequested)
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

        if (deps.RuntimeState.isStomping)
        {
            return false;
        }

        if (deps.RuntimeState.isGrounded)
        {
            return false;
        }

        if (deps.RuntimeState.isWallGrabbing)
        {
            return false;
        }

        if (deps.RuntimeState.isLedgeClimbing)
        {
            return false;
        }

        if (deps.RuntimeState.isDashing && !deps.Settings.Stomp.AllowStartWhileDashing)
        {
            return false;
        }

        if (deps.FrameRequests.dashRequested && !deps.Settings.Stomp.PreferStompOverDownStepDash)
        {
            return false;
        }

        return true;
    }

    // ストンプ開始を試みる。成否に関わらず stompRequested は消費する。
    internal bool TryStartStomp()
    {
        bool canStart = CanStartStomp();
        deps.FrameRequests.stompRequested = false;

        if (!canStart)
        {
            return false;
        }

        deps.RuntimeState.isStomping = true;
        deps.RuntimeState.stompTimer = 0f;
        deps.FrameRequests.dashRequested = false;

        if (deps.Rb != null)
        {
            deps.RuntimeState.stompStartHorizontalVelocity = deps.Rb.linearVelocity.x;
        }
        else
        {
            deps.RuntimeState.stompStartHorizontalVelocity = 0f;
        }

        // TODO Step 4: AllowStartWhileDashing が true の場合は DashSystem 側のダッシュ中断処理と接続する。
        return true;
    }

    // ストンプ中に適用する速度を計算して反映する。
    internal void ApplyStompVelocity()
    {
        if (!deps.RuntimeState.isStomping)
        {
            return;
        }

        if (deps.Rb == null)
        {
            return;
        }

        float stompSpeed = deps.Settings.Dash.Speed * deps.Settings.Stomp.SpeedMultiplier;
        Vector3 velocity = deps.Rb.linearVelocity;

        switch (deps.Settings.Stomp.HorizontalPolicy)
        {
            case StompHorizontalPolicy.Zero:
                velocity.x = 0f;
                break;

            case StompHorizontalPolicy.Keep:
                velocity.x = deps.RuntimeState.stompStartHorizontalVelocity;
                break;

            case StompHorizontalPolicy.Damp:
                velocity.x = deps.RuntimeState.stompStartHorizontalVelocity * deps.Settings.Stomp.HorizontalDampMultiplier;
                break;

            case StompHorizontalPolicy.AirControl:
                // Step 3 最小実装: 既存の通常横移動統合は行わず、現在値を維持する。
                velocity.x = deps.Rb.linearVelocity.x;
                break;
        }

        velocity.y = -stompSpeed;
        deps.Rb.linearVelocity = velocity;
    }

    // ストンプ入力離しで終了する設定を処理する。
    internal void UpdateStompCancelByInput()
    {
        if (!deps.RuntimeState.isStomping)
        {
            return;
        }

        if (!deps.Settings.Stomp.CancelOnRelease)
        {
            return;
        }

        if (!deps.InputReader.StompHeld || deps.InputReader.StompReleased)
        {
            EndStomp();
        }
    }

    // ストンプ継続時間を更新する。
    internal void UpdateStompTimer(float deltaTime)
    {
        if (!deps.RuntimeState.isStomping)
        {
            return;
        }

        deps.RuntimeState.stompTimer += deltaTime;
    }

    // ストンプ状態を終了する。
    internal void EndStomp()
    {
        deps.RuntimeState.isStomping = false;
        deps.RuntimeState.stompTimer = 0f;
        deps.RuntimeState.stompStartHorizontalVelocity = 0f;
    }

    // 通常着地時のストンプ終了口。
    internal void EndStompByLanding()
    {
        if (!deps.RuntimeState.isStomping)
        {
            return;
        }

        EndStomp();
    }

    // 壁掴まり遷移優先時のストンプ終了口。
    internal void EndStompForWallGrab()
    {
        EndStomp();
    }
}