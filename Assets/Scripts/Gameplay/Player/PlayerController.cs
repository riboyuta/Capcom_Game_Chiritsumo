using Game.Input;
using UnityEngine;

// 同一 GameObject への多重アタッチを防ぐ。
// PlayerController は 1 つの GameObject に 1 つだけでよい。
[DisallowMultipleComponent]

// 物理移動前提なので Rigidbody を必須にする。
[RequireComponent(typeof(Rigidbody))]

// 接地判定にカプセル形状を使うため CapsuleCollider を必須にする。
[RequireComponent(typeof(CapsuleCollider))]
public sealed class PlayerController : MonoBehaviour
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

    // 現在接地中かどうか。
    private bool isGrounded;

    // 壁接触中かどうか。
    private bool isTouchingWall;

    // 接触している壁の左右。(-1:left / +1:right / 0:none)
    private int wallSide;

    // 壁キック直後の横入力上書きロックタイマー。
    private float wallJumpControlLockTimer;

    // 現在壁滑り中かどうか。
    private bool isWallSliding;

    // 現在前ステ中かどうか。
    private bool isStepping;

    // 前ステ残り時間。
    private float stepTimer;

    // 前ステクールダウン残り時間。
    private float stepCooldownTimer;

    // 最後に向いていた左右方向。(-1:left / +1:right)
    private int facing = 1;

    // Update で検出したジャンプ押下を FixedUpdate まで保持する。
    // これにより物理フレームとのズレで押下を取りこぼしにくくする。
    private bool jumpRequested;

    // Update で検出した前ステ押下を FixedUpdate まで保持する。
    // これにより物理フレームとのズレで押下を取りこぼしにくくする。
    private bool stepRequested;

    // 着地/クールダウン解除直前の前ステ入力を保持するタイマー。
    private float stepBufferTimer;

    // 床離れ直後でもジャンプ可能にする猶予タイマー。
    private float coyoteTimer;

    // 着地直前のジャンプ入力を保持するタイマー。
    private float jumpBufferTimer;

    // Ground 判定デバッグ可視化用の SphereCast 開始位置。
    private Vector3 groundCheckOrigin;

    // Ground 判定デバッグ可視化用の SphereCast 半径。
    private float groundCheckRadius;

    // Ground 判定デバッグ可視化用の SphereCast 距離。
    private float groundCheckDistance;

    // Ground 判定デバッグ可視化用のヒット結果。
    private bool groundCheckHit;

    // Wall 判定デバッグ可視化用の左右 SphereCast 開始位置。
    private Vector3 leftWallCheckOrigin;
    private Vector3 rightWallCheckOrigin;

    // Wall 判定デバッグ可視化用の SphereCast 半径。
    private float wallCheckRadius;

    // Wall 判定デバッグ可視化用の SphereCast 距離。
    private float wallCheckDistance;

    // Wall 判定デバッグ可視化用の左右ヒット結果。
    private bool leftWallCheckHit;
    private bool rightWallCheckHit;

    // デバッグ表示向けの接地状態。
    public bool IsGrounded => isGrounded;

    // デバッグ表示向けの現在速度。
    public Vector3 CurrentVelocity => rb != null ? rb.linearVelocity : Vector3.zero;

    // デバッグ表示向けのジャンプ要求状態。
    // Update で押下された入力が次の FixedUpdate で消費されるまで true になる。
    public bool JumpRequested => jumpRequested;

    // デバッグ表示向けのコヨーテタイマー。
    public float CoyoteTimer => coyoteTimer;

    // デバッグ表示向けのジャンプバッファタイマー。
    public float JumpBufferTimer => jumpBufferTimer;
    // デバッグ表示向けの前ステ要求状態。
    public bool StepRequested => stepRequested;

    // デバッグ表示向けの前ステバッファタイマー。
    public float StepBufferTimer => stepBufferTimer;
    // デバッグ表示向けの Ground 判定開始位置。
    public Vector3 GroundCheckOrigin => groundCheckOrigin;

    // デバッグ表示向けの Ground 判定半径。
    public float GroundCheckRadius => groundCheckRadius;

    // デバッグ表示向けの Ground 判定距離。
    public float GroundCheckDistance => groundCheckDistance;

    // デバッグ表示向けの Ground 判定ヒット結果。
    public bool GroundCheckHit => groundCheckHit;

    // デバッグ表示向けの壁接触状態。
    public bool IsTouchingWall => isTouchingWall;

    // デバッグ表示向けの壁左右情報。(-1:left / +1:right / 0:none)
    public int WallSide => wallSide;

    // デバッグ表示向けの壁滑り状態。
    public bool IsWallSliding => isWallSliding;

    // デバッグ表示向けの壁キック入力ロックタイマー。
    public float WallJumpControlLockTimer => wallJumpControlLockTimer;

    // デバッグ表示向けの向き。(-1:left / +1:right)
    public int Facing => facing;

    // デバッグ表示向けの前ステ状態。
    public bool IsStepping => isStepping;

    // デバッグ表示向けの前ステ残り時間。
    public float StepTimer => stepTimer;

    // デバッグ表示向けの前ステクールダウン残り時間。
    public float StepCooldownTimer => stepCooldownTimer;

    // デバッグ表示向けの左壁判定開始位置。
    public Vector3 LeftWallCheckOrigin => leftWallCheckOrigin;

    // デバッグ表示向けの右壁判定開始位置。
    public Vector3 RightWallCheckOrigin => rightWallCheckOrigin;

    // デバッグ表示向けの壁判定半径。
    public float WallCheckRadius => wallCheckRadius;

    // デバッグ表示向けの壁判定距離。
    public float WallCheckDistance => wallCheckDistance;

    // デバッグ表示向けの左壁判定ヒット結果。
    public bool LeftWallCheckHit => leftWallCheckHit;

    // デバッグ表示向けの右壁判定ヒット結果。
    public bool RightWallCheckHit => rightWallCheckHit;

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
    }

    private void FixedUpdate()
    {
        // 初期化失敗時の防御。
        if (playerInputReader == null)
        {
            return;
        }

        float deltaTime = Time.fixedDeltaTime;

        // 物理フレームで接地状態を更新する。
        isGrounded = CheckGrounded();

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

        // 横移動、ジャンプ、可変ジャンプ、壁滑り、追加重力を順に適用する。
        ApplyHorizontalMovement(deltaTime);
        ApplyJump();
        ApplyVariableJumpCut();
        ApplyWallSlide();
        ApplyCustomGravity();
    }

    private bool CheckGrounded()
    {
        Vector3 up = transform.up;
        Vector3 worldCenter = transform.TransformPoint(capsuleCollider.center);
        float worldRadius = GetWorldCapsuleRadius();
        float halfHeight = Mathf.Max(capsuleCollider.height * 0.5f, capsuleCollider.radius);
        float worldHalfHeight = halfHeight * Mathf.Abs(transform.lossyScale.y);
        Vector3 bottomSphereCenter = worldCenter - up * (worldHalfHeight - worldRadius);
        float castDistance = movementSettings.groundCheckDistance + 0.01f;

        bool hit = Physics.SphereCast(
            bottomSphereCenter,
            worldRadius * 0.95f,
            -up,
            out _,
            castDistance,
            movementSettings.groundLayerMask,
            QueryTriggerInteraction.Ignore);

        groundCheckOrigin = bottomSphereCenter;
        groundCheckRadius = worldRadius * 0.95f;
        groundCheckDistance = castDistance;
        groundCheckHit = hit;

        return hit;
    }

    private void CheckWallContact()
    {
        Vector3 right = transform.right;
        Vector3 worldCenter = transform.TransformPoint(capsuleCollider.center);
        float worldRadius = GetWorldCapsuleRadius();

        float castRadius = Mathf.Max(0.01f, movementSettings.wallCheckRadius);
        float castDistance = movementSettings.wallCheckDistance + 0.01f;

        leftWallCheckOrigin = worldCenter;
        rightWallCheckOrigin = worldCenter;
        wallCheckRadius = castRadius;
        wallCheckDistance = castDistance;

        bool hitLeft = Physics.SphereCast(
            worldCenter,
            castRadius,
            -right,
            out _,
            castDistance + worldRadius,
            movementSettings.groundLayerMask,
            QueryTriggerInteraction.Ignore);

        bool hitRight = Physics.SphereCast(
            worldCenter,
            castRadius,
            right,
            out _,
            castDistance + worldRadius,
            movementSettings.groundLayerMask,
            QueryTriggerInteraction.Ignore);

        leftWallCheckHit = hitLeft;
        rightWallCheckHit = hitRight;

        if (hitLeft == hitRight)
        {
            wallSide = 0;
            isTouchingWall = false;
            return;
        }

        wallSide = hitLeft ? -1 : 1;
        isTouchingWall = true;
    }

    private float GetWorldCapsuleRadius()
    {
        float scaleX = Mathf.Abs(transform.lossyScale.x);
        float scaleZ = Mathf.Abs(transform.lossyScale.z);
        float maxHorizontalScale = Mathf.Max(scaleX, scaleZ);
        return Mathf.Max(0.01f, capsuleCollider.radius * maxHorizontalScale);
    }

    private void UpdateJumpAssistTimers(float deltaTime)
    {
        if (isGrounded)
        {
            coyoteTimer = movementSettings.useCoyoteTime ? movementSettings.coyoteTime : 0f;
        }
        else
        {
            coyoteTimer = Mathf.Max(0f, coyoteTimer - deltaTime);
        }

        if (!movementSettings.useJumpBuffer)
        {
            jumpBufferTimer = 0f;
            return;
        }

        jumpBufferTimer = Mathf.Max(0f, jumpBufferTimer - deltaTime);
    }

    private void UpdateWallJumpLockTimer(float deltaTime)
    {
        wallJumpControlLockTimer = Mathf.Max(0f, wallJumpControlLockTimer - deltaTime);
    }

    private void UpdateFacingFromMoveInput()
    {
        float inputX = Mathf.Clamp(playerInputReader.Move.x, -1f, 1f);
        const float facingThreshold = 0.1f;

        if (inputX > facingThreshold)
        {
            facing = 1;
        }
        else if (inputX < -facingThreshold)
        {
            facing = -1;
        }
    }

    private void UpdateStepTimers(float deltaTime)
    {
        stepCooldownTimer = Mathf.Max(0f, stepCooldownTimer - deltaTime);

        if (!isStepping)
        {
            stepTimer = 0f;
            return;
        }

        stepTimer = Mathf.Max(0f, stepTimer - deltaTime);
        if (stepTimer <= 0f)
        {
            isStepping = false;
        }
    }

    private void UpdateStepBufferTimer(float deltaTime)
    {
        if (!movementSettings.useStepBuffer)
        {
            stepBufferTimer = 0f;
            return;
        }

        stepBufferTimer = Mathf.Max(0f, stepBufferTimer - deltaTime);
    }
    private void TryStartStep()
    {
        if (!movementSettings.useStep)
        {
            stepRequested = false;
            stepBufferTimer = 0f;
            return;
        }

        bool immediateStepRequest = stepRequested;
        if (immediateStepRequest)
        {
            if (movementSettings.useStepBuffer)
            {
                stepBufferTimer = movementSettings.stepBufferTime;
            }

            stepRequested = false;
        }

        bool hasBufferedStep = movementSettings.useStepBuffer && stepBufferTimer > 0f;
        bool hasStepRequest = immediateStepRequest || hasBufferedStep;
        if (!hasStepRequest)
        {
            return;
        }

        if (isStepping)
        {
            return;
        }

        if (stepCooldownTimer > 0f)
        {
            return;
        }

        if (!isGrounded && !movementSettings.allowAirStep)
        {
            return;
        }

        isStepping = true;
        stepTimer = movementSettings.stepDuration;
        stepCooldownTimer = movementSettings.stepCooldown;


        stepRequested = false;
        stepBufferTimer = 0f;

        jumpRequested = false;
        if (movementSettings.useJumpBuffer)
        {
            jumpBufferTimer = 0f;
        }
    }

    private void ApplyHorizontalMovement(float deltaTime)
    {
        if (isStepping)
        {
            Vector3 steppingVelocity = rb.linearVelocity;
            steppingVelocity.x = facing * movementSettings.stepSpeed;
            rb.linearVelocity = steppingVelocity;
            return;
        }

        float inputX = Mathf.Clamp(playerInputReader.Move.x, -1f, 1f);
        float targetSpeed = inputX * movementSettings.moveMaxSpeed;
        bool hasMoveInput = Mathf.Abs(inputX) > 0.01f;

        float accel = hasMoveInput
            ? (isGrounded ? movementSettings.groundAcceleration : movementSettings.airAcceleration)
            : (isGrounded ? movementSettings.groundDeceleration : movementSettings.airDeceleration);

        // 壁キック直後は入力上書きを抑えて初速を維持する。
        if (wallJumpControlLockTimer > 0f)
        {
            return;
        }

        Vector3 velocity = rb.linearVelocity;
        velocity.x = Mathf.MoveTowards(velocity.x, targetSpeed, accel * deltaTime);
        rb.linearVelocity = velocity;
    }

    private void ApplyJump()
    {
        if (isStepping)
        {
            jumpRequested = false;
            if (movementSettings.useJumpBuffer)
            {
                jumpBufferTimer = 0f;
            }

            return;
        }

        bool requested = jumpRequested;
        jumpRequested = false;

        if (requested && movementSettings.useJumpBuffer)
        {
            jumpBufferTimer = movementSettings.jumpBufferTime;
        }

        bool hasJumpRequest = movementSettings.useJumpBuffer ? jumpBufferTimer > 0f : requested;
        if (!hasJumpRequest)
        {
            return;
        }

        // 非接地 + 壁接触中のジャンプは壁キックを優先する。
        if (TryApplyWallKick())
        {
            jumpBufferTimer = 0f;
            return;
        }

        bool canJump = movementSettings.useCoyoteTime ? coyoteTimer > 0f : isGrounded;
        if (!canJump)
        {
            return;
        }

        Vector3 velocity = rb.linearVelocity;
        velocity.y = movementSettings.jumpVelocity;
        rb.linearVelocity = velocity;

        isGrounded = false;
        coyoteTimer = 0f;
        jumpBufferTimer = 0f;
    }

    private bool TryApplyWallKick()
    {
        if (isStepping)
        {
            return false;
        }

        if (!movementSettings.useWallKick)
        {
            return false;
        }

        if (isGrounded || !isTouchingWall || wallSide == 0)
        {
            return false;
        }

        Vector3 velocity = rb.linearVelocity;
        velocity.x = -wallSide * movementSettings.wallJumpHorizontalVelocity;
        velocity.y = movementSettings.wallJumpVerticalVelocity;
        rb.linearVelocity = velocity;

        wallJumpControlLockTimer = movementSettings.wallJumpControlLockTime;
        coyoteTimer = 0f;
        isGrounded = false;
        return true;
    }

    private void ApplyVariableJumpCut()
    {
        if (!movementSettings.useVariableJump)
        {
            return;
        }

        if (playerInputReader.JumpHeld)
        {
            return;
        }

        Vector3 velocity = rb.linearVelocity;
        if (velocity.y <= 0f)
        {
            return;
        }

        float cutVelocityY = movementSettings.jumpVelocity * movementSettings.jumpCutMultiplier;
        velocity.y = Mathf.Min(velocity.y, cutVelocityY);
        rb.linearVelocity = velocity;
    }

    private void ApplyWallSlide()
    {
        isWallSliding = false;

        if (!movementSettings.useWallSlide)
        {
            return;
        }

        if (isGrounded || !isTouchingWall || wallSide == 0)
        {
            return;
        }

        Vector3 velocity = rb.linearVelocity;
        if (velocity.y >= 0f)
        {
            return;
        }

        float inputX = Mathf.Clamp(playerInputReader.Move.x, -1f, 1f);
        float wallDirection = wallSide;
        bool pushingToWall = inputX * wallDirection >= movementSettings.wallInputThreshold;
        if (!pushingToWall)
        {
            return;
        }

        float minVelocityY = -movementSettings.wallSlideMaxSpeed;
        if (velocity.y < minVelocityY)
        {
            velocity.y = minVelocityY;
            rb.linearVelocity = velocity;
        }

        isWallSliding = true;
    }

    private void ApplyCustomGravity()
    {
        float gravityMultiplier = movementSettings.gravityScale;
        if (rb.linearVelocity.y < 0f)
        {
            gravityMultiplier *= movementSettings.fallGravityMultiplier;
        }

        if (!Mathf.Approximately(gravityMultiplier, 1f))
        {
            Vector3 extraGravity = Physics.gravity * (gravityMultiplier - 1f);
            rb.AddForce(extraGravity, ForceMode.Acceleration);
        }

        Vector3 velocity = rb.linearVelocity;
        float minVelocityY = -movementSettings.maxFallSpeed;
        if (velocity.y < minVelocityY)
        {
            velocity.y = minVelocityY;
            rb.linearVelocity = velocity;
        }
    }
}