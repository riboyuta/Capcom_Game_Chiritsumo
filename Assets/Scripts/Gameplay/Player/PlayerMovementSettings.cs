using System;
using UnityEngine;

[Serializable]
public sealed class PlayerMovementSettings
{
    [Header("Horizontal Move")]

    // 地上と空中の両方で使う最大横移動速度。
    // PlayerController では inputX * moveMaxSpeed で目標速度を作る。
    [Min(0f)] public float moveMaxSpeed = 6f;

    // 地上で移動入力があるときの加速度。
    // 値が大きいほど目標速度に早く近づく。
    [Min(0f)] public float groundAcceleration = 50f;

    // 地上で移動入力がないときの減速度。
    // 方向転換するときの止まりやすさにも影響する。
    [Min(0f)] public float groundDeceleration = 60f;

    // 空中で移動入力があるときの加速度。
    // 地上より小さくすると空中制御を弱くできる。
    [Min(0f)] public float airAcceleration = 25f;

    // 空中で移動入力がないときの減速度。
    // 空中でどれだけ慣性を残すかに影響する。
    [Min(0f)] public float airDeceleration = 20f;

    [Header("Vertical Move")]

    // ジャンプ開始時に与える上向き速度。
    // PlayerController では Rigidbody の Y 速度へ直接設定する。
    public float jumpVelocity = 10f;

    // Unity 標準重力に対する倍率。
    // 1 なら標準重力のまま。
    // 2 なら追加重力を足して合計で 2 倍相当にする。
    [Min(0f)] public float gravityScale = 1f;

    [Header("Ground Check")]

    // 接地判定で使う下方向の判定距離。
    // 小さすぎると段差や境界を取りこぼしやすい。
    [Min(0f)] public float groundCheckDistance = 0.1f;

    // 地面として扱うレイヤーマスク。
    // SphereCast の対象をここで制限する。
    // 初期値の ~0 は全レイヤーを対象にする。
    public LayerMask groundLayerMask = ~0;
}