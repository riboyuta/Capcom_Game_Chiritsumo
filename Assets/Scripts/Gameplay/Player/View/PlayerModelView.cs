using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerModelView : MonoBehaviour
{
    [Header("参照: PlayerController")]
    [Tooltip("モデル表示の元になる PlayerController 参照です。未設定時は親階層から自動探索します。")]
    [SerializeField] private PlayerController playerController;

    [Header("参照: Animator")]
    [Tooltip("プレイヤーモデルの Animator です。")]
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

    [Header("デバッグ(Runtime): 壁キック向き固定")]
    [SerializeField] private float wallJumpFacingLockTimer;
    [SerializeField] private int wallJumpLockedFacing = 1;

    private PlayerAnimationState previousDesiredState = PlayerAnimationState.Idle;

    [Header("Animator ステート不足対策")]
    [Tooltip("指定ステートが Animator に存在しない場合、Idle にフォールバックします。")]
    [SerializeField] private bool fallbackToIdleWhenStateMissing = true;

    [Tooltip("Animator にステートが見つからない場合に警告ログを出します。")]
    [SerializeField] private bool warnWhenAnimatorStateMissing = true;

    [Header("アニメーションステート名")]
    [Tooltip("Animator Controller 内の各ステート名です。enum 名と完全一致させるなら初期値のままで使えます。")]
    [SerializeField] private PlayerAnimationStateNameMap stateNames = new PlayerAnimationStateNameMap();

    [Header("状態別見た目補正")]
    [Tooltip("ON の場合、状態ごとのローカル位置・回転・スケール補正を適用します。")]
    [SerializeField] private bool useStateAdjustments = true;

    [Tooltip("状態ごとのモデル補正です。壁掴まり、崖乗り上げなどの見た目合わせに使います。")]
    [SerializeField] private PlayerModelStateAdjustmentMap stateAdjustments = new PlayerModelStateAdjustmentMap();

    [Header("デバッグ(Runtime): 現在状態")]
    [Tooltip("現在要求されているモデルアニメーション状態です。フォールバック再生中でも要求元の状態を表示します。")]
    [SerializeField] private PlayerAnimationState currentState = PlayerAnimationState.Idle;

    [Header("デバッグ(Runtime): 要求状態")]
    [Tooltip("Resolver が今フレーム要求したモデルアニメーション状態です。")]
    [SerializeField] private PlayerAnimationState desiredState = PlayerAnimationState.Idle;

    [Header("デバッグ(Runtime): 実再生ステート名")]
    [Tooltip("実際に Animator へ渡したステート名です。未作成ステート時は Idle などにフォールバックされます。")]
    [SerializeField] private string playingAnimatorStateName = "None";

    [Header("デバッグ(Runtime): フード状態")]
    [Tooltip("現在反映しているフード見た目状態です。")]
    [SerializeField] private PlayerHoodVisualState currentHoodState = PlayerHoodVisualState.Up;

    [Header("デバッグ(Runtime): 状態経過時間")]
    [Tooltip("現在のアニメーション状態に入ってからの経過時間です。")]
    [SerializeField] private float currentStateElapsed;

    [Header("デバッグ(Runtime): 状態ロック残り")]
    [Tooltip("Resolver 側の minimumDuration によるロック残り時間です。")]
    [SerializeField] private float currentStateLockRemaining;

    [Header("UpperBody Layer")]
    [Tooltip("ON の場合、HoodRecover を UpperBody Layer で再生します。")]
    [SerializeField] private bool useUpperBodyLayer = true;

    [Tooltip("上半身アニメーションを再生する Animator レイヤー番号です。Base Layer が 0 なら、通常は 1 です。")]
    [Min(0)]
    [SerializeField] private int upperBodyLayerIndex = 1;

    [Tooltip("HoodRecover_Upper の Animator ステート名です。")]
    [SerializeField] private string hoodRecoverUpperStateName = "HoodRecover_Upper";

    [Tooltip("HoodRecover 再生後に戻す空ステート名です。")]
    [SerializeField] private string upperEmptyStateName = "UpperEmpty";

    [Tooltip("HoodRecover Upper の CrossFade 時間です。")]
    [Min(0f)]
    [SerializeField] private float upperTransitionDuration = 0.04f;

    [Header("HoodRecover Timing")]
    [Tooltip("HoodRecover 開始から何秒後にフード見た目を Up に戻すかです。アニメイベント未使用時の仮タイミングです。")]
    [Min(0f)]
    [SerializeField] private float hoodRecoverSwitchToUpTime = 0.18f;

    [Tooltip("HoodRecover Upper を何秒再生したら UpperEmpty に戻すかです。")]
    [Min(0f)]
    [SerializeField] private float hoodRecoverTotalDuration = 0.45f;

    [Header("デバッグ(Runtime): HoodRecover")]
    [SerializeField] private bool isHoodRecoverPlaying;
    [SerializeField] private float hoodRecoverTimer;
    [SerializeField] private bool hoodRecoverAppliedUp;

    private PlayerAnimationResolver resolver;

    private Vector3 baseModelLocalPosition;
    private Quaternion baseModelLocalRotation;
    private Vector3 baseModelScale = Vector3.one;

    private bool hasAnimatorState;
    private int currentAnimatorStateHash;

    private bool hasMissingStateLog;
    private PlayerAnimationState lastMissingState;

    private void Awake()
    {
        if (playerController == null)
        {
            playerController = GetComponentInParent<PlayerController>();
        }

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        if (modelRoot == null && animator != null)
        {
            modelRoot = animator.transform;
        }

        if (playerController == null || animator == null || modelRoot == null)
        {
            Debug.LogError("PlayerModelView references are missing.", this);
            enabled = false;
            return;
        }

        baseModelLocalPosition = modelRoot.localPosition;
        baseModelLocalRotation = modelRoot.localRotation;
        baseModelScale = modelRoot.localScale;

        resolver = new PlayerAnimationResolver(resolverSettings);

        ApplyHoodState(PlayerHoodVisualState.Up);
        ForcePlayState(PlayerAnimationState.Idle);
    }

    private void OnEnable()
    {
        if (resolver != null)
        {
            resolver.Reset(PlayerAnimationState.Idle);
        }

        hasAnimatorState = false;
        hasMissingStateLog = false;
        playingAnimatorStateName = "None";

        wallJumpFacingLockTimer = 0f;
        wallJumpLockedFacing = 1;
        previousDesiredState = PlayerAnimationState.Idle;
    }

    private void Update()
    {
        if (playerController == null || animator == null || resolver == null)
        {
            return;
        }

        PlayerAnimationSnapshot snapshot = playerController.CurrentAnimationSnapshot;

        desiredState = resolver.Tick(snapshot, Time.deltaTime);
        currentStateElapsed = resolver.CurrentStateElapsed;
        currentStateLockRemaining = resolver.CurrentStateLockRemaining;

        TickWallJumpFacingLock(snapshot.facing, desiredState, Time.deltaTime);

        int displayFacing = ResolveDisplayFacing(snapshot.facing);

        ApplyBaseAnimation(desiredState);
        ApplyModelTransform(displayFacing, desiredState);

        TickHoodRecover(snapshot, Time.deltaTime);
        ApplyHoodState(GetDisplayHoodState(snapshot));
    }

    private void ApplyBaseAnimation(PlayerAnimationState nextState)
    {
        if (!TryResolveAnimatorState(nextState, out int stateHash, out PlayerAnimationState resolvedState, out string resolvedName))
        {
            return;
        }

        if (hasAnimatorState && currentState == nextState && currentAnimatorStateHash == stateHash)
        {
            return;
        }

        float transitionDuration = GetTransitionDuration(nextState);

        animator.CrossFadeInFixedTime(
            stateHash,
            transitionDuration,
            baseLayerIndex);

        currentState = nextState;
        currentAnimatorStateHash = stateHash;
        playingAnimatorStateName = resolvedName;
        hasAnimatorState = true;
    }

    private void ForcePlayState(PlayerAnimationState nextState)
    {
        if (!TryResolveAnimatorState(nextState, out int stateHash, out PlayerAnimationState resolvedState, out string resolvedName))
        {
            return;
        }

        animator.Play(
            stateHash,
            baseLayerIndex,
            0f);

        currentState = nextState;
        currentAnimatorStateHash = stateHash;
        playingAnimatorStateName = resolvedName;
        hasAnimatorState = true;
    }

    private bool TryResolveAnimatorState(
        PlayerAnimationState requestedState,
        out int stateHash,
        out PlayerAnimationState resolvedState,
        out string resolvedStateName)
    {
        resolvedState = requestedState;
        resolvedStateName = stateNames.GetStateName(requestedState);
        stateHash = Animator.StringToHash(resolvedStateName);

        if (!string.IsNullOrEmpty(resolvedStateName) && animator.HasState(baseLayerIndex, stateHash))
        {
            return true;
        }

        LogMissingAnimatorState(requestedState, resolvedStateName);

        if (!fallbackToIdleWhenStateMissing)
        {
            return false;
        }

        resolvedState = PlayerAnimationState.Idle;
        resolvedStateName = stateNames.GetStateName(PlayerAnimationState.Idle);
        stateHash = Animator.StringToHash(resolvedStateName);

        if (!string.IsNullOrEmpty(resolvedStateName) && animator.HasState(baseLayerIndex, stateHash))
        {
            return true;
        }

        LogMissingAnimatorState(PlayerAnimationState.Idle, resolvedStateName);
        return false;
    }

    private void LogMissingAnimatorState(PlayerAnimationState state, string stateName)
    {
        if (!warnWhenAnimatorStateMissing)
        {
            return;
        }

        if (hasMissingStateLog && lastMissingState == state)
        {
            return;
        }

        hasMissingStateLog = true;
        lastMissingState = state;

        Debug.LogWarning(
            $"[PlayerModelView] Animator state not found. State={state}, Name={stateName}, Layer={baseLayerIndex}",
            this);
    }

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

    private void ApplyModelTransform(int facing, PlayerAnimationState state)
    {
        if (modelRoot == null)
        {
            return;
        }

        PlayerModelStateAdjustment adjustment = useStateAdjustments
            ? stateAdjustments.GetAdjustment(state)
            : null;

        Vector3 localOffset = adjustment != null
            ? adjustment.localOffset
            : Vector3.zero;

        Vector3 localEulerAngles = adjustment != null
            ? adjustment.localEulerAngles
            : Vector3.zero;

        float scaleMultiplier = adjustment != null
            ? Mathf.Max(0f, adjustment.scaleMultiplier)
            : 1f;

        bool extraFlipX = adjustment != null && adjustment.extraFlipX;

        modelRoot.localPosition = baseModelLocalPosition + localOffset;
        modelRoot.localRotation = baseModelLocalRotation * Quaternion.Euler(localEulerAngles);

        int safeFacing = facing == 0 ? 1 : (facing > 0 ? 1 : -1);

        if (invertFacing)
        {
            safeFacing *= -1;
        }

        if (extraFlipX)
        {
            safeFacing *= -1;
        }

        Vector3 scale = baseModelScale * scaleMultiplier;

        if (flipByScaleX)
        {
            scale.x = Mathf.Abs(baseModelScale.x) * scaleMultiplier * safeFacing;
        }

        modelRoot.localScale = scale;
    }

    private void TickWallJumpFacingLock(int snapshotFacing, PlayerAnimationState nextState, float deltaTime)
    {
        if (wallJumpFacingLockTimer > 0f)
        {
            wallJumpFacingLockTimer = Mathf.Max(0f, wallJumpFacingLockTimer - Mathf.Max(0f, deltaTime));
        }

        if (!useWallJumpFacingLock)
        {
            previousDesiredState = nextState;
            return;
        }

        bool enteredWallJump =
            nextState == PlayerAnimationState.WallJump &&
            previousDesiredState != PlayerAnimationState.WallJump;

        if (enteredWallJump)
        {
            wallJumpLockedFacing = NormalizeFacing(snapshotFacing);
            wallJumpFacingLockTimer = wallJumpFacingLockDuration;
        }

        previousDesiredState = nextState;
    }

    private int ResolveDisplayFacing(int snapshotFacing)
    {
        if (useWallJumpFacingLock && wallJumpFacingLockTimer > 0f)
        {
            return wallJumpLockedFacing;
        }

        return snapshotFacing;
    }

    private int NormalizeFacing(int facing)
    {
        if (facing == 0)
        {
            return 1;
        }

        return facing > 0 ? 1 : -1;
    }

    private void ApplyHoodState(PlayerHoodVisualState hoodState)
    {
        currentHoodState = hoodState;

        if (hoodUpRoot != null)
        {
            hoodUpRoot.SetActive(hoodState == PlayerHoodVisualState.Up);
        }

        if (hoodDownRoot != null)
        {
            hoodDownRoot.SetActive(hoodState == PlayerHoodVisualState.Down);
        }
    }

    private void TickHoodRecover(PlayerAnimationSnapshot snapshot, float deltaTime)
    {
        if (snapshot.requestHoodRecover && !isHoodRecoverPlaying)
        {
            StartHoodRecover();
        }

        if (!isHoodRecoverPlaying)
        {
            return;
        }

        hoodRecoverTimer += Mathf.Max(0f, deltaTime);

        if (!hoodRecoverAppliedUp && hoodRecoverTimer >= hoodRecoverSwitchToUpTime)
        {
            ApplyHoodRecoverVisualUp();
        }

        if (hoodRecoverTimer >= hoodRecoverTotalDuration)
        {
            EndHoodRecover();
        }
    }

    private void StartHoodRecover()
    {
        isHoodRecoverPlaying = true;
        hoodRecoverTimer = 0f;
        hoodRecoverAppliedUp = false;

        PlayUpperBodyState(hoodRecoverUpperStateName);
    }

    private void ApplyHoodRecoverVisualUp()
    {
        hoodRecoverAppliedUp = true;

        if (playerController != null)
        {
            playerController.CompleteHoodRecoverVisual();
        }
    }

    private void EndHoodRecover()
    {
        isHoodRecoverPlaying = false;
        hoodRecoverTimer = 0f;
        hoodRecoverAppliedUp = false;

        PlayUpperBodyState(upperEmptyStateName);
    }

    private PlayerHoodVisualState GetDisplayHoodState(PlayerAnimationSnapshot snapshot)
    {
        if (isHoodRecoverPlaying && hoodRecoverAppliedUp)
        {
            return PlayerHoodVisualState.Up;
        }

        return snapshot.hoodState;
    }

    private void PlayUpperBodyState(string stateName)
    {
        if (!useUpperBodyLayer)
        {
            return;
        }

        if (animator == null)
        {
            return;
        }

        if (upperBodyLayerIndex < 0 || upperBodyLayerIndex >= animator.layerCount)
        {
            return;
        }

        if (string.IsNullOrEmpty(stateName))
        {
            return;
        }

        int stateHash = Animator.StringToHash(stateName);

        if (!animator.HasState(upperBodyLayerIndex, stateHash))
        {
            Debug.LogWarning(
                $"[PlayerModelView] UpperBody Animator state not found. StateName={stateName}, Layer={upperBodyLayerIndex}",
                this);
            return;
        }

        animator.SetLayerWeight(upperBodyLayerIndex, 1f);
        animator.CrossFadeInFixedTime(
            stateHash,
            upperTransitionDuration,
            upperBodyLayerIndex);
    }
}

// PlayerAnimationState と Animator Controller 内のステート名を対応させる。
// enum 名と Animator ステート名を完全一致させる場合は初期値のままでよい。
[System.Serializable]
internal sealed class PlayerAnimationStateNameMap
{
    [Header("地上")]
    public string idle = "Idle";
    public string run = "Run";
    public string land = "Land";

    [Header("空中")]
    public string jumpStart = "JumpStart";
    public string jumpRise = "JumpRise";
    public string jumpToFall = "JumpToFall";
    public string fall = "Fall";

    [Header("壁")]
    public string wallSlide = "WallSlide";
    public string wallGrabIdle = "WallGrabIdle";
    public string wallClimb = "WallClimb";
    public string wallJump = "WallJump";

    [Header("特殊")]
    public string dash = "Dash";
    public string ledgeClimb = "LedgeClimb";
    public string stomp = "Stomp";

    public string GetStateName(PlayerAnimationState state)
    {
        switch (state)
        {
            case PlayerAnimationState.Idle:
                return idle;

            case PlayerAnimationState.Run:
                return run;

            case PlayerAnimationState.Land:
                return land;

            case PlayerAnimationState.JumpStart:
                return jumpStart;

            case PlayerAnimationState.JumpRise:
                return jumpRise;

            case PlayerAnimationState.JumpToFall:
                return jumpToFall;

            case PlayerAnimationState.Fall:
                return fall;

            case PlayerAnimationState.WallSlide:
                return wallSlide;

            case PlayerAnimationState.WallGrabIdle:
                return wallGrabIdle;

            case PlayerAnimationState.WallClimb:
                return wallClimb;

            case PlayerAnimationState.WallJump:
                return wallJump;

            case PlayerAnimationState.Dash:
                return dash;

            case PlayerAnimationState.LedgeClimb:
                return ledgeClimb;

            case PlayerAnimationState.Stomp:
                return stomp;

            default:
                return idle;
        }
    }
}

// 状態ごとの見た目補正。
// アニメクリップやモデルの原点が合わない場合に、状態単位で調整する。
[System.Serializable]
internal sealed class PlayerModelStateAdjustment
{
    [Tooltip("この状態中に modelRoot へ加算するローカル位置です。")]
    public Vector3 localOffset = Vector3.zero;

    [Tooltip("この状態中に modelRoot へ加算するローカル回転です。")]
    public Vector3 localEulerAngles = Vector3.zero;

    [Tooltip("この状態中に modelRoot の基準スケールへ掛ける倍率です。")]
    [Min(0f)]
    public float scaleMultiplier = 1f;

    [Tooltip("この状態だけ左右反転を追加で反転します。")]
    public bool extraFlipX = false;
}

// PlayerAnimationState ごとの見た目補正テーブル。
// ファイル数を増やさないため、PlayerModelView と同じファイル内に置く。
[System.Serializable]
internal sealed class PlayerModelStateAdjustmentMap
{
    [Header("地上")]
    public PlayerModelStateAdjustment idle = new PlayerModelStateAdjustment();
    public PlayerModelStateAdjustment run = new PlayerModelStateAdjustment();
    public PlayerModelStateAdjustment land = new PlayerModelStateAdjustment();

    [Header("空中")]
    public PlayerModelStateAdjustment jumpStart = new PlayerModelStateAdjustment();
    public PlayerModelStateAdjustment jumpRise = new PlayerModelStateAdjustment();
    public PlayerModelStateAdjustment jumpToFall = new PlayerModelStateAdjustment();
    public PlayerModelStateAdjustment fall = new PlayerModelStateAdjustment();

    [Header("壁")]
    public PlayerModelStateAdjustment wallSlide = new PlayerModelStateAdjustment();
    public PlayerModelStateAdjustment wallGrabIdle = new PlayerModelStateAdjustment();
    public PlayerModelStateAdjustment wallClimb = new PlayerModelStateAdjustment();
    public PlayerModelStateAdjustment wallJump = new PlayerModelStateAdjustment();

    [Header("特殊")]
    public PlayerModelStateAdjustment dash = new PlayerModelStateAdjustment();
    public PlayerModelStateAdjustment ledgeClimb = new PlayerModelStateAdjustment();
    public PlayerModelStateAdjustment stomp = new PlayerModelStateAdjustment();

    public PlayerModelStateAdjustment GetAdjustment(PlayerAnimationState state)
    {
        switch (state)
        {
            case PlayerAnimationState.Idle:
                return idle;

            case PlayerAnimationState.Run:
                return run;

            case PlayerAnimationState.Land:
                return land;

            case PlayerAnimationState.JumpStart:
                return jumpStart;

            case PlayerAnimationState.JumpRise:
                return jumpRise;

            case PlayerAnimationState.JumpToFall:
                return jumpToFall;

            case PlayerAnimationState.Fall:
                return fall;

            case PlayerAnimationState.WallSlide:
                return wallSlide;

            case PlayerAnimationState.WallGrabIdle:
                return wallGrabIdle;

            case PlayerAnimationState.WallClimb:
                return wallClimb;

            case PlayerAnimationState.WallJump:
                return wallJump;

            case PlayerAnimationState.Dash:
                return dash;

            case PlayerAnimationState.LedgeClimb:
                return ledgeClimb;

            case PlayerAnimationState.Stomp:
                return stomp;

            default:
                return idle;
        }
    }
}