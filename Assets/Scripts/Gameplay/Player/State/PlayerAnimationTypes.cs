// モデル表示用のベースアニメーション状態。
// Gameplay の生状態ではなく、見た目として何を再生するかを表す。
internal enum PlayerAnimationState
{
    Idle,
    Run,
    Land,
    JumpStart,
    JumpRise,
    JumpToFall,
    Fall,
    WallSlide,
    WallGrabIdle,
    WallClimb,
    WallJump,
    Dash,
    LedgeClimb,
    Stomp
}

// 上半身だけに重ねる補助アニメーション状態。
// Base Layer の移動アニメとは独立して扱う。
internal enum PlayerUpperBodyOverlayState
{
    None,
    HoodRecover
}

// フードの見た目状態。
// ダッシュ残数とは分離し、純粋に表示上の状態として扱う。
internal enum PlayerHoodVisualState
{
    Up,
    Down
}