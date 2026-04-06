using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerView : MonoBehaviour
{
    // PlayerView 内で扱う見た目用アニメーション状態。
    // PlayerController.VisualState から、この状態へ変換して Clip を選択する。
    private enum AnimState
    {
        Idle,
        WalkLoop,
        Land,
        JumpStart,
        JumpRise,
        JumpToFall,
        Fall,
        WallSlide,
        WallJump,
        Dash
    }

    [Header("参照: PlayerController")]
    [Tooltip("見た目更新の元になる PlayerController 参照です。未設定時は親階層から自動探索します。")]
    // 見た目状態の参照元。
    [SerializeField] private PlayerController playerController;

    [Header("参照: SpriteRenderer")]
    [Tooltip("実際にスプライトを表示する SpriteRenderer です。未設定時は同一 GameObject から自動取得します。")]
    // スプライト描画先。
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("参照: ViewRoot")]
    [Tooltip("見た目補正の基準 Transform です。ローカル位置オフセットやスケール補正を適用する対象です。未設定時はこの Transform を使います。")]
    // Clip ごとの見た目補正を掛ける基準 Transform。
    [SerializeField] private Transform viewRoot;

    [Header("判定しきい値: 歩行")]
    [Tooltip("接地中に WalkLoop へ切り替える最小横速度です。絶対値がこの値以上なら歩行扱いになります。")]
    // 地上で歩行アニメへ入る最小横速度。
    [SerializeField] private float walkThreshold = 0.05f;

    [Header("判定しきい値: 上昇")]
    [Tooltip("空中で JumpRise と判定する最小上向き速度です。Y速度がこの値より大きい間を上昇扱いにします。")]
    // 上昇中とみなす最小 Y 速度。
    [SerializeField] private float riseThreshold = 0.05f;

    [Header("ベース見た目設定: ローカルオフセット")]
    [Tooltip("全 Clip 共通で ViewRoot に適用する基準ローカル位置です。各 Clip の localOffset はこの値に加算されます。")]
    // 全 Clip 共通の基準ローカル位置。
    [SerializeField] private Vector3 baseLocalOffset = Vector3.zero;

    [Header("ベース見た目設定: スケール")]
    [Tooltip("全 Clip 共通で ViewRoot に適用する基準スケールです。各 Clip の scaleMultiplier はこの値に乗算されます。")]
    // 全 Clip 共通の基準スケール。
    [SerializeField] private Vector3 baseScale = Vector3.one;

    [Header("地上アニメ: Idle")]
    [Tooltip("接地中かつ低速時に再生する待機用 Clip です。未設定または無効なら他状態からのフォールバックに影響します。")]
    // 地上待機用 Clip。
    [SerializeField]
    private SpriteSequenceClip idleClip = new SpriteSequenceClip
    {
        playbackMode = SpriteSequencePlaybackMode.HoldLastFrame,
        minimumDuration = 0f
    };

    [Header("地上アニメ: WalkLoop")]
    [Tooltip("接地中かつ横移動中に再生する歩行ループ用 Clip です。")]
    // 地上歩行用 Clip。
    [SerializeField]
    private SpriteSequenceClip walkLoopClip = new SpriteSequenceClip
    {
        fps = 16f,
        playbackMode = SpriteSequencePlaybackMode.Loop,
        minimumDuration = 0f
    };

    [Header("地上アニメ: Land")]
    [Tooltip("justLanded が立ったときに優先再生する着地用 Clip です。minimumDuration の間は他状態への切り替えを一部抑制します。")]
    // 着地瞬間用 Clip。
    [SerializeField]
    private SpriteSequenceClip landClip = new SpriteSequenceClip
    {
        fps = 10f,
        playbackMode = SpriteSequencePlaybackMode.OneShot,
        minimumDuration = 0.08f
    };

    [Header("空中アニメ: JumpStart")]
    [Tooltip("justJumped が立ったときに優先再生するジャンプ開始用 Clip です。")]
    // ジャンプ開始瞬間用 Clip。
    [SerializeField]
    private SpriteSequenceClip jumpStartClip = new SpriteSequenceClip
    {
        fps = 24f,
        playbackMode = SpriteSequencePlaybackMode.OneShot,
        minimumDuration = 0.10f
    };

    [Header("空中アニメ: JumpRise")]
    [Tooltip("非接地かつ上昇中に再生する上昇用 Clip です。fps が 0 の場合は静止画的に startFrame を維持します。")]
    // 空中上昇用 Clip。
    [SerializeField]
    private SpriteSequenceClip jumpRiseClip = new SpriteSequenceClip
    {
        fps = 0f,
        playbackMode = SpriteSequencePlaybackMode.HoldLastFrame,
        minimumDuration = 0f
    };

    [Header("空中アニメ: JumpToFall")]
    [Tooltip("justCrossedApex が立ったときに優先再生する、上昇から落下への切り替え用 Clip です。")]
    // 頂点通過瞬間用 Clip。
    [SerializeField]
    private SpriteSequenceClip jumpToFallClip = new SpriteSequenceClip
    {
        fps = 24f,
        playbackMode = SpriteSequencePlaybackMode.OneShot,
        minimumDuration = 0.08f
    };

    [Header("空中アニメ: Fall")]
    [Tooltip("非接地かつ上昇条件に当てはまらない間に再生する落下用 Clip です。")]
    // 空中落下用 Clip。
    [SerializeField]
    private SpriteSequenceClip fallClip = new SpriteSequenceClip
    {
        fps = 0f,
        playbackMode = SpriteSequencePlaybackMode.HoldLastFrame,
        minimumDuration = 0f
    };

    [Header("壁アニメ: WallSlide")]
    [Tooltip("壁滑り中に再生する Clip です。通常の空中アニメより優先されます。")]
    // 壁滑り用 Clip。
    [SerializeField]
    private SpriteSequenceClip wallSlideClip = new SpriteSequenceClip
    {
        fps = 0f,
        playbackMode = SpriteSequencePlaybackMode.HoldLastFrame,
        minimumDuration = 0f
    };

    [Header("壁アニメ: WallJump")]
    [Tooltip("justWallJumped が立ったときに優先再生する壁ジャンプ用 Clip です。")]
    // 壁ジャンプ瞬間用 Clip。
    [SerializeField]
    private SpriteSequenceClip wallJumpClip = new SpriteSequenceClip
    {
        fps = 24f,
        playbackMode = SpriteSequencePlaybackMode.OneShot,
        minimumDuration = 0.10f
    };

    [Header("特殊アニメ: Dash")]
    [Tooltip("ダッシュ中に最優先で再生する Clip です。現在状態ロック中でも割り込みを許可します。")]
    // ダッシュ用 Clip。
    [SerializeField]
    private SpriteSequenceClip dashClip = new SpriteSequenceClip
    {
        fps = 0f,
        playbackMode = SpriteSequencePlaybackMode.HoldLastFrame,
        minimumDuration = 0f
    };

    [Header("デバッグ(Runtime): 現在状態")]
    [Tooltip("現在 PlayerView が採用している AnimState です。実行時確認専用です。")]
    // 現在採用中のアニメ状態。
    [SerializeField] private AnimState currentAnimState;

    [Header("デバッグ(Runtime): 要求状態")]
    [Tooltip("VisualState から今フレームに解決された理想 AnimState です。ロック中は currentAnimState と異なる場合があります。")]
    // 現在フレームで本来入りたい状態。
    [SerializeField] private AnimState desiredAnimState;

    [Header("デバッグ(Runtime): 状態経過時間")]
    [Tooltip("現在の状態に入ってから経過した時間(秒)です。minimumDuration 管理の確認に使います。")]
    // 現在状態の経過時間。
    [SerializeField] private float currentStateElapsed;

    [Header("デバッグ(Runtime): 状態ロック残り時間")]
    [Tooltip("現在の Clip の minimumDuration に基づく残りロック時間(秒)です。0 より大きい間は一部の状態遷移が抑制されます。")]
    // 状態ロックの残り時間。
    [SerializeField] private float currentStateLockRemaining;

    [Header("デバッグ(Runtime): 現在クリップ名")]
    [Tooltip("現在再生中の Clip に対応する状態名です。実行時確認専用です。")]
    // 現在再生中 Clip の表示名。
    [SerializeField] private string currentClipName = "None";

    [Header("デバッグ(Runtime): 現在フレーム")]
    [Tooltip("現在再生中 Clip のフレーム番号です。実行時確認専用です。")]
    // 現在フレーム番号。
    [SerializeField] private int currentFrameIndex;

    // 実際の連番再生を担当するプレイヤー。
    private readonly SpriteSequencePlayer sequencePlayer = new SpriteSequencePlayer();

    // 現在再生中の Clip。
    private SpriteSequenceClip currentClip;

    // 一度でも状態を確定したかどうか。
    private bool hasState;

    // 前フレームで接地していたか。
    private bool wasGrounded = true;

    // 現在の空中状態が「自発ジャンプ由来」かを補助的に覚える。
    // 崖落ちとジャンプ上昇を区別したいときに使う。
    private bool airborneFromJump;

    // 見た目演出専用の read-only 参照口。
    // 未設定時は PlayerView 自身の Transform を返して安全側で扱う。
    public Transform ViewRoot => viewRoot != null ? viewRoot : transform;
    private void Awake()
    {
        // PlayerController が未設定なら親階層から補完する。
        if (playerController == null)
        {
            playerController = GetComponentInParent<PlayerController>();
        }

        // SpriteRenderer が未設定なら同一 GameObject から補完する。
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        // ViewRoot が未設定なら自分自身を基準にする。
        if (viewRoot == null)
        {
            viewRoot = transform;
        }

        // 必要参照が足りない場合は動作継続できない。
        if (playerController == null || spriteRenderer == null || viewRoot == null)
        {
            Debug.LogError("PlayerView references are missing.", this);
            enabled = false;
            return;
        }

        // SpriteSequencePlayer の描画先を設定する。
        sequencePlayer.SetRenderer(spriteRenderer);

        // baseScale がゼロなら、現在の ViewRoot スケールを基準値として採用する。
        if (baseScale == Vector3.zero)
        {
            baseScale = viewRoot.localScale;
        }
    }
    
    private void Update()
    {
        // Controller 側で確定済みの見た目スナップショットを読む。
        PlayerController.VisualState state = playerController.CurrentVisualState;

        // 空中文脈を更新する。
        UpdateAirborneContext(state);

        // 現在フレームで理想とする見た目状態を解決する。
        desiredAnimState = ResolveDesiredState(state);

        // 最低再生時間や割り込み条件を見ながら状態遷移を進める。
        TickStateTransition(state, desiredAnimState, Time.deltaTime);

        // 現在 Clip の時間進行を更新する。
        sequencePlayer.Tick(Time.deltaTime);
        currentFrameIndex = sequencePlayer.CurrentFrame;

        // 向き反転・オフセット・スケール補正を適用する。
        ApplyVisualCorrection(state);
    }

    private void UpdateAirborneContext(PlayerController.VisualState state)
    {
        // 自発ジャンプ開始時は「ジャンプ由来の空中状態」とみなす。
        if (state.justJumped)
        {
            airborneFromJump = true;
        }

        // 空中から接地へ戻ったらジャンプ由来フラグを解除する。
        if (!wasGrounded && state.isGrounded)
        {
            airborneFromJump = false;
        }

        // 接地から非接地へ移ったが、その瞬間ジャンプしていないなら
        // 崖落ちなどの可能性が高いのでジャンプ由来ではない扱いにする。
        if (wasGrounded && !state.isGrounded && !state.justJumped)
        {
            airborneFromJump = false;
        }

        wasGrounded = state.isGrounded;
    }

    private void TickStateTransition(PlayerController.VisualState state, AnimState desired, float deltaTime)
    {
        // 初回は無条件で状態へ入る。
        if (!hasState)
        {
            ForceEnterState(desired);
            return;
        }

        // 現在状態の経過時間とロック残時間を更新する。
        currentStateElapsed += Mathf.Max(0f, deltaTime);
        currentStateLockRemaining = Mathf.Max(0f, (currentClip != null ? currentClip.minimumDuration : 0f) - currentStateElapsed);

        // 既に同じ状態なら遷移不要。
        if (desired == currentAnimState)
        {
            return;
        }

        // まだロック中で、かつ現在→次状態への割り込みが許可されないなら遷移しない。
        if (currentStateLockRemaining > 0f && !CanInterruptDuringLock(currentAnimState, desired))
        {
            return;
        }

        ForceEnterState(desired);
    }

    private void ForceEnterState(AnimState next)
    {
        hasState = true;
        currentAnimState = next;
        currentStateElapsed = 0f;

        // 次状態の Clip を取得し、ロック時間を初期化する。
        currentClip = GetClip(next);
        currentStateLockRemaining = currentClip != null ? currentClip.minimumDuration : 0f;
        currentClipName = currentClip != null ? next.ToString() : "None";

        // Clip 再生を開始する。
        sequencePlayer.Play(currentClip);
        currentFrameIndex = sequencePlayer.CurrentFrame;
    }

    private bool CanInterruptDuringLock(AnimState current, AnimState desired)
    {
        // ダッシュは最優先で常に割り込み可能。
        if (desired == AnimState.Dash)
        {
            return true;
        }

        // JumpStart 中に壁ジャンプが入るケースは上書き許可。
        if (desired == AnimState.WallJump && current == AnimState.JumpStart)
        {
            return true;
        }

        // 通常空中状態から壁滑りへ入る遷移は優先して見た目を更新したい。
        if (desired == AnimState.WallSlide && IsAirNormalState(current))
        {
            return true;
        }

        return false;
    }

    private static bool IsAirNormalState(AnimState state)
    {
        return state == AnimState.JumpStart
            || state == AnimState.JumpRise
            || state == AnimState.JumpToFall
            || state == AnimState.Fall;
    }

    private AnimState ResolveDesiredState(PlayerController.VisualState state)
    {
        // ダッシュは最優先。
        if (state.isDashing && IsClipEnabled(dashClip))
        {
            return AnimState.Dash;
        }

        // 壁ジャンプ瞬間。
        if (state.justWallJumped && IsClipEnabled(wallJumpClip))
        {
            return AnimState.WallJump;
        }

        // 壁滑り中。
        if (state.isWallSliding && IsClipEnabled(wallSlideClip))
        {
            return AnimState.WallSlide;
        }

        // 通常ジャンプ開始瞬間。
        if (state.justJumped && !state.justWallJumped && IsClipEnabled(jumpStartClip))
        {
            return AnimState.JumpStart;
        }

        // 着地瞬間。
        if (state.justLanded && IsClipEnabled(landClip))
        {
            return AnimState.Land;
        }

        // 頂点通過瞬間。
        if (state.justCrossedApex && IsClipEnabled(jumpToFallClip))
        {
            return AnimState.JumpToFall;
        }

        // 非接地かつ上向き速度が十分あるなら上昇状態。
        if (!state.isGrounded && state.velocityY > riseThreshold)
        {
            if (IsClipEnabled(jumpRiseClip))
            {
                return AnimState.JumpRise;
            }

            // JumpRise が無い場合は、ジャンプ由来空中なら JumpStart 維持も許可する。
            if (airborneFromJump && IsClipEnabled(jumpStartClip))
            {
                return AnimState.JumpStart;
            }

            return hasState ? currentAnimState : AnimState.Idle;
        }

        // 非接地中で壁滑り / ダッシュでないなら落下系へ。
        if (!state.isGrounded && !state.isWallSliding && !state.isDashing)
        {
            if (IsClipEnabled(fallClip))
            {
                return AnimState.Fall;
            }

            // Fall が無い場合は、ジャンプ由来なら JumpToFall を使う救済。
            if (airborneFromJump && IsClipEnabled(jumpToFallClip))
            {
                return AnimState.JumpToFall;
            }

            return hasState ? currentAnimState : AnimState.Idle;
        }

        // 接地中で横速度が十分あるなら歩行。
        if (state.isGrounded && Mathf.Abs(state.velocityX) >= walkThreshold && IsClipEnabled(walkLoopClip))
        {
            return AnimState.WalkLoop;
        }

        // それ以外は Idle。
        if (IsClipEnabled(idleClip))
        {
            return AnimState.Idle;
        }

        return hasState ? currentAnimState : AnimState.Idle;
    }

    private void ApplyVisualCorrection(PlayerController.VisualState state)
    {
        // facing と Clip 側の追加反転設定を合成して最終反転を決める。
        bool facingFlipX = state.facing < 0;
        bool extraFlipX = currentClip != null && currentClip.extraFlipX;
        bool extraFlipY = currentClip != null && currentClip.extraFlipY;

        spriteRenderer.flipX = facingFlipX ^ extraFlipX;
        spriteRenderer.flipY = extraFlipY;

        // Clip ごとの見た目補正値を基準値へ合成する。
        float scaleMultiplier = currentClip != null ? currentClip.scaleMultiplier : 1f;
        Vector3 localOffset = currentClip != null ? currentClip.localOffset : Vector3.zero;

        viewRoot.localScale = baseScale * scaleMultiplier;
        viewRoot.localPosition = baseLocalOffset + localOffset;
    }

    private SpriteSequenceClip GetClip(AnimState state)
    {
        switch (state)
        {
            case AnimState.Idle:
                return idleClip;
            case AnimState.WalkLoop:
                return walkLoopClip;
            case AnimState.Land:
                return landClip;
            case AnimState.JumpStart:
                return jumpStartClip;
            case AnimState.JumpRise:
                return jumpRiseClip;
            case AnimState.JumpToFall:
                return jumpToFallClip;
            case AnimState.Fall:
                return fallClip;
            case AnimState.WallSlide:
                return wallSlideClip;
            case AnimState.WallJump:
                return wallJumpClip;
            case AnimState.Dash:
                return dashClip;
            default:
                return idleClip;
        }
    }

    private static bool IsClipEnabled(SpriteSequenceClip clip)
    {
        return clip != null && clip.enabled;
    }
}