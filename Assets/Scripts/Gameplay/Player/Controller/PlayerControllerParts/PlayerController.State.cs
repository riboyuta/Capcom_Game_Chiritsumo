using UnityEngine;

public sealed partial class PlayerController
{
    public bool IsDashActive => isDashing;
    public bool IsGrounded => isGrounded;
    public bool IsAirborne => !isGrounded;
    public bool IsWallGrabbing => isWallGrabbing;
    public int Facing => facing;

    // TODO: WallGrabTimeRemaining は壁掴まり時間制限の内部データ実装後に公開する。

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

    // 現在壁捕まり中かどうか。
    private bool isWallGrabbing;

    // 捕まっている壁の左右。(-1:left / +1:right / 0:none)
    private int wallGrabSide;

    // 現在ダッシュ中かどうか。
    private bool isDashing;

    // 現在急降下中かどうか。
    private bool isFastFalling;

    // ダッシュ残り時間。
    private float dashTimer;
    // 地上ダッシュ連続制限用の残り時間。
    private float groundDashCooldownTimer;
    // 現在のダッシュ残数。
    private int currentDashCharges;

    // 前フレームの接地状態。
    // 接地遷移を検出して、必要時のみダッシュ回復させるために使う。
    private bool wasGroundedLastFrame;


    // ダッシュ開始時のY速度。
    private float dashStartVerticalVelocity;

    // 最後に向いていた左右方向。(-1:left / +1:right)
    private int facing = 1;

    // 死亡演出中に描画向きを固定するかどうか。
    // true の間は VisualState へ通常 facing ではなく fixedDeathFacing を流す。
    private bool isDeathFacingFixed;

    // 死亡演出開始時点の描画向き。(-1:left / +1:right)
    // 0 が入らないよう、設定時に安全補正して扱う。
    private int fixedDeathFacing = 1;

    // ダッシュ開始時に固定する進行方向。
    // 無入力時は facing から (±1, 0)、入力時は正規化済みベクトルを保持する。
    private Vector2 dashDirection = Vector2.right;
    // Update で検出したジャンプ押下を FixedUpdate まで保持する。
    // これにより物理フレームとのズレで押下を取りこぼしにくくする。
    private bool jumpRequested;

    // Update で検出したダッシュ押下を FixedUpdate まで保持する。
    // これにより物理フレームとのズレで押下を取りこぼしにくくする。
    private bool dashRequested;

    // 着地/クールダウン解除直前のダッシュ入力を保持するタイマー。
    private float dashBufferTimer;

    // 見た目向け単発イベント(1物理フレームだけ true)。
    private bool justLandedThisFrame;
    private bool justJumpedThisFrame;
    private bool justWallJumpedThisFrame;
    private bool justCrossedApexThisFrame;

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

    // 外部要因（バネ床など）で打ち上げられた状態。
    // true の間は可変ジャンプカットを適用しない。
    // 着地時に自動で解除される。
    private bool isExternalLaunched;

    // --- Grind Rail (レール滑走) 関連の状態 ---
    
    // 現在レール上を滑らかに滑走中かどうか
    private bool isGrinding;
    
    // 現在乗っているレールギミック
    private RailGimmick currentRail;
    
    // レール上で現在位置しているセグメントのインデックス
    private int currentRailSegment;
    
    // 現在のセグメント上での進行距離
    private float distanceOnRailSegment;
    
    // ウェイポイントに対する進行方向 (+1: start to end, -1: end to start)
    private int grindDirection = 1;
    
    // レール再吸着ロックタイマー
    private float railReattachLockTimer;
}