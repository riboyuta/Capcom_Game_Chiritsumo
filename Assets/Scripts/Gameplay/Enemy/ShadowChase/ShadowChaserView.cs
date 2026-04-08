using UnityEngine;

[DisallowMultipleComponent]
public sealed class ShadowChaserView : MonoBehaviour
{
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

    [Header("参照: ShadowChaserEnemy")]
    [Tooltip("見た目更新の元になる ShadowChaserEnemy 参照です。未設定時は親階層から自動探索します。")]
    [SerializeField] private ShadowChaserEnemy shadowEnemy;

    [Header("参照: SpriteRenderer")]
    [Tooltip("実際にスプライトを表示する SpriteRenderer です。未設定時は同一 GameObject から自動取得します。")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("参照: ViewRoot")]
    [Tooltip("見た目補正の基準 Transform です。ローカル位置オフセットやスケール補正を適用する対象です。未設定時はこの Transform を使います。")]
    [SerializeField] private Transform viewRoot;

    [Header("判定しきい値: 歩行")]
    [Tooltip("接地中に WalkLoop へ切り替える最小横速度です。絶対値がこの値以上なら歩行扱いになります。")]
    [SerializeField] private float walkThreshold = 0.05f;

    [Header("判定しきい値: 上昇")]
    [Tooltip("空中で JumpRise と判定する最小上向き速度です。Y速度がこの値より大きい間を上昇扱いにします。")]
    [SerializeField] private float riseThreshold = 0.05f;

    [Header("ベース見た目設定: ローカルオフセット")]
    [Tooltip("全 Clip 共通で ViewRoot に適用する基準ローカル位置です。各 Clip の localOffset はこの値に加算されます。")]
    [SerializeField] private Vector3 baseLocalOffset = Vector3.zero;

    [Header("ベース見た目設定: スケール")]
    [Tooltip("全 Clip 共通で ViewRoot に適用する基準スケールです。各 Clip の scaleMultiplier はこの値に乗算されます。")]
    [SerializeField] private Vector3 baseScale = Vector3.one;

    [Header("待機アニメ: Idle")]
    [SerializeField]
    private SpriteSequenceClip idleClip = new SpriteSequenceClip
    {
        playbackMode = SpriteSequencePlaybackMode.HoldLastFrame,
        minimumDuration = 0f
    };

    [Header("歩きアニメ: WalkLoop")]
    [SerializeField]
    private SpriteSequenceClip walkLoopClip = new SpriteSequenceClip
    {
        fps = 16f,
        playbackMode = SpriteSequencePlaybackMode.Loop,
        minimumDuration = 0f
    };

    [Header("地上アニメ: Land")]
    [SerializeField]
    private SpriteSequenceClip landClip = new SpriteSequenceClip
    {
        fps = 10f,
        playbackMode = SpriteSequencePlaybackMode.OneShot,
        minimumDuration = 0.08f
    };

    [Header("ジャンプアニメ: JumpStart")]
    [SerializeField]
    private SpriteSequenceClip jumpStartClip = new SpriteSequenceClip
    {
        fps = 24f,
        playbackMode = SpriteSequencePlaybackMode.OneShot,
        minimumDuration = 0.10f
    };

    [Header("ジャンプ上昇中アニメ: JumpRise")]
    [SerializeField]
    private SpriteSequenceClip jumpRiseClip = new SpriteSequenceClip
    {
        fps = 0f,
        playbackMode = SpriteSequencePlaybackMode.HoldLastFrame,
        minimumDuration = 0f
    };

    [Header("ジャンプ下降中アニメ: JumpToFall")]
    [SerializeField]
    private SpriteSequenceClip jumpToFallClip = new SpriteSequenceClip
    {
        fps = 24f,
        playbackMode = SpriteSequencePlaybackMode.OneShot,
        minimumDuration = 0.08f
    };

    [Header("落下アニメ: Fall")]
    [SerializeField]
    private SpriteSequenceClip fallClip = new SpriteSequenceClip
    {
        fps = 0f,
        playbackMode = SpriteSequencePlaybackMode.HoldLastFrame,
        minimumDuration = 0f
    };

    [Header("壁ずりアニメ: WallSlide")]
    [SerializeField]
    private SpriteSequenceClip wallSlideClip = new SpriteSequenceClip
    {
        fps = 0f,
        playbackMode = SpriteSequencePlaybackMode.HoldLastFrame,
        minimumDuration = 0f
    };

    [Header("壁ジャンプアニメ: WallJump")]
    [SerializeField]
    private SpriteSequenceClip wallJumpClip = new SpriteSequenceClip
    {
        fps = 24f,
        playbackMode = SpriteSequencePlaybackMode.OneShot,
        minimumDuration = 0.10f
    };

    [Header("ブリンクアニメ: Dash")]
    [SerializeField]
    private SpriteSequenceClip dashClip = new SpriteSequenceClip
    {
        fps = 0f,
        playbackMode = SpriteSequencePlaybackMode.HoldLastFrame,
        minimumDuration = 0f
    };

    [Header("デバッグ(Runtime): 現在状態")]
    [SerializeField] private AnimState currentAnimState;

    [Header("デバッグ(Runtime): 要求状態")]
    [SerializeField] private AnimState desiredAnimState;

    [Header("デバッグ(Runtime): 状態経過時間")]
    [SerializeField] private float currentStateElapsed;

    [Header("デバッグ(Runtime): 状態ロック残り時間")]
    [SerializeField] private float currentStateLockRemaining;

    [Header("デバッグ(Runtime): 現在クリップ名")]
    [SerializeField] private string currentClipName = "None";

    [Header("デバッグ(Runtime): 現在フレーム")]
    [SerializeField] private int currentFrameIndex;

    private readonly SpriteSequencePlayer sequencePlayer = new SpriteSequencePlayer();
    private SpriteSequenceClip currentClip;
    private bool hasState;
    private bool wasGrounded = true;
    private bool airborneFromJump;

    public Transform ViewRoot => viewRoot != null ? viewRoot : transform;

    // 初期化処理。
    // ShadowChaserEnemy と SpriteRenderer の参照を取得し、
    // スプライトアニメーションプレイヤーを設定する。
    private void Awake()
    {
        // 未設定なら親階層から ShadowChaserEnemy を取得
        if (shadowEnemy == null)
        {
            shadowEnemy = GetComponentInParent<ShadowChaserEnemy>();
        }

        // 未設定なら同一 GameObject から SpriteRenderer を取得
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        // 未設定なら自身を viewRoot とする
        if (viewRoot == null)
        {
            viewRoot = transform;
        }

        // 必要な参照がすべて揃っているかチェック
        if (shadowEnemy == null || spriteRenderer == null || viewRoot == null)
        {
            Debug.LogError("ShadowChaserView references are missing.", this);
            enabled = false;
            return;
        }

        // スプライトシーケンスプレイヤーを設定
        sequencePlayer.SetRenderer(spriteRenderer);

        // baseScale が未設定なら viewRoot の現在のスケールを使用
        if (baseScale == Vector3.zero)
        {
            baseScale = viewRoot.localScale;
        }
    }

    // 毎フレーム見た目を更新する。
    // ShadowChaserEnemy から snapshot を取得し、適切なアニメーション状態に切り替える。
    private void Update()
    {
        // snapshot がなければ何もしない
        if (!shadowEnemy.HasSnapshot)
        {
            return;
        }

        // 現在のプレイヤー見た目状態を取得
        PlayerController.VisualState state = shadowEnemy.CurrentSnapshot.visualState;

        // 空中コンテキストを更新（ジャンプからの落下かどうかを記憶）
        UpdateAirborneContext(state);

        // 本来のアニメ状態を解決
        desiredAnimState = ResolveDesiredState(state);

        // 状態遷移を処理（最小持続時間などを考慮）
        TickStateTransition(desiredAnimState, Time.deltaTime);

        // スプライトシーケンスプレイヤーを進める
        sequencePlayer.Tick(Time.deltaTime);
        currentFrameIndex = sequencePlayer.CurrentFrame;

        // 見た目補正（向き、スケール、オフセット）を適用
        ApplyVisualCorrection(state);
    }

    // 空中コンテキストを更新する。
    // ジャンプからの空中状態かどうかを記憶し、アニメ選択に役立てる。
    private void UpdateAirborneContext(PlayerController.VisualState state)
    {
        // ジャンプした瞬間にフラグを立てる
        if (state.justJumped)
        {
            airborneFromJump = true;
        }

        // 空中から着地したらフラグを下ろす
        if (!wasGrounded && state.isGrounded)
        {
            airborneFromJump = false;
        }

        // 地上から空中に移行したがジャンプではない場合（落下など）
        if (wasGrounded && !state.isGrounded && !state.justJumped)
        {
            airborneFromJump = false;
        }

        wasGrounded = state.isGrounded;
    }

    private void TickStateTransition(AnimState desired, float deltaTime)
    {
        if (!hasState)
        {
            ForceEnterState(desired);
            return;
        }

        currentStateElapsed += Mathf.Max(0f, deltaTime);
        currentStateLockRemaining = Mathf.Max(0f, (currentClip != null ? currentClip.minimumDuration : 0f) - currentStateElapsed);

        if (desired == currentAnimState)
        {
            return;
        }

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

        currentClip = GetClip(next);
        currentStateLockRemaining = currentClip != null ? currentClip.minimumDuration : 0f;
        currentClipName = currentClip != null ? next.ToString() : "None";

        sequencePlayer.Play(currentClip);
        currentFrameIndex = sequencePlayer.CurrentFrame;
    }

    private bool CanInterruptDuringLock(AnimState current, AnimState desired)
    {
        if (desired == AnimState.Dash)
        {
            return true;
        }

        if (desired == AnimState.WallJump && current == AnimState.JumpStart)
        {
            return true;
        }

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

    // 現在のプレイヤー見た目状態から、適切なアニメ状態を決定する。
    // 優先度の高い順に判定していく。
    private AnimState ResolveDesiredState(PlayerController.VisualState state)
    {
        // ダッシュ中は最優先
        if (state.isDashing && IsClipEnabled(dashClip))
        {
            return AnimState.Dash;
        }

        // 壁ジャンプした瞬間
        if (state.justWallJumped && IsClipEnabled(wallJumpClip))
        {
            return AnimState.WallJump;
        }

        // 壁ずり中
        if (state.isWallSliding && IsClipEnabled(wallSlideClip))
        {
            return AnimState.WallSlide;
        }

        // ジャンプした瞬間（壁ジャンプを除く）
        if (state.justJumped && !state.justWallJumped && IsClipEnabled(jumpStartClip))
        {
            return AnimState.JumpStart;
        }

        // 着地した瞬間
        if (state.justLanded && IsClipEnabled(landClip))
        {
            return AnimState.Land;
        }

        // 頭頂点を越えて下降に切り替わった瞬間
        if (state.justCrossedApex && IsClipEnabled(jumpToFallClip))
        {
            return AnimState.JumpToFall;
        }

        // 上昇中
        if (!state.isGrounded && state.velocityY > riseThreshold)
        {
            if (IsClipEnabled(jumpRiseClip))
            {
                return AnimState.JumpRise;
            }

            // JumpRise が無い場合は JumpStart を使う
            if (airborneFromJump && IsClipEnabled(jumpStartClip))
            {
                return AnimState.JumpStart;
            }

            return hasState ? currentAnimState : AnimState.Idle;
        }

        // 下降中
        if (!state.isGrounded && !state.isWallSliding && !state.isDashing)
        {
            if (IsClipEnabled(fallClip))
            {
                return AnimState.Fall;
            }

            // Fall が無い場合は JumpToFall を使う
            if (airborneFromJump && IsClipEnabled(jumpToFallClip))
            {
                return AnimState.JumpToFall;
            }

            return hasState ? currentAnimState : AnimState.Idle;
        }

        // 地上で移動中
        if (state.isGrounded && Mathf.Abs(state.velocityX) >= walkThreshold && IsClipEnabled(walkLoopClip))
        {
            return AnimState.WalkLoop;
        }

        // それ以外は待機
        if (IsClipEnabled(idleClip))
        {
            return AnimState.Idle;
        }

        return hasState ? currentAnimState : AnimState.Idle;
    }

    // 見た目補正を適用する。
    // 左右反転、スケール、ローカル位置オフセットを設定する。
    private void ApplyVisualCorrection(PlayerController.VisualState state)
    {
        // facing による左右反転
        bool facingFlipX = state.facing < 0;
        bool extraFlipX = currentClip != null && currentClip.extraFlipX;
        bool extraFlipY = currentClip != null && currentClip.extraFlipY;

        // XOR で反転を結合
        spriteRenderer.flipX = facingFlipX ^ extraFlipX;
        spriteRenderer.flipY = extraFlipY;

        // スケールとオフセットを適用
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