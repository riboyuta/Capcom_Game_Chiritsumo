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

    // 初期値の ~0 は全レイヤーを対象にする。
    public LayerMask groundLayerMask = ~0;

    [Header("Wall Action")]

    // 壁滑りを有効化するか。
    public bool useWallSlide = true;

    // 壁判定で使う左右方向の判定距離。
    [Min(0f)] public float wallCheckDistance = 0.15f;

    // 壁判定で使う SphereCast 半径。
    [Min(0f)] public float wallCheckRadius = 0.2f;

    // 壁滑り中に許可する最大落下速度。
    // 実際の clamp は -wallSlideMaxSpeed までを許可する。
    [Min(0f)] public float wallSlideMaxSpeed = 3f;

    // 壁キックを有効化するか。
    public bool useWallKick = true;

    // 壁キック時に壁から離れる方向へ与える横速度。
    [Min(0f)] public float wallJumpHorizontalVelocity = 7f;

    // 壁キック時に与える上向き速度。
    public float wallJumpVerticalVelocity = 9f;

    // 壁キック直後の横入力上書きを抑える時間。
    [Min(0f)] public float wallJumpControlLockTime = 0.1f;
    // 「壁方向へ入力している」と判定する入力しきい値。
    [Range(0f, 1f)] public float wallInputThreshold = 0.1f;

    [Header("Step")]

    // 前ステを有効化するか。
    public bool useStep = true;

    // 前ステ中の固定横速度。
    [Min(0f)] public float stepSpeed = 12f;

    // 前ステ継続時間(秒)。
    [Min(0f)] public float stepDuration = 0.12f;

    // 前ステ再使用までのクールダウン(秒)。
    [Min(0f)] public float stepCooldown = 0.35f;

    // 空中前ステを許可するか。
    // Phase 4 では設定値のみ保持し、挙動には未使用。
    public bool allowAirStep;

    // 前ステ中の無敵を有効化するか。
    // Phase 4 では設定値のみ保持し、挙動には未使用。
    public bool stepInvulnerable;


}