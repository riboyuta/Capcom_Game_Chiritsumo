using UnityEngine;

public sealed partial class PlayerController
{
    private bool CanEnterWallGrab()
    {
        if (!CanAcceptGrabInput())
        {
            return false;
        }

        // 機能が無効なら入らない。
        if (!movementSettings.Wall.UseWallGrab)
        {
            return false;
        }

        // 行動不能中・接地中・ダッシュ中・レール滑走中は不可。
        if (IsActionLocked || runtimeState.isGrounded || runtimeState.isDashing || isGrinding)
        {
            return false;
        }

        // 壁未接触、または壁方向が不明なときは不可。
        if (!runtimeState.isTouchingWall || runtimeState.wallSide == 0)
        {
            return false;
        }

        // 壁再付着ロック中は不可。
        if (runtimeState.wallReattachLockTimer > 0f)
        {
            return false;
        }

        // Grab 入力保持中のみ入る。
        if (!playerInputReader.GrabHeld)
        {
            return false;
        }

        return true;
    }

    private void UpdateWallGrabState()
    {
        // すでに壁捕まり中なら、維持条件を満たさない時だけ抜ける。
        if (runtimeState.isWallGrabbing)
        {
            if (ShouldExitWallGrab())
            {
                ExitWallGrab();
            }

            return;
        }

        // 未捕まり時は進入条件を満たしたときだけ入る。
        if (CanEnterWallGrab())
        {
            EnterWallGrab();
        }
    }

    private bool ShouldExitWallGrab()
    {
        if (IsActionLocked)
        {
            return true;
        }

        if (!CanAcceptGrabInput())
        {
            return true;
        }

        if (!playerInputReader.GrabHeld)
        {
            return true;
        }

        if (runtimeState.isGrounded || runtimeState.isDashing || isGrinding)
        {
            return true;
        }

        if (!runtimeState.isTouchingWall || runtimeState.wallSide == 0)
        {
            return true;
        }

        if (runtimeState.wallReattachLockTimer > 0f)
        {
            return true;
        }

        return false;
    }

    private void EnterWallGrab()
    {
        runtimeState.isWallGrabbing = true;
        runtimeState.wallGrabSide = runtimeState.wallSide;
        runtimeState.isWallSliding = false;
        runtimeState.isFastFalling = false;

        Vector3 velocity = rb.linearVelocity;
        velocity.x = 0f;
        velocity.y = Mathf.Max(velocity.y, movementSettings.Wall.WallGrabVerticalSpeed);
        rb.linearVelocity = velocity;
    }

    private void ExitWallGrab()
    {
        runtimeState.isWallGrabbing = false;
        runtimeState.wallGrabSide = 0;
    }

    private void ApplyWallGrabMovement()
    {
        if (!runtimeState.isWallGrabbing)
        {
            return;
        }

        runtimeState.facing = runtimeState.wallGrabSide;

        Vector3 velocity = rb.linearVelocity;
        velocity.x = 0f;
        velocity.y = movementSettings.Wall.WallGrabVerticalSpeed;
        rb.linearVelocity = velocity;

        runtimeState.isWallSliding = false;
        runtimeState.isFastFalling = false;
    }
}