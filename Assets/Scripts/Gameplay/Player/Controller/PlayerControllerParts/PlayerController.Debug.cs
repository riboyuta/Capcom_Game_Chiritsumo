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

    // デバッグ表示向けのジャンプ上昇維持タイマー。
    public float JumpHoldTimer => jumpHoldTimer;

    // デバッグ表示向けの上昇中追加重力倍率。
    public float RiseGravityMultiplier => movementSettings.riseGravityMultiplier;

    // デバッグ表示向けのダッシュ要求状態。
    public bool DashRequested => dashRequested;

    // デバッグ表示向けのダッシュバッファタイマー。
    public float DashBufferTimer => dashBufferTimer;

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

    // デバッグ表示向けのダッシュ状態。
    public bool IsDashing => isDashing;

    // デバッグ表示向けの急降下状態。
    public bool IsFastFalling => isFastFalling;

    // デバッグ表示向けのダッシュ残り時間。
    public float DashTimer => dashTimer;

    // デバッグ表示向けのダッシュクールダウン残り時間。
    public float DashCooldownTimer => dashCooldownTimer;

    // デバッグ表示向けのダッシュ中重力倍率。
    public float DashGravityMultiplier => movementSettings.dashGravityMultiplier;

    // デバッグ表示向けのダッシュ開始時Y速度。
    public float DashStartVerticalVelocity => dashStartVerticalVelocity;

    // デバッグ表示向けのダッシュ開始時Y速度復元設定。
    public bool RestoreDashStartVerticalVelocity => movementSettings.restoreDashStartVerticalVelocity;

    // デバッグ表示向けの左壁判定開始位置。
    public Vector3 LeftWallCheckOrigin => leftWallCheckOrigin;

    // デバッグ表示向けのダッシュ中方向転換許可設定。

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

    // 外部ギミック（一方通行床など）から参照する下入力状態。
    // スティックまたは十字キーの下方向が一定以上入力されている場合に true。
    public bool IsDownInputHeld => playerInputReader != null && playerInputReader.Move.y < -0.5f;

    // 外部要因（バネ床など）で打ち上げられたことをプレイヤーに通知する。
    // 可変ジャンプカットをスキップし、着地時に自動で解除される。
    public void NotifyExternalLaunch()
    {
        isExternalLaunched = true;
    }
}