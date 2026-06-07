using UnityEngine;

// PlayerController から View へ渡す見た目専用スナップショット。
// RuntimeState をそのまま公開せず、表示判断に必要な情報だけをまとめる。
internal readonly struct PlayerAnimationSnapshot
{
    // 継続状態
    public readonly bool isGrounded;
    public readonly bool isDashing;
    public readonly bool isWallSliding;
    public readonly bool isWallGrabbing;
    public readonly bool isLedgeClimbing;
    public readonly bool isStomping;
    public readonly bool isExternallyControlled;
    public readonly bool isDead;

    // 単発イベント
    public readonly bool justLanded;
    public readonly bool justJumped;
    public readonly bool justWallJumped;
    public readonly bool justCrossedApex;
    public readonly bool justDashStarted;
    public readonly bool requestHoodRecover;

    // 移動情報
    public readonly float velocityX;
    public readonly float velocityY;
    public readonly float moveInputX;
    public readonly float moveInputY;

    // 向き・壁情報
    public readonly int facing;
    public readonly int wallSide;
    public readonly int wallGrabSide;

    // 見た目専用状態
    public readonly PlayerHoodVisualState hoodState;

    // HoodRecover 要求ID。
    // bool だけでは連続要求を区別できないため、要求ごとに増えるIDを持つ。
    public readonly int hoodRecoverRequestId;

    // この HoodRecover が対象にするフード世代。
    // 古い HoodRecover 完了で、新しい Dash 後の Down 状態を Up にしないために使う。
    public readonly int hoodRecoverTargetVersion;

    // 現在のフード世代。
    public readonly int hoodVisualVersion;

    // 空の初期値。
    public static PlayerAnimationSnapshot Default => new PlayerAnimationSnapshot(
    isGrounded: false,
    isDashing: false,
    isWallSliding: false,
    isWallGrabbing: false,
    isLedgeClimbing: false,
    isStomping: false,
    isExternallyControlled: false,
    isDead: false,
    justLanded: false,
    justJumped: false,
    justWallJumped: false,
    justCrossedApex: false,
    justDashStarted: false,
    requestHoodRecover: false,
    velocityX: 0f,
    velocityY: 0f,
    moveInputX: 0f,
    moveInputY: 0f,
    facing: 1,
    wallSide: 0,
    wallGrabSide: 0,
    hoodState: PlayerHoodVisualState.Up,
    hoodRecoverRequestId: 0,
    hoodRecoverTargetVersion: 0,
    hoodVisualVersion: 0);

    internal PlayerAnimationSnapshot(
        bool isGrounded,
        bool isDashing,
        bool isWallSliding,
        bool isWallGrabbing,
        bool isLedgeClimbing,
        bool isStomping,
        bool isExternallyControlled,
        bool isDead,
        bool justLanded,
        bool justJumped,
        bool justWallJumped,
        bool justCrossedApex,
        bool justDashStarted,
        bool requestHoodRecover,
        float velocityX,
        float velocityY,
        float moveInputX,
        float moveInputY,
        int facing,
        int wallSide,
        int wallGrabSide,
        PlayerHoodVisualState hoodState,
        int hoodRecoverRequestId,
        int hoodRecoverTargetVersion,
        int hoodVisualVersion)
    {
        this.isGrounded = isGrounded;
        this.isDashing = isDashing;
        this.isWallSliding = isWallSliding;
        this.isWallGrabbing = isWallGrabbing;
        this.isLedgeClimbing = isLedgeClimbing;
        this.isStomping = isStomping;
        this.isExternallyControlled = isExternallyControlled;
        this.isDead = isDead;

        this.justLanded = justLanded;
        this.justJumped = justJumped;
        this.justWallJumped = justWallJumped;
        this.justCrossedApex = justCrossedApex;
        this.justDashStarted = justDashStarted;
        this.requestHoodRecover = requestHoodRecover;

        this.velocityX = velocityX;
        this.velocityY = velocityY;
        this.moveInputX = moveInputX;
        this.moveInputY = moveInputY;

        this.facing = facing == 0 ? 1 : (facing > 0 ? 1 : -1);
        this.wallSide = wallSide;   
        this.wallGrabSide = wallGrabSide;
        this.hoodState = hoodState;
        this.hoodRecoverRequestId = hoodRecoverRequestId;
        this.hoodRecoverTargetVersion = hoodRecoverTargetVersion;
        this.hoodVisualVersion = hoodVisualVersion;
    }

    // 地上で横移動入力があるかの簡易判定。
    internal bool HasMoveInput(float threshold = 0.1f)
    {
        return Mathf.Abs(moveInputX) >= threshold;
    }

    // 上昇中かの簡易判定。
    internal bool IsRising(float threshold = 0.05f)
    {
        return velocityY > threshold;
    }

    // 落下中かの簡易判定。
    internal bool IsFalling(float threshold = -0.05f)
    {
        return velocityY < threshold;
    }

    // 壁掴まり中に上下入力が入っているかの簡易判定。
    internal bool HasWallClimbInput(float threshold = 0.1f)
    {
        return Mathf.Abs(moveInputY) >= threshold;
    }
}