using UnityEngine;

// PlayerController に追尾用 snapshot 取得口だけを追加する partial。
// 敵側が PlayerController の private 状態へ直接依存しないようにする。
public sealed partial class PlayerController
{
    // 現在フレーム時点の追尾用 snapshot を返す。
    // PlayerShadowRecorder が定期的に呼び出して履歴を作成する。
    public PlayerShadowSnapshot CaptureShadowSnapshot()
    {
        PlayerShadowSnapshot snapshot = new PlayerShadowSnapshot();

        snapshot.time = Time.time;

        // 位置、回転、速度を記録
        snapshot.position = transform.position;
        snapshot.rotation = transform.rotation;
        snapshot.velocity = rb != null ? rb.linearVelocity : Vector3.zero;

        // 向きを正規化して記録
        snapshot.facing = NormalizeShadowFacing(runtimeState.facing);

        // 状態フラグを記録
        snapshot.isGrounded = runtimeState.isGrounded;
        snapshot.isTouchingWall = runtimeState.isTouchingWall;
        snapshot.wallSide = runtimeState.wallSide;

        snapshot.isWallSliding = runtimeState.isWallSliding;
        snapshot.isDashing = runtimeState.isDashing;
        snapshot.isFastFalling = runtimeState.isFastFalling;

        snapshot.isActionLocked = IsActionLocked;
        snapshot.isDead = IsDeadState;

        // Player 側で確定済みの見た目状態をそのまま渡す。
        snapshot.visualState = CurrentVisualState;

        return snapshot;
    }

    // 0 や異常値が入っても -1 / +1 に寄せる。
    // 追尾敵が左右の向きを確実に判定できるように正規化する。
    private int NormalizeShadowFacing(int direction)
    {
        if (direction < 0)
        {
            return -1;
        }

        return 1;
    }
}