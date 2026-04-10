using UnityEngine;

// PlayerController 内部専用の継続実行時状態コンテナ。
internal sealed class PlayerRuntimeState
{
    // 現在接地中かどうか。
    public bool isGrounded;

    // 壁接触中かどうか。
    public bool isTouchingWall;

    // 接触している壁の左右。(-1:left / +1:right / 0:none)
    public int wallSide;

    // 壁キック直後の横入力上書きロックタイマー。
    public float wallJumpControlLockTimer;

    // 壁キック直後の壁再付着ロックタイマー。
    public float wallReattachLockTimer;

    // 現在壁滑り中かどうか。
    public bool isWallSliding;

    // 現在壁捕まり中かどうか。
    public bool isWallGrabbing;

    // 捕まっている壁の左右。(-1:left / +1:right / 0:none)
    public int wallGrabSide;

    // 現在ダッシュ中かどうか。
    public bool isDashing;

    // 現在急降下中かどうか。
    public bool isFastFalling;

    // ダッシュ残り時間。
    public float dashTimer;

    // 地上ダッシュ連続制限用の残り時間。
    public float groundDashCooldownTimer;

    // 現在のダッシュ残数。
    public int currentDashCharges;

    // 前フレームの接地状態。
    // 接地遷移を検出して、必要時のみダッシュ回復させるために使う。
    public bool wasGroundedLastFrame;

    // ダッシュ開始時のY速度。
    public float dashStartVerticalVelocity;

    // 最後に向いていた左右方向。(-1:left / +1:right)
    public int facing = 1;

    // 死亡演出中に描画向きを固定するかどうか。
    // true の間は VisualState へ通常 facing ではなく fixedDeathFacing を流す。
    public bool isDeathFacingFixed;

    // 死亡演出開始時点の描画向き。(-1:left / +1:right)
    // 0 が入らないよう、設定時に安全補正して扱う。
    public int fixedDeathFacing = 1;

    // ダッシュ開始時に固定する進行方向。
    // 無入力時は facing から (±1, 0)、入力時は正規化済みベクトルを保持する。
    public Vector2 dashDirection = Vector2.right;

    // 床離れ直後でもジャンプ可能にする猶予タイマー。
    public float coyoteTimer;

    // 着地直前のジャンプ入力を保持するタイマー。
    public float jumpBufferTimer;

    // ジャンプ上昇維持の残り時間。
    public float jumpHoldTimer;

}