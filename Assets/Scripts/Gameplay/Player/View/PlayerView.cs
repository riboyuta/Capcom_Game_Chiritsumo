using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerView : MonoBehaviour
{
    // PlayerView 内で扱う見た目用のアニメーション状態。
    // PlayerController.VisualState から、この列挙へ変換して Clip を選ぶ。
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
        Step
    }

    [Header("参照: PlayerController")]
    [Tooltip("見た目更新の元になる PlayerController 参照です。未設定時は親階層から自動探索します。")]
    // 見た目状態の参照元。
    // 未設定なら Awake で親階層から取得する。
    [SerializeField] private PlayerController playerController;

    [Header("参照: SpriteRenderer")]
    [Tooltip("実際にスプライトを表示する SpriteRenderer です。未設定時は同一 GameObject から自動取得します。")]
    // スプライト表示先。
    // SpriteSequencePlayer がこの Renderer に対して Sprite を差し替える。
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("判定しきい値: 歩行判定")]
    [Tooltip("接地中に WalkLoop へ切り替える最小横速度です。絶対値がこの値以上なら歩行扱いになります。")]
    // 接地中に歩きアニメへ入る最小横速度。
    [SerializeField] private float walkThreshold = 0.05f;

    [Header("判定しきい値: 上昇判定")]
    [Tooltip("空中で JumpRise と判定する最小上向き速度です。Y速度がこの値より大きい間を上昇扱いにします。")]
    // 上昇中とみなす最小 Y 速度。
    [SerializeField] private float riseThreshold = 0.05f;

    [Header("地上アニメ: 待機")]
    [Tooltip("接地中かつ低速時に再生する待機アニメーションです。")]
    // 地上待機用 Clip。
    [SerializeField] private SpriteSequenceClip idleClip;

    [Header("地上アニメ: 歩行ループ")]
    [Tooltip("接地中かつ横移動中に再生する歩行ループアニメーションです。")]
    // 地上歩行用 Clip。
    [SerializeField] private SpriteSequenceClip walkLoopClip;

    [Header("地上アニメ: 着地")]
    [Tooltip("justLanded が立ったフレームで優先再生する着地アニメーションです。")]
    // 着地瞬間用 Clip。
    [SerializeField] private SpriteSequenceClip landClip;

    [Header("空中アニメ: ジャンプ開始")]
    [Tooltip("justJumped が立ったフレームで優先再生するジャンプ開始アニメーションです。")]
    // ジャンプ開始瞬間用 Clip。
    [SerializeField] private SpriteSequenceClip jumpStartClip;

    [Header("空中アニメ: 上昇")]
    [Tooltip("非接地かつ Y速度が riseThreshold を超えている間に再生する上昇アニメーションです。")]
    // 空中上昇用 Clip。
    [SerializeField] private SpriteSequenceClip jumpRiseClip;

    [Header("空中アニメ: 頂点通過")]
    [Tooltip("justCrossedApex が立ったフレームで優先再生する、上昇から落下への切り替えアニメーションです。")]
    // ジャンプ頂点通過瞬間用 Clip。
    [SerializeField] private SpriteSequenceClip jumpToFallClip;

    [Header("空中アニメ: 落下")]
    [Tooltip("非接地かつ上昇条件に当てはまらない間に再生する落下アニメーションです。")]
    // 空中落下用 Clip。
    [SerializeField] private SpriteSequenceClip fallClip;

    [Header("壁アニメ: 壁滑り")]
    [Tooltip("壁滑り中に再生するアニメーションです。")]
    // 壁滑り用 Clip。
    [SerializeField] private SpriteSequenceClip wallSlideClip;

    [Header("壁アニメ: 壁ジャンプ")]
    [Tooltip("justWallJumped が立ったフレームで優先再生する壁ジャンプアニメーションです。")]
    // 壁ジャンプ瞬間用 Clip。
    [SerializeField] private SpriteSequenceClip wallJumpClip;

    [Header("特殊アニメ: ステップ")]
    [Tooltip("前ステップ中に最優先で再生するアニメーションです。")]
    // ステップ用 Clip。
    [SerializeField] private SpriteSequenceClip stepClip;

    [Header("デバッグ(Runtime): 現在アニメ状態")]
    [Tooltip("現在選択されている AnimState 名です。実行時の確認用です。")]
    // 現在の状態名。
    [SerializeField] private string currentAnimState;

    [Header("デバッグ(Runtime): 現在クリップ名")]
    [Tooltip("現在再生中の Clip に対応する名前です。実行時の確認用です。")]
    // 現在再生中 Clip の表示名。
    [SerializeField] private string currentClip;

    [Header("デバッグ(Runtime): 現在フレーム")]
    [Tooltip("現在再生中 Clip のフレーム番号です。実行時の確認用です。")]
    // 現在再生中フレーム。
    [SerializeField] private int currentFrame;

    // 実際の連番再生を担当する最小プレイヤー。
    private readonly SpriteSequencePlayer sequencePlayer = new SpriteSequencePlayer();

    // 現在アクティブな見た目状態。
    private AnimState activeState = AnimState.Idle;

    // 一度でも状態を確定したかどうか。
    // 初回は強制的に Play を流すために使う。
    private bool hasActiveState;

    // デバッグ表示用の現在 Clip 名。
    private string activeClipName = "None";

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

        // View は Controller を参照できないと成立しない。
        if (playerController == null)
        {
            Debug.LogError("PlayerView requires PlayerController in parent hierarchy.", this);
            enabled = false;
            return;
        }

        // 実際の描画先が無いとアニメ表示できない。
        if (spriteRenderer == null)
        {
            Debug.LogError("PlayerView requires SpriteRenderer on the same GameObject.", this);
            enabled = false;
            return;
        }

        // 連番再生先 Renderer を設定する。
        sequencePlayer.SetRenderer(spriteRenderer);
    }

    private void Update()
    {
        if (playerController == null)
        {
            return;
        }

        // Controller 側で確定済みの見た目スナップショットを読む。
        PlayerController.VisualState state = playerController.CurrentVisualState;

        // 向きとアニメ状態を更新する。
        UpdateFacing(state);
        UpdateAnimationState(state);

        // 現在 Clip の再生を進める。
        sequencePlayer.Tick(Time.deltaTime);

        // Runtime デバッグ表示を更新する。
        UpdateDebugFields();
    }

    private void UpdateFacing(PlayerController.VisualState state)
    {
        // facing が負なら左向きとして反転表示する。
        spriteRenderer.flipX = state.facing < 0;
    }

    private void UpdateAnimationState(PlayerController.VisualState state)
    {
        AnimState nextState = ResolveState(state);

        // 初回、または状態変化時だけ Clip を再生し直す。
        if (!hasActiveState || nextState != activeState)
        {
            activeState = nextState;
            hasActiveState = true;
            sequencePlayer.Play(GetClip(nextState));
            activeClipName = nextState.ToString();
        }
    }

    // 現在の見た目状態を AnimState へ変換する。
    // 優先順位は上から順に判定する。
    private AnimState ResolveState(PlayerController.VisualState state)
    {
        // ステップ中は最優先。
        if (state.isStepping)
        {
            return AnimState.Step;
        }

        // 壁ジャンプ瞬間は壁滑りより優先。
        if (state.justWallJumped)
        {
            return AnimState.WallJump;
        }

        // 壁滑り中。
        if (state.isWallSliding)
        {
            return AnimState.WallSlide;
        }

        // 通常ジャンプ開始瞬間。
        if (state.justJumped)
        {
            return AnimState.JumpStart;
        }

        // 着地瞬間。
        if (state.justLanded)
        {
            return AnimState.Land;
        }

        // 頂点通過瞬間。
        if (state.justCrossedApex)
        {
            return AnimState.JumpToFall;
        }

        // 非接地かつ十分な上向き速度があるなら上昇。
        if (!state.isGrounded && state.velocityY > riseThreshold)
        {
            return AnimState.JumpRise;
        }

        // 非接地なら残りは落下。
        if (!state.isGrounded)
        {
            return AnimState.Fall;
        }

        // 接地中で横速度がしきい値以上なら歩行。
        if (state.isGrounded && Mathf.Abs(state.velocityX) >= walkThreshold)
        {
            return AnimState.WalkLoop;
        }

        // それ以外は待機。
        return AnimState.Idle;
    }

    // AnimState から対応する Clip を引く。
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
            case AnimState.Step:
                return stepClip;
            default:
                return idleClip;
        }
    }

    private void UpdateDebugFields()
    {
        currentAnimState = activeState.ToString();
        currentClip = sequencePlayer.CurrentClip != null ? activeClipName : "None";
        currentFrame = sequencePlayer.CurrentFrame;
    }
}