using UnityEngine;

public sealed partial class PlayerController
{
    // 現在接地中かどうか。
    private bool isGrounded;

    // 壁接触中かどうか。
    private bool isTouchingWall;

    // 接触している壁の左右。(-1:left / +1:right / 0:none)
    private int wallSide;

    // 壁キック直後の横入力上書きロックタイマー。
    private float wallJumpControlLockTimer;

    // 壁キック直後の壁再付着ロックタイマー。
    private float wallReattachLockTimer;

    // 現在壁滑り中かどうか。
    private bool isWallSliding;

    // 現在前ステ中かどうか。
    private bool isStepping;

    // 現在急降下中かどうか。
    private bool isFastFalling;

    // 前ステ残り時間。
    private float stepTimer;

    // 前ステクールダウン残り時間。
    private float stepCooldownTimer;

    // 前ステ開始時のY速度。
    private float stepStartVerticalVelocity;

    // 最後に向いていた左右方向。(-1:left / +1:right)
    private int facing = 1;
    // 前ステ開始時に固定する進行方向。(-1:left / +1:right)
    private int stepDirection = 1;
    // Update で検出したジャンプ押下を FixedUpdate まで保持する。
    // これにより物理フレームとのズレで押下を取りこぼしにくくする。
    private bool jumpRequested;

    // Update で検出した前ステ押下を FixedUpdate まで保持する。
    // これにより物理フレームとのズレで押下を取りこぼしにくくする。
    private bool stepRequested;

    // 着地/クールダウン解除直前の前ステ入力を保持するタイマー。
    private float stepBufferTimer;

    // 床離れ直後でもジャンプ可能にする猶予タイマー。
    private float coyoteTimer;

    // 着地直前のジャンプ入力を保持するタイマー。
    private float jumpBufferTimer;

    // ジャンプ上昇維持の残り時間。
    private float jumpHoldTimer;

    // Ground 判定デバッグ可視化用の SphereCast 開始位置。
    private Vector3 groundCheckOrigin;

    // Ground 判定デバッグ可視化用の SphereCast 半径。
    private float groundCheckRadius;

    // Ground 判定デバッグ可視化用の SphereCast 距離。
    private float groundCheckDistance;

    // Ground 判定デバッグ可視化用のヒット結果。
    private bool groundCheckHit;

    // Wall 判定デバッグ可視化用の左右 SphereCast 開始位置。
    private Vector3 leftWallCheckOrigin;
    private Vector3 rightWallCheckOrigin;

    // Wall 判定デバッグ可視化用の SphereCast 半径。
    private float wallCheckRadius;

    // Wall 判定デバッグ可視化用の SphereCast 距離。
    private float wallCheckDistance;

    // Wall 判定デバッグ可視化用の左右ヒット結果。
    private bool leftWallCheckHit;
    private bool rightWallCheckHit;
}