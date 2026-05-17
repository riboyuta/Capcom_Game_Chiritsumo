using UnityEngine;

// PlayerAnimationSnapshot から、モデル表示用のベースアニメーション状態を解決する設定。
// PlayerModelView から Inspector 経由で調整する想定。
[System.Serializable]
internal sealed class PlayerAnimationResolverSettings
{
    [Header("判定しきい値")]
    [Tooltip("接地中に Run へ切り替える最小横速度です。")]
    [Min(0f)]
    public float runVelocityThreshold = 0.05f;

    [Tooltip("空中で JumpRise とみなす最小上向き速度です。")]
    public float riseVelocityThreshold = 0.05f;

    [Tooltip("壁掴まり中に WallClimb とみなす最小上下入力です。")]
    [Min(0f)]
    public float wallClimbInputThreshold = 0.1f;

    [Header("最低維持時間")]
    [Tooltip("Land に入ったあと、最低限この秒数だけ維持します。")]
    [Min(0f)]
    public float landMinimumDuration = 0.08f;

    [Tooltip("JumpStart に入ったあと、最低限この秒数だけ維持します。")]
    [Min(0f)]
    public float jumpStartMinimumDuration = 0.10f;

    [Tooltip("JumpToFall に入ったあと、最低限この秒数だけ維持します。")]
    [Min(0f)]
    public float jumpToFallMinimumDuration = 0.08f;

    [Tooltip("WallJump に入ったあと、最低限この秒数だけ維持します。")]
    [Min(0f)]
    public float wallJumpMinimumDuration = 0.10f;

    [Tooltip("LedgeClimb に入ったあと、最低限この秒数だけ維持します。")]
    [Min(0f)]
    public float ledgeClimbMinimumDuration = 0.10f;

    [Tooltip("Stomp に入ったあと、最低限この秒数だけ維持します。")]
    [Min(0f)]
    public float stompMinimumDuration = 0.05f;

    // 状態ごとの最低維持時間を返す。
    internal float GetMinimumDuration(PlayerAnimationState state)
    {
        switch (state)
        {
            case PlayerAnimationState.Land:
                return landMinimumDuration;

            case PlayerAnimationState.JumpStart:
                return jumpStartMinimumDuration;

            case PlayerAnimationState.JumpToFall:
                return jumpToFallMinimumDuration;

            case PlayerAnimationState.WallJump:
                return wallJumpMinimumDuration;

            case PlayerAnimationState.LedgeClimb:
                return ledgeClimbMinimumDuration;

            case PlayerAnimationState.Stomp:
                return stompMinimumDuration;

            default:
                return 0f;
        }
    }
}

// PlayerAnimationSnapshot をもとに、現在表示すべきモデルアニメーション状態を決めるクラス。
// 責務:
// - Snapshot から desired state を決める
// - minimumDuration を管理する
// - 優先度の高い状態だけロック中でも割り込ませる
//
// 非責務:
// - Animator を直接操作する
// - モデルやメッシュを切り替える
// - Gameplay の状態を変更する
internal sealed class PlayerAnimationResolver
{
    private readonly PlayerAnimationResolverSettings settings;

    private bool hasState;
    private bool wasGrounded = true;
    private bool airborneFromJump;

    private PlayerAnimationState currentState = PlayerAnimationState.Idle;
    private PlayerAnimationState desiredState = PlayerAnimationState.Idle;
    private PlayerAnimationState previousState = PlayerAnimationState.Idle;

    private float currentStateElapsed;
    private float currentStateLockRemaining;

    internal PlayerAnimationState CurrentState => currentState;
    internal PlayerAnimationState DesiredState => desiredState;
    internal PlayerAnimationState PreviousState => previousState;

    internal float CurrentStateElapsed => currentStateElapsed;
    internal float CurrentStateLockRemaining => currentStateLockRemaining;

    internal bool HasState => hasState;
    internal bool EnteredStateThisFrame { get; private set; }

    internal PlayerAnimationResolver(PlayerAnimationResolverSettings settings)
    {
        this.settings = settings ?? new PlayerAnimationResolverSettings();
    }

    // リスポーンや表示切替時に、Resolver の内部状態を初期化する。
    internal void Reset(PlayerAnimationState initialState = PlayerAnimationState.Idle)
    {
        hasState = false;
        wasGrounded = true;
        airborneFromJump = false;

        currentState = initialState;
        desiredState = initialState;
        previousState = initialState;

        currentStateElapsed = 0f;
        currentStateLockRemaining = 0f;
        EnteredStateThisFrame = false;
    }

    // 毎フレーム呼び出し、現在採用すべきアニメーション状態を返す。
    internal PlayerAnimationState Tick(in PlayerAnimationSnapshot snapshot, float deltaTime)
    {
        EnteredStateThisFrame = false;

        UpdateAirborneContext(snapshot);

        desiredState = ResolveDesiredState(snapshot);

        TickStateTransition(desiredState, deltaTime);

        return currentState;
    }

    // 自発ジャンプ由来の空中状態かどうかを更新する。
    // JumpRise / Fall のフォールバック判断や、今後の演出分岐に使う。
    private void UpdateAirborneContext(in PlayerAnimationSnapshot snapshot)
    {
        if (snapshot.justJumped)
        {
            airborneFromJump = true;
        }

        if (!wasGrounded && snapshot.isGrounded)
        {
            airborneFromJump = false;
        }

        if (wasGrounded && !snapshot.isGrounded && !snapshot.justJumped)
        {
            airborneFromJump = false;
        }

        wasGrounded = snapshot.isGrounded;
    }

    // minimumDuration と割り込み可否を見ながら現在状態を確定する。
    private void TickStateTransition(PlayerAnimationState nextDesiredState, float deltaTime)
    {
        if (!hasState)
        {
            ForceEnterState(nextDesiredState);
            return;
        }

        currentStateElapsed += Mathf.Max(0f, deltaTime);
        currentStateLockRemaining = Mathf.Max(
            0f,
            settings.GetMinimumDuration(currentState) - currentStateElapsed);

        if (nextDesiredState == currentState)
        {
            return;
        }

        if (currentStateLockRemaining > 0f &&
            !CanInterruptDuringLock(currentState, nextDesiredState))
        {
            return;
        }

        ForceEnterState(nextDesiredState);
    }

    // 現在状態を強制的に切り替える。
    private void ForceEnterState(PlayerAnimationState nextState)
    {
        hasState = true;
        EnteredStateThisFrame = true;

        previousState = currentState;
        currentState = nextState;

        currentStateElapsed = 0f;
        currentStateLockRemaining = settings.GetMinimumDuration(currentState);
    }

    // Snapshot から理想アニメーション状態を解決する。
    private PlayerAnimationState ResolveDesiredState(in PlayerAnimationSnapshot snapshot)
    {
        // ダッシュは最優先。
        if (snapshot.isDashing)
        {
            return PlayerAnimationState.Dash;
        }

        // 崖乗り上げ中は専用状態。
        if (snapshot.isLedgeClimbing)
        {
            return PlayerAnimationState.LedgeClimb;
        }

        // 壁ジャンプ瞬間。
        if (snapshot.justWallJumped)
        {
            return PlayerAnimationState.WallJump;
        }

        // ストンプ中。
        if (snapshot.isStomping)
        {
            return PlayerAnimationState.Stomp;
        }

        // 壁掴まり中。
        if (snapshot.isWallGrabbing)
        {
            if (Mathf.Abs(snapshot.moveInputY) >= settings.wallClimbInputThreshold)
            {
                return PlayerAnimationState.WallClimb;
            }

            return PlayerAnimationState.WallGrabIdle;
        }

        // 壁滑り中。
        if (snapshot.isWallSliding)
        {
            return PlayerAnimationState.WallSlide;
        }

        // 通常ジャンプ開始瞬間。
        if (snapshot.justJumped && !snapshot.justWallJumped)
        {
            return PlayerAnimationState.JumpStart;
        }

        // 着地瞬間。
        if (snapshot.justLanded)
        {
            return PlayerAnimationState.Land;
        }

        // 頂点通過瞬間。
        if (snapshot.justCrossedApex)
        {
            return PlayerAnimationState.JumpToFall;
        }

        // 非接地かつ上昇中。
        if (!snapshot.isGrounded && snapshot.velocityY > settings.riseVelocityThreshold)
        {
            return PlayerAnimationState.JumpRise;
        }

        // 非接地なら落下。
        if (!snapshot.isGrounded)
        {
            return PlayerAnimationState.Fall;
        }

        // 接地中かつ横移動中。
        if (Mathf.Abs(snapshot.velocityX) >= settings.runVelocityThreshold)
        {
            return PlayerAnimationState.Run;
        }

        return PlayerAnimationState.Idle;
    }

    // minimumDuration 中でも割り込みを許可するか判定する。
    private static bool CanInterruptDuringLock(
        PlayerAnimationState current,
        PlayerAnimationState desired)
    {
        // ダッシュは常に最優先。
        if (desired == PlayerAnimationState.Dash)
        {
            return true;
        }

        // 崖乗り上げに入った場合は、壁掴まりや壁登りの表示を即座に上書きしたい。
        if (desired == PlayerAnimationState.LedgeClimb)
        {
            return true;
        }

        // 壁ジャンプは JumpStart / 空中 / 壁系から即座に上書きしてよい。
        if (desired == PlayerAnimationState.WallJump)
        {
            return current == PlayerAnimationState.JumpStart
                || IsAirNormalState(current)
                || IsWallState(current);
        }

        // ストンプ開始は空中通常状態から即座に反映する。
        if (desired == PlayerAnimationState.Stomp && IsAirNormalState(current))
        {
            return true;
        }

        // 壁掴まり・壁登りは空中通常状態や壁滑りから即座に移行したい。
        if (IsWallGrabState(desired))
        {
            return IsAirNormalState(current)
                || current == PlayerAnimationState.WallSlide;
        }

        // 空中通常状態から壁滑りへ入る遷移は優先して見た目を更新する。
        if (desired == PlayerAnimationState.WallSlide && IsAirNormalState(current))
        {
            return true;
        }

        // 空中通常状態から着地へ入った場合は、着地を見せたい。
        if (desired == PlayerAnimationState.Land && IsAirNormalState(current))
        {
            return true;
        }

        return false;
    }

    private static bool IsAirNormalState(PlayerAnimationState state)
    {
        return state == PlayerAnimationState.JumpStart
            || state == PlayerAnimationState.JumpRise
            || state == PlayerAnimationState.JumpToFall
            || state == PlayerAnimationState.Fall;
    }

    private static bool IsWallGrabState(PlayerAnimationState state)
    {
        return state == PlayerAnimationState.WallGrabIdle
            || state == PlayerAnimationState.WallClimb;
    }

    private static bool IsWallState(PlayerAnimationState state)
    {
        return state == PlayerAnimationState.WallSlide
            || state == PlayerAnimationState.WallGrabIdle
            || state == PlayerAnimationState.WallClimb;
    }
}