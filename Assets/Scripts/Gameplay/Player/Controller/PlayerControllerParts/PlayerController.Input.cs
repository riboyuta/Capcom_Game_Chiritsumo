using UnityEngine;

public sealed partial class PlayerController
{
    private const float DiagonalInputThreshold = 0.5f;

    public Vector2 MoveInputDirection => playerInputReader != null ? playerInputReader.Move : Vector2.zero;
    public bool IsMoveInputDiagonal => ComputeIsMoveInputDiagonal();

    public void RequestInputBlockThisFrame(InputBlockFlags flags)
    {
        frameRequests.requestedInputBlockFlagsThisFrame |= flags;
    }

    private void ResetInputBlockRequestsThisFrame()
    {
        frameRequests.ResetPerFrameRequests();
    }

    private bool CanAcceptMoveInput()
    {
        return !IsInputBlocked(InputBlockFlags.Move);
    }

    private bool CanAcceptJumpInput()
    {
        return !IsInputBlocked(InputBlockFlags.Jump);
    }

    private bool CanAcceptDashInput()
    {
        return !IsInputBlocked(InputBlockFlags.Dash);
    }

    private bool CanAcceptGrabInput()
    {
        return !IsInputBlocked(InputBlockFlags.Grab);
    }

    private bool IsInputBlocked(InputBlockFlags flags)
    {
        InputBlockFlags blockedFlags = frameRequests.requestedInputBlockFlagsThisFrame;

        if (externalControlSystem != null)
        {
            blockedFlags |= externalControlSystem.PersistentInputBlockFlags;
        }

        return (blockedFlags & flags) != 0;
    }

    private bool ComputeIsMoveInputDiagonal()
    {
        Vector2 move = MoveInputDirection;
        return Mathf.Abs(move.x) >= DiagonalInputThreshold
            && Mathf.Abs(move.y) >= DiagonalInputThreshold;
    }

    private void UpdateFacingFromMoveInput()
    {
        // 掴まれ・叩きつけ・死亡などの行動不能中は向き更新を止める。
        // 死亡中の見た目固定に対して入力側の更新影響を持ち込まないため。
        if (IsActionLocked)
        {
            return;
        }

        if (runtimeState.isDashing)
        {
            return;
        }
        if (runtimeState.isWallGrabbing)
        {
            return;
        }

        if (!CanAcceptMoveInput())
        {
            return;
        }

        // 移動入力の X 成分を -1 から 1 の範囲に収める。
        float inputX = Mathf.Clamp(playerInputReader.Move.x, -1f, 1f);
        // わずかな入力ぶれで向きが変わらないように閾値を設ける。
        const float facingThreshold = 0.1f;

        // 右入力が閾値を超えたら右向きにする。
        if (inputX > facingThreshold)
        {
            runtimeState.facing = 1;
        }
        // 左入力が負の閾値を下回ったら左向きにする。
        else if (inputX < -facingThreshold)
        {
            runtimeState.facing = -1;
        }
    }
}