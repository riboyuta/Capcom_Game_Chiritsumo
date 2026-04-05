using UnityEngine;

// 追尾敵が参照する 1 時点ぶんのプレイヤー記録。
// PlayerController の内部状態をそのまま見せず、
// 追尾用途に必要な情報だけをまとめる。
public struct PlayerShadowSnapshot
{
    public float time;

    public Vector3 position;
    public Quaternion rotation;
    public Vector3 velocity;

    public int facing;

    public bool isGrounded;
    public bool isTouchingWall;
    public int wallSide;

    public bool isWallSliding;
    public bool isStepping;
    public bool isFastFalling;

    public bool isActionLocked;
    public bool isDead;
}