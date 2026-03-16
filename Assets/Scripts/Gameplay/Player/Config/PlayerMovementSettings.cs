using System;
using UnityEngine;

[Serializable]
public sealed class PlayerMovementSettings
{
    [Header("最大横移動速度")]
    [Min(0f)] public float moveMaxSpeed = 6f;

    [Header("地上加速度")]
    [Min(0f)] public float groundAcceleration = 50f;

    [Header("地上反転加速度")]
    [Min(0f)] public float groundTurnAcceleration = 65f;

    [Header("地上減速度")]
    [Min(0f)] public float groundDeceleration = 60f;

    [Header("空中加速度")]
    [Min(0f)] public float airAcceleration = 25f;

    [Header("空中反転加速度")]
    [Min(0f)] public float airTurnAcceleration = 32f;

    [Header("空中減速度")]
    [Min(0f)] public float airDeceleration = 20f;

    [Header("ジャンプ初速")]
    public float jumpVelocity = 10f;

    [Header("重力倍率")]
    [Min(0f)] public float gravityScale = 1f;

    [Header("可変ジャンプを使う")]
    public bool useVariableJump = true;

    [Header("ジャンプ長押しの最大有効時間")]
    [Min(0f)] public float maxJumpHoldTime = 0.12f;

    [Header("上昇中追加重力倍率")]
    [Min(0f)] public float riseGravityMultiplier = 1f;

    [Header("ジャンプ短押し時の減衰率")]
    [Range(0f, 1f)] public float jumpCutMultiplier = 0.5f;

    [Header("コヨーテタイムを使う")]
    public bool useCoyoteTime = true;

    [Header("コヨーテタイム秒数")]
    [Min(0f)] public float coyoteTime = 0.1f;

    [Header("ジャンプバッファを使う")]
    public bool useJumpBuffer = true;

    [Header("ジャンプ入力保持時間")]
    [Min(0f)] public float jumpBufferTime = 0.1f;

    [Header("落下時追加重力倍率")]
    [Min(1f)] public float fallGravityMultiplier = 1f;

    [Header("最大落下速度")]
    [Min(0f)] public float maxFallSpeed = 20f;

    [Header("急降下を使う")]
    public bool useFastFall = true;

    [Header("急降下時の落下重力倍率")]
    [Min(1f)] public float fastFallGravityMultiplier = 2.5f;

    [Header("急降下時の最大落下速度")]
    [Min(0f)] public float fastFallMaxSpeed = 28f;

    [Header("接地判定距離")]
    [Min(0f)] public float groundCheckDistance = 0.1f;

    [Header("接地判定レイヤー")]
    public LayerMask groundLayerMask = ~0;

    [Header("壁滑りを使う")]
    public bool useWallSlide = true;

    [Header("壁判定距離")]
    [Min(0f)] public float wallCheckDistance = 0.15f;

    [Header("壁判定半径")]
    [Min(0f)] public float wallCheckRadius = 0.2f;

    [Header("壁滑り最大落下速度")]
    [Min(0f)] public float wallSlideMaxSpeed = 3f;

    [Header("壁キックを使う")]
    public bool useWallKick = true;

    [Header("壁キック横速度")]
    [Min(0f)] public float wallJumpHorizontalVelocity = 7f;

    [Header("壁キック上速度")]
    public float wallJumpVerticalVelocity = 9f;

    [Header("壁キック後入力ロック時間")]
    [Min(0f)] public float wallJumpControlLockTime = 0.1f;

    [Header("壁キック後再付着ロック時間")]
    [Min(0f)] public float wallReattachLockTime = 0.12f;

    [Header("壁入力しきい値")]
    [Range(0f, 1f)] public float wallInputThreshold = 0.1f;

    [Header("前ステを使う")]
    public bool useStep = true;

    [Header("前ステ速度")]
    [Min(0f)] public float stepSpeed = 12f;

    [Header("前ステ時間")]
    [Min(0f)] public float stepDuration = 0.12f;

    [Header("前ステ中重力倍率")]
    [Min(0f)] public float stepGravityMultiplier = 1f;

    [Header("前ステ終了時に開始時Y速度を復元")]
    public bool restoreStepStartVerticalVelocity;

    [Header("前ステクールダウン")]
    [Min(0f)] public float stepCooldown = 0.35f;

    [Header("空中前ステを許可")]
    public bool allowAirStep;

    [Header("前ステ入力バッファを使う")]
    public bool useStepBuffer = true;

    [Header("前ステ入力保持時間")]
    [Min(0f)] public float stepBufferTime = 0.1f;

    [Header("前ステ中無敵")]
    public bool stepInvulnerable;
}