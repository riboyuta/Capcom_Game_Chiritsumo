using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerView : MonoBehaviour
{
    [System.Serializable]
    private sealed class SpriteClipData
    {
        [Tooltip("この状態で再生するスプライト連番クリップです。")]
        public SpriteSequenceClip clip;

        [Tooltip("通常の向き反転に加算する、クリップ固有の X 反転です。")]
        public bool extraFlipX;

        [Tooltip("クリップ固有の Y 反転です。")]
        public bool extraFlipY;

        [Tooltip("ViewRoot の基準スケールへ乗算する倍率です。")]
        [Min(0f)]
        public float scaleMultiplier = 1f;

        [Tooltip("ViewRoot の基準ローカル座標へ加算する補正です。")]
        public Vector3 localOffset;
    }

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

    [Header("参照: ViewRoot")]
    [Tooltip("見た目補正を適用する Transform です。未設定時はこの GameObject の Transform を使います。")]
    [SerializeField] private Transform viewRoot;

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
    [SerializeField] private SpriteClipData idleClipData;

    [Header("地上アニメ: 歩行ループ")]
    [Tooltip("接地中かつ横移動中に再生する歩行ループアニメーションです。")]
    // 地上歩行用 Clip。
    [SerializeField] private SpriteClipData walkLoopClipData;

    [Header("地上アニメ: 着地")]
    [Tooltip("justLanded が立ったフレームで優先再生する着地アニメーションです。")]
    // 着地瞬間用 Clip。
    [SerializeField] private SpriteClipData landClipData;

    [Header("空中アニメ: ジャンプ開始")]
    [Tooltip("justJumped が立ったフレームで優先再生するジャンプ開始アニメーションです。")]
    // ジャンプ開始瞬間用 Clip。
    [SerializeField] private SpriteClipData jumpStartClipData;

    [Header("空中アニメ: 上昇")]
    [Tooltip("非接地かつ Y速度が riseThreshold を超えている間に再生する上昇アニメーションです。")]
    // 空中上昇用 Clip。
    [SerializeField] private SpriteClipData jumpRiseClipData;

    [Header("空中アニメ: 頂点通過")]
    [Tooltip("justCrossedApex が立ったフレームで優先再生する、上昇から落下への切り替えアニメーションです。")]
    // ジャンプ頂点通過瞬間用 Clip。
    [SerializeField] private SpriteClipData jumpToFallClipData;

    [Header("空中アニメ: 落下")]
    [Tooltip("非接地かつ上昇条件に当てはまらない間に再生する落下アニメーションです。")]
    // 空中落下用 Clip。
    [SerializeField] private SpriteClipData fallClipData;

    [Header("壁アニメ: 壁滑り")]
    [Tooltip("壁滑り中に再生するアニメーションです。")]
    // 壁滑り用 Clip。
    [SerializeField] private SpriteClipData wallSlideClipData;

    [Header("壁アニメ: 壁ジャンプ")]
    [Tooltip("justWallJumped が立ったフレームで優先再生する壁ジャンプアニメーションです。")]
    // 壁ジャンプ瞬間用 Clip。
    [SerializeField] private SpriteClipData wallJumpClipData;

    [Header("特殊アニメ: ステップ")]
    [Tooltip("前ステップ中に最優先で再生するアニメーションです。")]
    // ステップ用 Clip。
    [SerializeField] private SpriteClipData stepClipData;

    // 旧バージョン互換: 既存シーンの Clip 参照を SpriteClipData へ移すための退避領域。
    [SerializeField, HideInInspector] private SpriteSequenceClip idleClip;
    [SerializeField, HideInInspector] private SpriteSequenceClip walkLoopClip;
    [SerializeField, HideInInspector] private SpriteSequenceClip landClip;
    [SerializeField, HideInInspector] private SpriteSequenceClip jumpStartClip;
    [SerializeField, HideInInspector] private SpriteSequenceClip jumpRiseClip;
    [SerializeField, HideInInspector] private SpriteSequenceClip jumpToFallClip;
    [SerializeField, HideInInspector] private SpriteSequenceClip fallClip;
    [SerializeField, HideInInspector] private SpriteSequenceClip wallSlideClip;
    [SerializeField, HideInInspector] private SpriteSequenceClip wallJumpClip;
    [SerializeField, HideInInspector] private SpriteSequenceClip stepClip;

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

    // ViewRoot の初期ローカルスケール。
    private Vector3 baseScale = Vector3.one;

    // ViewRoot の初期ローカル座標。
    private Vector3 baseOffset = Vector3.zero;

    private void Awake()
    {
        MigrateLegacyClipReferences();

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

        // ViewRoot が未設定なら自身の Transform を使う。
        if (viewRoot == null)
        {
            viewRoot = transform;
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

        if (viewRoot == null)
        {
            Debug.LogError("PlayerView requires ViewRoot Transform.", this);
            enabled = false;
            return;
        }

        // 連番再生先 Renderer を設定する。
        sequencePlayer.SetRenderer(spriteRenderer);

        // 基準見た目補正を保存する。
        baseScale = viewRoot.localScale;
        baseOffset = viewRoot.localPosition;
    }

    private void OnValidate()
    {
        MigrateLegacyClipReferences();
    }

    private void Update()
    {
        if (playerController == null)
        {
            return;
        }

        // Controller 側で確定済みの見た目スナップショットを読む。
        PlayerController.VisualState state = playerController.CurrentVisualState;

        // アニメ状態を更新する。
        UpdateAnimationState(state);
        UpdateFacingAndViewCorrection(state);

        // 現在 Clip の再生を進める。
        sequencePlayer.Tick(Time.deltaTime);

        // Runtime デバッグ表示を更新する。
        UpdateDebugFields();
    }

    private void UpdateFacingAndViewCorrection(PlayerController.VisualState state)
    {
        SpriteClipData activeClipData = GetClipData(activeState);

        // facing が負なら左向き。クリップ固有反転は XOR で加算する。
        bool facingFlipX = state.facing < 0;
        bool extraFlipX = activeClipData != null && activeClipData.extraFlipX;
        bool extraFlipY = activeClipData != null && activeClipData.extraFlipY;
        spriteRenderer.flipX = facingFlipX ^ extraFlipX;
        spriteRenderer.flipY = extraFlipY;

        // 見た目補正は ViewRoot に適用する（PlayerRoot は変更しない）。
        float scaleMultiplier = activeClipData != null ? activeClipData.scaleMultiplier : 1f;
        Vector3 localOffset = activeClipData != null ? activeClipData.localOffset : Vector3.zero;
        viewRoot.localScale = baseScale * scaleMultiplier;
        viewRoot.localPosition = baseOffset + localOffset;
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
        SpriteClipData clipData = GetClipData(state);
        return clipData != null ? clipData.clip : null;
    }

    private SpriteClipData GetClipData(AnimState state)
    {
        switch (state)
        {
            case AnimState.Idle:
                return idleClipData;
            case AnimState.WalkLoop:
                return walkLoopClipData;
            case AnimState.Land:
                return landClipData;
            case AnimState.JumpStart:
                return jumpStartClipData;
            case AnimState.JumpRise:
                return jumpRiseClipData;
            case AnimState.JumpToFall:
                return jumpToFallClipData;
            case AnimState.Fall:
                return fallClipData;
            case AnimState.WallSlide:
                return wallSlideClipData;
            case AnimState.WallJump:
                return wallJumpClipData;
            case AnimState.Step:
                return stepClipData;
            default:
                return idleClipData;
        }
    }

    private void UpdateDebugFields()
    {
        currentAnimState = activeState.ToString();
        currentClip = sequencePlayer.CurrentClip != null ? activeClipName : "None";
        currentFrame = sequencePlayer.CurrentFrame;
    }

    private void MigrateLegacyClipReferences()
    {
        MigrateLegacyClipReference(ref idleClipData, idleClip);
        MigrateLegacyClipReference(ref walkLoopClipData, walkLoopClip);
        MigrateLegacyClipReference(ref landClipData, landClip);
        MigrateLegacyClipReference(ref jumpStartClipData, jumpStartClip);
        MigrateLegacyClipReference(ref jumpRiseClipData, jumpRiseClip);
        MigrateLegacyClipReference(ref jumpToFallClipData, jumpToFallClip);
        MigrateLegacyClipReference(ref fallClipData, fallClip);
        MigrateLegacyClipReference(ref wallSlideClipData, wallSlideClip);
        MigrateLegacyClipReference(ref wallJumpClipData, wallJumpClip);
        MigrateLegacyClipReference(ref stepClipData, stepClip);
    }

    private static void MigrateLegacyClipReference(ref SpriteClipData clipData, SpriteSequenceClip legacyClip)
    {
        if (clipData == null)
        {
            clipData = new SpriteClipData();
        }

        if (clipData.clip == null && legacyClip != null)
        {
            clipData.clip = legacyClip;
        }
    }
}