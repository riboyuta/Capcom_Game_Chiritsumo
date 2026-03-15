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

    // ボタン短押しでジャンプを低くする機能を使うか。
    public bool useVariableJump = true;

    // ジャンプ上昇中にボタンを離した際の上向き速度倍率。
    // 1 に近いほど変化が小さく、0 に近いほど強く減衰する。
    [Range(0f, 1f)] public float jumpCutMultiplier = 0.5f;

    // 床から離れた直後の猶予ジャンプ(コヨーテタイム)を使うか。
    public bool useCoyoteTime = true;

    // コヨーテタイムの猶予秒数。
    [Min(0f)] public float coyoteTime = 0.1f;

    // 着地前入力を保持して着地直後にジャンプする機能を使うか。
    public bool useJumpBuffer = true;

    // ジャンプ入力を保持する秒数。
    [Min(0f)] public float jumpBufferTime = 0.1f;

    // 落下中に適用する追加重力倍率。
    // 1 なら追加なし、2 なら標準重力1個分を追加する。
    [Min(1f)] public float fallGravityMultiplier = 1f;

    // 落下速度の下限(負方向)を制限するための最大値。
    // 実際の clamp は -maxFallSpeed までを許可する。
    [Min(0f)] public float maxFallSpeed = 20f;


    [Header("Ground Check")]

    // 接地判定で使う下方向の判定距離。
    // 小さすぎると段差や境界を取りこぼしやすい。
    [Min(0f)] public float groundCheckDistance = 0.1f;

    // 地面として扱うレイヤーマスク。
    // SphereCast の対象をここで制限する。
    // 初期値の ~0 は全レイヤーを対象にする。
    public LayerMask groundLayerMask = ~0;
}