using UnityEngine;

public sealed partial class PlayerController
{
    // デバッグ表示向けの接地状態。
    public bool IsGrounded => isGrounded;

    // デバッグ表示向けの現在速度。
    public Vector3 CurrentVelocity => rb != null ? rb.linearVelocity : Vector3.zero;

    // デバッグ表示向けのジャンプ要求状態。
    // Update で押下された入力が次の FixedUpdate で消費されるまで true になる。
    public bool JumpRequested => jumpRequested;

    // デバッグ表示向けのコヨーテタイマー。
    public float CoyoteTimer => coyoteTimer;

    // デバッグ表示向けのジャンプバッファタイマー。
    public float JumpBufferTimer => jumpBufferTimer;

    // デバッグ表示向けの前ステ要求状態。
    public bool StepRequested => stepRequested;

    // デバッグ表示向けの前ステバッファタイマー。
    public float StepBufferTimer => stepBufferTimer;

    // デバッグ表示向けの Ground 判定開始位置。
    public Vector3 GroundCheckOrigin => groundCheckOrigin;

    // デバッグ表示向けの Ground 判定半径。
    public float GroundCheckRadius => groundCheckRadius;

    // デバッグ表示向けの Ground 判定距離。
    public float GroundCheckDistance => groundCheckDistance;

    // デバッグ表示向けの Ground 判定ヒット結果。
    public bool GroundCheckHit => groundCheckHit;

    // デバッグ表示向けの壁接触状態。
    public bool IsTouchingWall => isTouchingWall;

    // デバッグ表示向けの壁左右情報。(-1:left / +1:right / 0:none)
    public int WallSide => wallSide;

    // デバッグ表示向けの壁滑り状態。
    public bool IsWallSliding => isWallSliding;

    // デバッグ表示向けの壁キック入力ロックタイマー。
    public float WallJumpControlLockTimer => wallJumpControlLockTimer;

    // デバッグ表示向けの壁再付着ロックタイマー。
    public float WallReattachLockTimer => wallReattachLockTimer;

    // デバッグ表示向けの向き。(-1:left / +1:right)
    public int Facing => facing;

    // デバッグ表示向けの前ステ状態。
    public bool IsStepping => isStepping;

    // デバッグ表示向けの急降下状態。
    public bool IsFastFalling => isFastFalling;

    // デバッグ表示向けの前ステ残り時間。
    public float StepTimer => stepTimer;

    // デバッグ表示向けの前ステクールダウン残り時間。
    public float StepCooldownTimer => stepCooldownTimer;

    // デバッグ表示向けの左壁判定開始位置。
    public Vector3 LeftWallCheckOrigin => leftWallCheckOrigin;

    // デバッグ表示向けの右壁判定開始位置。
    public Vector3 RightWallCheckOrigin => rightWallCheckOrigin;

    // デバッグ表示向けの壁判定半径。
    public float WallCheckRadius => wallCheckRadius;

    // デバッグ表示向けの壁判定距離。
    public float WallCheckDistance => wallCheckDistance;

    // デバッグ表示向けの左壁判定ヒット結果。
    public bool LeftWallCheckHit => leftWallCheckHit;

    // デバッグ表示向けの右壁判定ヒット結果。
    public bool RightWallCheckHit => rightWallCheckHit;
}