using UnityEngine;

// 追尾敵が参照する 1 時点ぶんのプレイヤー記録。
// 移動再生用の情報と、モデル表示用の情報だけを持つ。
public struct PlayerShadowSnapshot
{
    // 記録時刻
    public float time;

    // 移動再生用
    public Vector3 position;
    public Quaternion rotation;
    public Vector3 velocity;

    // 左右の向き（-1: 左、+1: 右）
    public int facing;

    // 記録時点でプレイヤーがダッシュ中だったか。
    public bool isDashing;

    // モデル表示用。
    // ShadowChaserModelView はこれを読んで、プレイヤーと同じアニメーション状態を遅延再生する。
    internal PlayerAnimationSnapshot animationSnapshot;
}