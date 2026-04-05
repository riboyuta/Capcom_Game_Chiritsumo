using UnityEngine;

// PlayerController に追尾用 snapshot 取得口だけを追加する partial。
// 敵側が PlayerController の private 状態へ直接依存しないようにする。
public sealed partial class PlayerController
{
    // 現在フレーム時点の追尾用 snapshot を返す。
    public PlayerShadowSnapshot CaptureShadowSnapshot()
    {
        PlayerShadowSnapshot snapshot = new PlayerShadowSnapshot();

        snapshot.time = Time.time;

        snapshot.position = transform.position;
        snapshot.rotation = transform.rotation;
        snapshot.velocity = rb != null ? rb.linearVelocity : Vector3.zero;

        snapshot.facing = NormalizeShadowFacing(facing);

        snapshot.isGrounded = isGrounded;
        snapshot.isTouchingWall = isTouchingWall;
        snapshot.wallSide = wallSide;

        snapshot.isWallSliding = isWallSliding;
        snapshot.isStepping = isStepping;
        snapshot.isFastFalling = isFastFalling;

        snapshot.isActionLocked = IsActionLocked;
        snapshot.isDead = reactionState == PlayerReactionState.Dead;

        return snapshot;
    }

    // 0 や異常値が入っても -1 / +1 に寄せる。
    private int NormalizeShadowFacing(int direction)
    {
        if (direction < 0)
        {
            return -1;
        }

        return 1;
    }
}