using Game.Input;
using UnityEngine;

// 同一 GameObject への多重アタッチを防ぐ。
// PlayerController は 1 つの GameObject に 1 つだけでよい。
[DisallowMultipleComponent]

// 物理移動前提なので Rigidbody を必須にする。
[RequireComponent(typeof(Rigidbody))]

// 接地判定にカプセル形状を使うため CapsuleCollider を必須にする。
[RequireComponent(typeof(CapsuleCollider))]
public sealed partial class PlayerController : MonoBehaviour
{
    [Header("入力: 生入力ソース")]
    [Tooltip("キーボードやゲームパッドなどの生入力を供給するコンポーネントです。未設定時は Awake で同一 GameObject から取得を試みます。")]
    // 生入力の供給元。
    // 未設定なら Awake で同一 GameObject から取得を試みる。
    [SerializeField] private RawInputSource rawInputSource;

    [Header("入力: 割り当て設定")]
    [Tooltip("ジャンプやステップなど、プレイヤー操作に対応する入力割り当て定義です。キーやボタンの対応関係をここで管理します。")]
    // プレイヤー操作の入力割り当て定義。
    // 実際の入力読み取りは PlayerInputReader が行い、
    // この設定は「どの操作をどの入力に対応させるか」を保持する。
    [SerializeField] private PlayerInputBindings inputBindings = new PlayerInputBindings();

    [Header("移動: パラメータ設定")]
    [Tooltip("移動速度、加速度、ジャンプ、重力、壁滑り、前ステップなどの調整値をまとめた設定です。Inspector からプレイ感を調整します。")]
    // 移動パラメータ群。
    // 速度・加速度・重力倍率・接地判定距離などを保持する。
    [SerializeField] private PlayerMovementSettings movementSettings = new PlayerMovementSettings();

    [Header("体力: パラメータ設定")]
    [Tooltip("体力、無敵時間、死亡後復帰待機、デバッグ死亡トリガーをまとめた設定です。")]
    [SerializeField] private PlayerHealthSettings healthSettings = new PlayerHealthSettings();

    // 外部（ギミック等）からの参照用プロパティ
    public PlayerMovementSettings MovementSettings => movementSettings;

    // 物理移動本体。
    // 速度変更、物理拘束、重力挙動などに使う。
    private Rigidbody rb;

    // 接地判定に使うカプセルコライダー。
    // 足元位置や高さ計算の前提として使う。
    private CapsuleCollider capsuleCollider;

    // 生入力をゲーム用の状態へ変換する入力リーダー。
    // 「押された瞬間」「押し続け」「離された」などの判定をここで扱う。
    private PlayerInputReader playerInputReader;

    private void Awake()
    {
        // 必須コンポーネントを取得する。
        rb = GetComponent<Rigidbody>();
        capsuleCollider = GetComponent<CapsuleCollider>();

        // 疑似 3D 横スクロール用の拘束を Rigidbody 側でまとめて設定する。
        // 毎フレームの座標補正よりも物理挙動を壊しにくい。
        rb.constraints |= RigidbodyConstraints.FreezePositionZ
            | RigidbodyConstraints.FreezeRotationX
            | RigidbodyConstraints.FreezeRotationY
            | RigidbodyConstraints.FreezeRotationZ;

        // 接地判定は Y 軸カプセル前提。
        // 前提が崩れると Ground 判定位置が不正になるため停止する。
        if (capsuleCollider.direction != 1)
        {
            Debug.LogError("PlayerController では CapsuleCollider.direction が Y軸 (1) である必要があります。", this);
            enabled = false;
            return;
        }

        // Inspector 未設定なら同一 GameObject から RawInputSource を探す。
        if (rawInputSource == null)
        {
            rawInputSource = GetComponent<RawInputSource>();
        }

        // RawInputSource がないと入力処理ができないため、
        // エラーログを出してこのコンポーネントを無効化する。
        if (rawInputSource == null)
        {
            Debug.LogError("PlayerController には RawInputSource が必要です。", this);
            enabled = false;
            return;
        }
        // 入力リーダーを生成する。
        // ここで「生入力」と「入力割り当て設定」を結び付ける。
        playerInputReader = new PlayerInputReader(rawInputSource, inputBindings);

        // Health と Grab システムを初期化する。
        // Health 側で ReactionState 初期化も行う想定。
        InitializeHealth();

        // 振動関連の比較用状態を初期化する。
        InitializeVibrationState();
    }

    private void Update()
    {
        // 初期化失敗時の防御。
        if (playerInputReader == null)
        {
            return;
        }

        // 入力取得は Update で行う。
        // 物理フレームより高頻度で入力を取りこぼしにくくするため。
        playerInputReader.Update();

        // 押下エッジを物理フレームまで保持する。
        // FixedUpdate 側で安全に消費できるよう、一旦フラグへ積む。
        if (playerInputReader.JumpPressed)
        {
            jumpRequested = true;
        }

        if (playerInputReader.StepPressed)
        {
            stepRequested = true;
        }

        // 横入力がしきい値を超えたときのみ向きを更新する。
        UpdateFacingFromMoveInput();

        // Health と Grab システムを更新する。
        // Health 側で ReactionState 更新も行う想定。
        float deltaTime = Time.deltaTime;
        UpdateHealth(deltaTime);

        // 掴まれ、叩きつけ、死亡などの行動不能状態では
        // 入力を保持せず、その場で打ち切る。
        if (IsActionLocked)
        {
            jumpRequested = false;
            stepRequested = false;
            return;
        }
    }

    private void FixedUpdate()
    {
        // 初期化失敗時の防御。
        if (playerInputReader == null)
        {
            return;
        }

        ResetVisualOneShotFlags();

        float deltaTime = Time.fixedDeltaTime;
        float previousVelocityY = rb != null ? rb.linearVelocity.y : 0f;

        // 掴まれ、叩きつけ、死亡などの行動不能状態では通常移動を止める。
        // 横移動を止め、縦速度だけは物理結果を維持する。
        if (IsActionLocked)
        {
            if (rb != null)
            {
                rb.linearVelocity = new Vector3(0.0f, rb.linearVelocity.y, 0.0f);
            }

            FinalizeVisualState(previousVelocityY);
            return;
        }

        // ノックバック中は専用速度を適用し、通常の移動処理をスキップする。
        if (isKnockback)
        {
            ApplyKnockbackVelocity();
            FinalizeVisualState(previousVelocityY);
            return;
        }

        // 物理フレームで接地状態を更新する。
        isGrounded = CheckGrounded();

        // ApplyJump による isGrounded 上書き前に、着地イベント用の情報を保存する。
        CaptureLandingSnapshot();

        // 接地しているなら急降下状態を解除する。
        // 外部打ち上げ状態は、上昇中でなければ解除する。
        // バネ床で打ち上げ直後はまだ接地判定が残ることがあるため、
        // 上昇中（velocity.y > 0）の場合はフラグを維持する。
        if (isGrounded)
        {
            isFastFalling = false;
            if (rb == null || rb.linearVelocity.y <= 0f)
            {
                isExternalLaunched = false;
            }
        }

        // 物理フレームで壁接触状態を更新する。
        CheckWallContact();

        // ジャンプ補助タイマーを更新する。
        // 例: コヨーテタイム、ジャンプバッファなど。
        UpdateJumpAssistTimers(deltaTime);

        // 壁キック入力ロックタイマーを減算する。
        UpdateWallJumpLockTimer(deltaTime);

        // 前ステの継続時間とクールダウンを更新する。
        UpdateStepTimers(deltaTime);

        // 前ステ入力バッファタイマーを更新する。
        UpdateStepBufferTimer(deltaTime);

        // 前ステ開始条件を満たす場合は開始する。
        TryStartStep();

        // 前ステップ中は専用速度を最優先し、通常の縦処理を通さない。
        if (isStepping)
        {
            ApplyStepVelocity();
            UpdateAudioEvents();
            UpdateVibrationEvents();
            FinalizeVisualState(previousVelocityY);
            return;
        }

        // 追加アクションとしてレール滑走中なら専用フローを優先する
        if (isGrinding)
        {
            ApplyGrindMovement(deltaTime);
            UpdateAudioEvents();
            UpdateVibrationEvents();
            FinalizeVisualState(previousVelocityY);
            return;
        }

        // 通常移動フロー。
        // 横移動、ジャンプ、可変ジャンプ、急降下、壁滑り、追加重力を順に適用する。
        ApplyHorizontalMovement(deltaTime);
        ApplyJump();
        ApplyVariableJumpCut();
        TryStartFastFall();
        ApplyWallSlide();
        ApplyCustomGravity();

        // 状態変化が確定したあとで振動イベントを通知する。
        UpdateAudioEvents();
        UpdateVibrationEvents();
        FinalizeVisualState(previousVelocityY);
    }

    // --- Grind Rail メソッド ---
    public void StartGrind(RailGimmick rail, int segmentIndex, float distanceOnSegment, int direction)
    {
        if (IsActionLocked || isGrinding) return;
        if (railReattachLockTimer > 0f) return;

        isGrinding = true;
        currentRail = rail;
        currentRailSegment = segmentIndex;
        distanceOnRailSegment = distanceOnSegment;
        grindDirection = direction;

        // レール走行開始時に縦や横の余分な重力・速度を一旦切る
        rb.linearVelocity = Vector3.zero;
        isGrounded = false;
        isFastFalling = false;
        
        Debug.Log($"Started Grinding. segment:{segmentIndex}, dir:{direction}");
    }

    private void EndGrind(Vector3 releaseVelocity)
    {
        if (!isGrinding) return;

        isGrinding = false;
        currentRail = null;

        // 離脱時の速度を乗せる
        rb.linearVelocity = releaseVelocity;
        
        // 直後からジャンプなどが効くようにするなどの調整があればここで行う
    }
}