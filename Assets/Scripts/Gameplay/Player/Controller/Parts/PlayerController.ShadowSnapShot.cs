using UnityEngine;

// PlayerController に追尾用 snapshot 取得口だけを追加する partial。
// 敵側が PlayerController の private 状態へ直接依存しないようにする。
public sealed partial class PlayerController
{
    // 現在フレーム時点の追尾用 snapshot を返す。
    // PlayerShadowRecorder が定期的に呼び出して履歴を作成する。
    public PlayerShadowSnapshot CaptureShadowSnapshot()
    {
        Vector3 position = rb != null ? rb.position : transform.position;
        Vector3 velocity = rb != null ? rb.linearVelocity : Vector3.zero;
        int facing = NormalizeShadowFacing(ResolveAnimationFacing());

        PlayerShadowSnapshot snapshot = new PlayerShadowSnapshot();

        snapshot.time = Time.time;

        // 移動再生用
        snapshot.position = position;
        snapshot.rotation = transform.rotation;
        snapshot.velocity = velocity;
        snapshot.facing = facing;

        // 影敵がプレイヤーの過去ダッシュを再現しているか判定するために使う
        snapshot.isDashing = runtimeState.isDashing;

        // モデル表示用
        snapshot.animationSnapshot = CurrentAnimationSnapshot;

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