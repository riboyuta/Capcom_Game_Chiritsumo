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
    [Header("Input")]
    // 生入力の供給元。
    // 未設定なら Awake で同一 GameObject から取得を試みる。
    [SerializeField] private RawInputSource rawInputSource;

    // プレイヤー操作の入力割り当て定義。
    [SerializeField] private PlayerInputBindings inputBindings = new PlayerInputBindings();

    [Header("Movement")]
    // 移動パラメータ群。
    // 速度・加速度・重力倍率・接地判定距離などを保持する。
    [SerializeField] private PlayerMovementSettings movementSettings = new PlayerMovementSettings();

    // 物理移動本体。
    private Rigidbody rb;

    // 接地判定に使うカプセルコライダー。
    private CapsuleCollider capsuleCollider;

    // 生入力をゲーム用の状態へ変換する入力リーダー。
    private PlayerInputReader playerInputReader;

    private void Awake()
    {
        // 必須コンポーネントを取得する。
        rb = GetComponent<Rigidbody>();
        capsuleCollider = GetComponent<CapsuleCollider>();

        // 疑似 3D 横スク用の拘束を Rigidbody 側でまとめて設定する。
        // 毎フレームの座標補正よりも物理挙動を壊しにくい。
        rb.constraints |= RigidbodyConstraints.FreezePositionZ
            | RigidbodyConstraints.FreezeRotationX
            | RigidbodyConstraints.FreezeRotationY
            | RigidbodyConstraints.FreezeRotationZ;

        // 接地判定は Y 軸カプセル前提。
        // 前提が崩れると Ground 判定位置が不正になるため停止する。
        if (capsuleCollider.direction != 1)
        {
            Debug.LogError("PlayerController requires CapsuleCollider.direction to be Y-axis (1).", this);
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
            Debug.LogError("PlayerController requires RawInputSource.", this);
            enabled = false;
            return;
        }

        // 入力リーダーを生成する。
        playerInputReader = new PlayerInputReader(rawInputSource, inputBindings);

        // Health と Grab システムを初期化する。
        InitializeHealth();
        InitializeGrab();
    }

    private void Update()
    {
        // 初期化失敗時の防御。
        if (playerInputReader == null)
        {
            return;
        }

        // 入力取得は Update で行う。
        playerInputReader.Update();

        // 押下エッジを物理フレームまで保持する。
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
        float deltaTime = Time.deltaTime;
        UpdateHealth(deltaTime);
        UpdateGrab(deltaTime);
    }

    private void FixedUpdate()
    {
        // 初期化失敗時の防御。
        if (playerInputReader == null)
        {
            return;
        }

        float deltaTime = Time.fixedDeltaTime;

        // 掴まれている場合は移動処理をスキップする。
        if (isGrabbed)
        {
            rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);
            return;
        }

        // ノックバック中は専用速度を適用し、通常の移動処理をスキップする。
        if (isKnockback)
        {
            ApplyKnockbackVelocity();
            return;
        }

        // 物理フレームで接地状態を更新する。
        isGrounded = CheckGrounded();
        if (isGrounded)
        {
            isFastFalling = false;
        }

        // 物理フレームで壁接触状態を更新する。
        CheckWallContact();

        // ジャンプ補助タイマーを更新する。
        UpdateJumpAssistTimers(deltaTime);

        // 壁キック入力ロックタイマーを減算する。
        UpdateWallJumpLockTimer(deltaTime);

        // 前ステの継続/クールダウンタイマーを減算する。
        UpdateStepTimers(deltaTime);
        // 前ステ入力バッファタイマーを減算する。
        UpdateStepBufferTimer(deltaTime);
        // 前ステ開始条件を満たす場合は開始する。
        TryStartStep();

        // 前ステップ中は専用速度を最優先し、通常の縦処理を通さない。
        if (isStepping)
        {
            ApplyStepVelocity();
            return;
        }

        // 横移動、ジャンプ、可変ジャンプ、壁滑り、追加重力を順に適用する。
        ApplyHorizontalMovement(deltaTime);
        ApplyJump();
        ApplyVariableJumpCut();
        TryStartFastFall();
        ApplyWallSlide();
        ApplyCustomGravity();
    }
}