using UnityEngine;

public sealed partial class PlayerController
{
    // モデル表示用スナップショットを確定する。
    // 既存の VisualState と並行で運用する。
    private void FinalizeAnimationSnapshot()
    {
        Vector3 velocity = rb != null ? rb.linearVelocity : Vector3.zero;
        Vector2 moveInput = playerInputReader != null ? playerInputReader.Move : Vector2.zero;

        int facing = ResolveAnimationFacing();

        currentAnimationSnapshot = new PlayerAnimationSnapshot(
            isGrounded: runtimeState.isGrounded,
            isDashing: runtimeState.isDashing,
            isWallSliding: runtimeState.isWallSliding,
            isWallGrabbing: runtimeState.isWallGrabbing,
            isLedgeClimbing: runtimeState.isLedgeClimbing,
            isStomping: runtimeState.isStomping,
            isExternallyControlled: IsExternallyControlled,
            isDead: IsDeadState,

            justLanded: justLandedThisFrame,
            justJumped: locomotionSystem != null && locomotionSystem.JustJumpedThisFrame,
            justWallJumped: locomotionSystem != null && locomotionSystem.JustWallJumpedThisFrame,
            justCrossedApex: justCrossedApexThisFrame,
            justDashStarted: runtimeState.justDashStartedThisFrame,
            requestHoodRecover: runtimeState.requestHoodRecoverThisFrame,
            dashStartRequestId: runtimeState.dashStartRequestId,
            
            velocityX: velocity.x,
            velocityY: velocity.y,
            moveInputX: moveInput.x,
            moveInputY: moveInput.y,

            facing: facing,
            wallSide: runtimeState.wallSide,
            wallGrabSide: runtimeState.wallGrabSide,

            hoodState: runtimeState.hoodVisualState,
            hoodRecoverRequestId: runtimeState.hoodRecoverRequestId,
            hoodRecoverTargetVersion: runtimeState.hoodRecoverTargetVersion,
            hoodVisualVersion: runtimeState.hoodVisualVersion);
    }

    // 見た目用の向きを解決する。
    // 死亡演出中は fixedDeathFacing を優先する既存設計に揃える。
    private int ResolveAnimationFacing()
    {
        if (runtimeState.isDeathFacingFixed)
        {
            return runtimeState.fixedDeathFacing != 0
                ? runtimeState.fixedDeathFacing
                : 1;
        }

        return runtimeState.facing != 0
            ? runtimeState.facing
            : 1;
    }
}