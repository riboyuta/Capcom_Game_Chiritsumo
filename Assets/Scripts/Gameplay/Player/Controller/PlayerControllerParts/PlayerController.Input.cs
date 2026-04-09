using UnityEngine;

public sealed partial class PlayerController
{
    [System.Flags]
    public enum InputBlockFlags
    {
        None = 0,
        Move = 1 << 0,
        Jump = 1 << 1,
        Dash = 1 << 2,
        Grab = 1 << 3
    }

    private const float DiagonalInputThreshold = 0.5f;
    private InputBlockFlags requestedInputBlockFlagsThisFrame = InputBlockFlags.None;

    public Vector2 MoveInputDirection => playerInputReader != null ? playerInputReader.Move : Vector2.zero;
    public bool IsMoveInputDiagonal => ComputeIsMoveInputDiagonal();

    public void RequestInputBlockThisFrame(InputBlockFlags flags)
    {
        requestedInputBlockFlagsThisFrame |= flags;
    }

    private void ResetInputBlockRequestsThisFrame()
    {
        requestedInputBlockFlagsThisFrame = InputBlockFlags.None;
    }

    private bool CanAcceptMoveInput()
    {
        return (requestedInputBlockFlagsThisFrame & InputBlockFlags.Move) == 0;
    }

    private bool CanAcceptJumpInput()
    {
        return (requestedInputBlockFlagsThisFrame & InputBlockFlags.Jump) == 0;
    }

    private bool CanAcceptDashInput()
    {
        return (requestedInputBlockFlagsThisFrame & InputBlockFlags.Dash) == 0;
    }

    private bool CanAcceptGrabInput()
    {
        return (requestedInputBlockFlagsThisFrame & InputBlockFlags.Grab) == 0;
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