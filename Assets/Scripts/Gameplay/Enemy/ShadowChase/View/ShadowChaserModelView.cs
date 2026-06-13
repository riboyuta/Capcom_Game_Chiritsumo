using UnityEngine;

// ShadowChaserEnemy のスナップショットを読み取り、モデルの見た目を更新するビュークラス。
// Transform・アニメーション・フード状態・向き反転・出現演出スケールをここで一括管理する。
[DefaultExecutionOrder(50)]
[DisallowMultipleComponent]
public sealed class ShadowChaserModelView : MonoBehaviour
{
    // =========================================================
    // インスペクター設定 — 参照
    // =========================================================

    [Header("参照: ShadowChaserEnemy")]
    [Tooltip("見た目更新の元になる ShadowChaserEnemy 参照です。未設定時は親階層から自動探索します。")]
    [SerializeField] private ShadowChaserEnemy shadowEnemy;

    [Header("参照: Animator")]
    [Tooltip("影モデルの Animator です。プレイヤーと同じ Animator Controller を使う想定です。")]
    [SerializeField] private Animator animator;

    [Header("参照: ModelRoot")]
    [Tooltip("モデル全体の向き反転・位置補正・スケール補正を行う Transform です。")]
    [SerializeField] private Transform modelRoot;

    [Header("参照: HoodUp Root")]
    [Tooltip("フードを被っている状態のメッシュ Root です。")]
    [SerializeField] private GameObject hoodUpRoot;

    [Header("参照: HoodDown Root")]
    [Tooltip("フードが外れている状態のメッシュ Root です。")]
    [SerializeField] private GameObject hoodDownRoot;

    [Header("参照: Renderer")]
    [Tooltip("表示・非表示を切り替える Renderer 群です。未設定時は子階層から自動取得します。")]
    [SerializeField] private Renderer[] controlledRenderers;

    // =========================================================
    // インスペクター設定 — アニメーション
    // =========================================================

    [Header("アニメーション解決設定")]
    [Tooltip("Snapshot からモデルアニメーション状態を決めるための設定です。")]
    [SerializeField] private PlayerAnimationResolverSettings resolverSettings = new PlayerAnimationResolverSettings();

    [Header("Animator レイヤー")]
    [Tooltip("通常の全身アニメーションを再生する Animator レイヤー番号です。")]
    [Min(0)]
    [SerializeField] private int baseLayerIndex = 0;

    [Header("遷移時間")]
    [Tooltip("通常状態の CrossFade 遷移時間です。")]
    [Min(0f)]
    [SerializeField] private float defaultTransitionDuration = 0.06f;

    [Tooltip("ダッシュなど、即時性が必要な状態の CrossFade 遷移時間です。")]
    [Min(0f)]
    [SerializeField] private float urgentTransitionDuration = 0.02f;

    [Header("アニメーションステート名")]
    [Tooltip("Animator Controller 内の各ステート名です。enum 名と完全一致させるなら初期値のままで使えます。")]
    [SerializeField] private PlayerAnimationStateNameMap stateNames = new PlayerAnimationStateNameMap();

    [Header("Animator ステート不足対策")]
    [Tooltip("指定ステートが Animator に存在しない場合、Idle にフォールバックします。")]
    [SerializeField] private bool fallbackToIdleWhenStateMissing = true;

    [Tooltip("Animator にステートが見つからない場合に警告ログを出します。")]
    [SerializeField] private bool warnWhenAnimatorStateMissing = true;

    // =========================================================
    // インスペクター設定 — モデル Transform
    // =========================================================

    [Header("状態別見た目補正")]
    [Tooltip("ON の場合、状態ごとのローカル位置・回転・スケール補正を適用します。")]
    [SerializeField] private bool useStateAdjustments = true;

    [Tooltip("状態ごとのモデル補正です。壁掴まり、崖乗り上げなどの見た目合わせに使います。")]
    [SerializeField] private PlayerModelStateAdjustmentMap stateAdjustments = new PlayerModelStateAdjustmentMap();

    [Header("出現見た目演出")]
    [Tooltip("ON の場合、出現中だけモデルにスケール・位置補正をかけます。")]
    [SerializeField] private bool useAppearViewEffect = true;

    [Tooltip("出現開始時のローカル位置オフセットです。")]
    [SerializeField] private Vector3 appearStartLocalOffset = new Vector3(0f, -0.25f, 0f);

    [Tooltip("出現開始時のスケール倍率です。")]
    [Min(0f)]
    [SerializeField] private float appearStartScaleMultiplier = 0.65f;

    [Header("向き反転")]
    [Tooltip("ON の場合、facing に応じて modelRoot.localScale.x を反転します。")]
    [SerializeField] private bool flipByScaleX = true;

    [Tooltip("モデルの正面が左向きに作られている場合は ON にします。")]
    [SerializeField] private bool invertFacing = false;

    [Header("壁キック向き固定")]
    [Tooltip("ON の場合、壁キック直後だけ見た目の向きを固定します。")]
    [SerializeField] private bool useWallJumpFacingLock = true;

    [Tooltip("壁キック直後に見た目の向きを固定する時間です。")]
    [Min(0f)]
    [SerializeField] private float wallJumpFacingLockDuration = 0.4f;

    // =========================================================
    // インスペクター設定 — デバッグ表示（Runtime）
    // =========================================================

    [Header("デバッグ(Runtime): 現在状態")]
    [SerializeField] private PlayerAnimationState currentState = PlayerAnimationState.Idle;

    [Header("デバッグ(Runtime): 要求状態")]
    [SerializeField] private PlayerAnimationState desiredState = PlayerAnimationState.Idle;

    [Header("デバッグ(Runtime): 実再生ステート名")]
    [SerializeField] private string playingAnimatorStateName = "None";

    [Header("デバッグ(Runtime): フード状態")]
    [SerializeField] private PlayerHoodVisualState currentHoodState = PlayerHoodVisualState.Up;

    [Header("デバッグ(Runtime): 状態経過時間")]
    [SerializeField] private float currentStateElapsed;

    [Header("デバッグ(Runtime): 状態ロック残り")]
    [SerializeField] private float currentStateLockRemaining;

    [Header("デバッグ(Runtime): 壁キック向き固定")]
    [SerializeField] private float wallJumpFacingLockTimer;
    [SerializeField] private int wallJumpLockedFacing = 1;

    // =========================================================
    // ランタイム状態
    // =========================================================

    private PlayerAnimationResolver resolver;

    // Awake 時点の modelRoot Transform（補正計算の基準値）
    private Vector3    baseModelLocalPosition;
    private Quaternion baseModelLocalRotation;
    private Vector3    baseModelScale = Vector3.one;

    // Animator ステートのキャッシュ（無駄な CrossFade 抑制用）
    private bool hasAnimatorState;
    private int  currentAnimatorStateHash;

    // ステート欠落警告の重複抑制
    private bool                 hasMissingStateLog;
    private PlayerAnimationState lastMissingState;

    // 壁キック検出用（前フレームのステート）
    private PlayerAnimationState previousDesiredState = PlayerAnimationState.Idle;

    // =========================================================
    // Unity ライフサイクル
    // =========================================================

    private void Awake()
    {
        // 未設定の参照を自動解決する
        if (shadowEnemy == null)
            shadowEnemy = GetComponentInParent<ShadowChaserEnemy>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);

        if (modelRoot == null && animator != null)
            modelRoot = animator.transform;

        if (controlledRenderers == null || controlledRenderers.Length == 0)
            controlledRenderers = GetComponentsInChildren<Renderer>(true);

        if (shadowEnemy == null || animator == null || modelRoot == null)
        {
            Debug.LogError("ShadowChaserModelView references are missing.", this);
            enabled = false;
            return;
        }

        // modelRoot の初期 Transform を記憶（補正計算の基準値）
        baseModelLocalPosition = modelRoot.localPosition;
        baseModelLocalRotation = modelRoot.localRotation;
        baseModelScale         = modelRoot.localScale;

        resolver = new PlayerAnimationResolver(resolverSettings);

        ApplyHoodState(PlayerHoodVisualState.Up);
        ForcePlayState(PlayerAnimationState.Idle);
    }

    private void OnValidate()
    {
        baseLayerIndex             = Mathf.Max(0, baseLayerIndex);
        defaultTransitionDuration  = Mathf.Max(0f, defaultTransitionDuration);
        urgentTransitionDuration   = Mathf.Max(0f, urgentTransitionDuration);
        wallJumpFacingLockDuration = Mathf.Max(0f, wallJumpFacingLockDuration);
        appearStartScaleMultiplier = Mathf.Max(0f, appearStartScaleMultiplier);
    }

    private void OnEnable()
    {
        if (resolver != null)
            resolver.Reset(PlayerAnimationState.Idle);

        hasAnimatorState         = false;
        hasMissingStateLog       = false;
        playingAnimatorStateName = "None";

        wallJumpFacingLockTimer  = 0f;
        wallJumpLockedFacing     = 1;
        previousDesiredState     = PlayerAnimationState.Idle;
    }

    private void LateUpdate()
    {
        if (shadowEnemy == null || animator == null || resolver == null)
            return;

        // 非活性中は非表示にして終了する
        bool visible = shadowEnemy.IsActive();
        SetVisible(visible);

        if (!visible)
            return;

        // スナップショットがまだない（出現直前など）は更新しない
        if (!shadowEnemy.HasSnapshot)
            return;

        PlayerShadowSnapshot    shadowSnapshot    = shadowEnemy.CurrentSnapshot;
        PlayerAnimationSnapshot animationSnapshot = shadowSnapshot.animationSnapshot;

        // アニメーション状態を解決する
        desiredState              = resolver.Tick(animationSnapshot, Time.deltaTime);
        currentStateElapsed       = resolver.CurrentStateElapsed;
        currentStateLockRemaining = resolver.CurrentStateLockRemaining;

        // 壁キック直後の向き固定タイマーを更新する
        TickWallJumpFacingLock(animationSnapshot.facing, desiredState, Time.deltaTime);

        int displayFacing = ResolveDisplayFacing(animationSnapshot.facing);

        ApplyBaseAnimation(desiredState);
        ApplyModelTransform(displayFacing, desiredState);
        ApplyHoodState(animationSnapshot.hoodState);
    }

    // =========================================================
    // アニメーション制御
    // =========================================================

    // 前フレームと同じステートなら CrossFade をスキップし、変化があれば遷移する
    private void ApplyBaseAnimation(PlayerAnimationState nextState)
    {
        if (!TryResolveAnimatorState(nextState, out int stateHash, out _, out string resolvedName))
            return;

        if (hasAnimatorState && currentState == nextState && currentAnimatorStateHash == stateHash)
            return;

        animator.CrossFadeInFixedTime(stateHash, GetTransitionDuration(nextState), baseLayerIndex);

        currentState             = nextState;
        currentAnimatorStateHash = stateHash;
        playingAnimatorStateName = resolvedName;
        hasAnimatorState         = true;
    }

    // 遷移なしで即時再生する（初期化時などに使用）
    private void ForcePlayState(PlayerAnimationState nextState)
    {
        if (!TryResolveAnimatorState(nextState, out int stateHash, out _, out string resolvedName))
            return;

        animator.Play(stateHash, baseLayerIndex, 0f);

        currentState             = nextState;
        currentAnimatorStateHash = stateHash;
        playingAnimatorStateName = resolvedName;
        hasAnimatorState         = true;
    }

    // ステートを Hash に解決し、存在しない場合は Idle にフォールバックする
    private bool TryResolveAnimatorState(
        PlayerAnimationState  requestedState,
        out int               stateHash,
        out PlayerAnimationState resolvedState,
        out string            resolvedStateName)
    {
        resolvedState     = requestedState;
        resolvedStateName = stateNames.GetStateName(requestedState);
        stateHash         = Animator.StringToHash(resolvedStateName);

        if (!string.IsNullOrEmpty(resolvedStateName) && animator.HasState(baseLayerIndex, stateHash))
            return true;

        LogMissingAnimatorState(requestedState, resolvedStateName);

        if (!fallbackToIdleWhenStateMissing)
            return false;

        // Idle へフォールバックする
        resolvedState     = PlayerAnimationState.Idle;
        resolvedStateName = stateNames.GetStateName(PlayerAnimationState.Idle);
        stateHash         = Animator.StringToHash(resolvedStateName);

        if (!string.IsNullOrEmpty(resolvedStateName) && animator.HasState(baseLayerIndex, stateHash))
            return true;

        LogMissingAnimatorState(PlayerAnimationState.Idle, resolvedStateName);
        return false;
    }

    // 同一ステートへの重複警告を抑制しつつ LogWarning を出す
    private void LogMissingAnimatorState(PlayerAnimationState state, string stateName)
    {
        if (!warnWhenAnimatorStateMissing)
            return;

        if (hasMissingStateLog && lastMissingState == state)
            return;

        hasMissingStateLog = true;
        lastMissingState   = state;

        Debug.LogWarning(
            $"[ShadowChaserModelView] Animator state not found. State={state}, Name={stateName}, Layer={baseLayerIndex}",
            this);
    }

    // 即時性が必要なステートには短い遷移時間を返す
    private float GetTransitionDuration(PlayerAnimationState state)
    {
        switch (state)
        {
            case PlayerAnimationState.Dash:
            case PlayerAnimationState.WallJump:
            case PlayerAnimationState.LedgeClimb:
            case PlayerAnimationState.Stomp:
                return urgentTransitionDuration;

            default:
                return defaultTransitionDuration;
        }
    }

    // =========================================================
    // モデル Transform 更新
    // =========================================================

    // 状態別補正・出現演出スケール・向き反転を合成して modelRoot に適用する
    private void ApplyModelTransform(int facing, PlayerAnimationState state)
    {
        if (modelRoot == null)
            return;

        // 状態別補正値を取得する（useStateAdjustments が OFF なら null）
        PlayerModelStateAdjustment adjustment = useStateAdjustments
            ? stateAdjustments.GetAdjustment(state)
            : null;

        Vector3 localOffset        = adjustment != null ? adjustment.localOffset      : Vector3.zero;
        Vector3 localEulerAngles   = adjustment != null ? adjustment.localEulerAngles : Vector3.zero;
        float   stateScaleMultiplier = adjustment != null ? Mathf.Max(0f, adjustment.scaleMultiplier) : 1f;
        bool    extraFlipX         = adjustment != null && adjustment.extraFlipX;

        // 出現演出によるオフセット・スケール補正を計算する
        float   appearT              = ResolveAppearViewT();
        Vector3 appearOffset         = useAppearViewEffect ? Vector3.Lerp(appearStartLocalOffset, Vector3.zero, appearT) : Vector3.zero;
        float   appearScaleMultiplier = useAppearViewEffect ? Mathf.Lerp(appearStartScaleMultiplier, 1f, appearT) : 1f;

        modelRoot.localPosition = baseModelLocalPosition + localOffset + appearOffset;
        modelRoot.localRotation = baseModelLocalRotation * Quaternion.Euler(localEulerAngles);

        // facing を正規化し、各フラグによる反転を適用する
        int safeFacing = NormalizeFacing(facing);
        if (invertFacing) safeFacing *= -1;
        if (extraFlipX)   safeFacing *= -1;

        float   finalScaleMultiplier = stateScaleMultiplier * appearScaleMultiplier;
        Vector3 scale                = baseModelScale * finalScaleMultiplier;

        if (flipByScaleX)
            scale.x = Mathf.Abs(baseModelScale.x) * finalScaleMultiplier * safeFacing;

        modelRoot.localScale = scale;
    }

    // 出現演出中なら AppearNormalizedTime を EaseOut した値を、それ以外は 1 を返す
    private float ResolveAppearViewT()
    {
        if (!useAppearViewEffect)
            return 1f;

        if (shadowEnemy == null || !shadowEnemy.IsAppearing)
            return 1f;

        return EaseOutCubic(shadowEnemy.AppearNormalizedTime);
    }

    // =========================================================
    // フード状態
    // =========================================================

    // hoodState に応じて hoodUpRoot / hoodDownRoot の表示を切り替える
    private void ApplyHoodState(PlayerHoodVisualState hoodState)
    {
        currentHoodState = hoodState;

        if (hoodUpRoot   != null) hoodUpRoot.SetActive(hoodState   == PlayerHoodVisualState.Up);
        if (hoodDownRoot != null) hoodDownRoot.SetActive(hoodState == PlayerHoodVisualState.Down);
    }

    // =========================================================
    // 壁キック向き固定
    // =========================================================

    // WallJump への遷移を検出してタイマーをセットし、タイマーを毎フレーム減算する
    private void TickWallJumpFacingLock(int snapshotFacing, PlayerAnimationState nextState, float deltaTime)
    {
        if (wallJumpFacingLockTimer > 0f)
            wallJumpFacingLockTimer = Mathf.Max(0f, wallJumpFacingLockTimer - Mathf.Max(0f, deltaTime));

        if (!useWallJumpFacingLock)
        {
            previousDesiredState = nextState;
            return;
        }

        // WallJump に入ったフレームだけ向きをロックする
        bool enteredWallJump =
            nextState == PlayerAnimationState.WallJump &&
            previousDesiredState != PlayerAnimationState.WallJump;

        if (enteredWallJump)
        {
            wallJumpLockedFacing    = NormalizeFacing(snapshotFacing);
            wallJumpFacingLockTimer = wallJumpFacingLockDuration;
        }

        previousDesiredState = nextState;
    }

    // タイマーが残っている間はロック済みの向きを返す
    private int ResolveDisplayFacing(int snapshotFacing)
    {
        if (useWallJumpFacingLock && wallJumpFacingLockTimer > 0f)
            return wallJumpLockedFacing;

        return snapshotFacing;
    }

    // =========================================================
    // ユーティリティ
    // =========================================================

    // facing を -1 / +1 に正規化する（0 は +1 扱い）
    private int NormalizeFacing(int facing)
    {
        if (facing == 0) return 1;
        return facing > 0 ? 1 : -1;
    }

    // controlledRenderers の enabled を一括切り替えする
    private void SetVisible(bool visible)
    {
        if (controlledRenderers == null)
            return;

        for (int i = 0; i < controlledRenderers.Length; ++i)
        {
            if (controlledRenderers[i] != null)
                controlledRenderers[i].enabled = visible;
        }
    }

    // 3 次イーズアウト（1 − (1−t)³）
    private static float EaseOutCubic(float t)
    {
        t = Mathf.Clamp01(t);
        float inv = 1f - t;
        return 1f - inv * inv * inv;
    }
}