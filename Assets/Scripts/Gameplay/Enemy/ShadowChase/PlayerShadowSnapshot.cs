using UnityEngine;

// 追尾敵が参照する 1 時点ぶんのプレイヤー記録。
// PlayerController の内部状態をそのまま見せず、
// 追尾用途に必要な情報だけをまとめる。
public struct PlayerShadowSnapshot
{
    // 記録時刻（Time.time）
    public float time;

    // 位置、回転、速度
    public Vector3 position;
    public Quaternion rotation;
    public Vector3 velocity;

    // 左右の向き（-1: 左、+1: 右）
    public int facing;

    // 接触状態
    public bool isGrounded;
    public bool isTouchingWall;
    public int wallSide;

    // 行動状態
    public bool isWallSliding;
    public bool isDashing;
    public bool isFastFalling;

    // 制約状態
    public bool isActionLocked;
    public bool isDead;

    // プレイヤー側で確定した見た目状態。
    // ShadowChaserView はこれを読むことで、
    // PlayerView に近いロジックで影の見た目を決められる。
    public PlayerController.VisualState visualState;
}