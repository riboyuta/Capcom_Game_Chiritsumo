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

    // Update で検出したジャンプ押下を FixedUpdate まで保持する。
    // これにより物理フレームとのズレで押下を取りこぼしにくくする。
    private bool jumpRequested;

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

    // デバッグ表示向けの Ground 判定開始位置。
    public Vector3 GroundCheckOrigin => groundCheckOrigin;

    // デバッグ表示向けの Ground 判定半径。
    public float GroundCheckRadius => groundCheckRadius;

    // デバッグ表示向けの Ground 判定距離。
    public float GroundCheckDistance => groundCheckDistance;

    // デバッグ表示向けの Ground 判定ヒット結果。
    public bool GroundCheckHit => groundCheckHit;

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
    }

    private void FixedUpdate()
    {
        // 初期化失敗時の防御。
        if (playerInputReader == null)
        {
            return;
        }

        // 物理フレームで接地状態を更新する。
        isGrounded = CheckGrounded();

        // ジャンプ補助タイマーを更新する。
        UpdateJumpAssistTimers(Time.fixedDeltaTime);

        // 横移動、ジャンプ、可変ジャンプ、追加重力を順に適用する。
        ApplyHorizontalMovement(Time.fixedDeltaTime);
        ApplyJump();
        ApplyVariableJumpCut();
        ApplyCustomGravity();
    }

    private bool CheckGrounded()
    {
        // カプセルの上方向ベクトルを取得する。
        // 通常は Vector3.up と同義だが、回転を考慮して transform.up を使う。
        Vector3 up = transform.up;

        // CapsuleCollider.center はローカル座標なので、
        // ワールド座標へ変換して実際の中心位置を求める。
        Vector3 worldCenter = transform.TransformPoint(capsuleCollider.center);

        // ワールド空間でのカプセル半径を求める。
        float worldRadius = GetWorldCapsuleRadius();

        // カプセルの半高さを求める。
        // height が小さい場合でも radius 未満にならないよう補正する。
        float halfHeight = Mathf.Max(capsuleCollider.height * 0.5f, capsuleCollider.radius);

        // Y スケールを考慮してワールド空間の半高さへ変換する。
        float worldHalfHeight = halfHeight * Mathf.Abs(transform.lossyScale.y);

        // 下側の球の中心を求める。
        // カプセル下端の接地確認の起点として使う。
        Vector3 bottomSphereCenter = worldCenter - up * (worldHalfHeight - worldRadius);

        // 接地確認用の SphereCast 距離。
        // わずかな余裕を足して、接地直前や段差での取りこぼしを減らす。
        float castDistance = movementSettings.groundCheckDistance + 0.01f;

        // 下方向へ SphereCast して、
        // 指定レイヤーの地面が接地距離内にあるかを調べる。
        bool hit = Physics.SphereCast(
            bottomSphereCenter,
            worldRadius * 0.95f,                  // 半径を少し縮めて誤判定を減らす意図
            -up,
            out _,
            castDistance,
            movementSettings.groundLayerMask,
            QueryTriggerInteraction.Ignore);

        // DebugView が利用できるよう、今回の Ground 判定情報を保持する。
        groundCheckOrigin = bottomSphereCenter;
        groundCheckRadius = worldRadius * 0.95f;
        groundCheckDistance = castDistance;
        groundCheckHit = hit;

        return hit;
    }

    private float GetWorldCapsuleRadius()
    {
        // カプセル半径は水平断面の大きさに影響されるため、
        // X/Z スケールの大きい方を使ってワールド半径へ変換する。
        float scaleX = Mathf.Abs(transform.lossyScale.x);
        float scaleZ = Mathf.Abs(transform.lossyScale.z);
        float maxHorizontalScale = Mathf.Max(scaleX, scaleZ);

        // 極端に小さい値にならないよう最小値を設ける。
        return Mathf.Max(0.01f, capsuleCollider.radius * maxHorizontalScale);
    }

    private void UpdateJumpAssistTimers(float deltaTime)
    {
        // 接地中はコヨーテタイムを満タンにし、空中では減算する。
        if (isGrounded)
        {
            coyoteTimer = movementSettings.useCoyoteTime ? movementSettings.coyoteTime : 0f;
        }
        else
        {
            coyoteTimer = Mathf.Max(0f, coyoteTimer - deltaTime);
        }

        // バッファ利用時のみ減算して保持する。
        if (!movementSettings.useJumpBuffer)
        {
            jumpBufferTimer = 0f;
            return;
        }

        jumpBufferTimer = Mathf.Max(0f, jumpBufferTimer - deltaTime);
    }

    private void ApplyHorizontalMovement(float deltaTime)
    {
        // 入力の X 成分を -1 ～ 1 に正規化する。
        float inputX = Mathf.Clamp(playerInputReader.Move.x, -1f, 1f);

        // 入力方向に応じた目標横速度を求める。
        float targetSpeed = inputX * movementSettings.moveMaxSpeed;

        // 移動入力があるかどうかを判定する。
        bool hasMoveInput = Mathf.Abs(inputX) > 0.01f;

        // 入力ありなら加速、入力なしなら減速。
        // さらに地上 / 空中で別パラメータを使い分ける。
        float accel = hasMoveInput
            ? (isGrounded ? movementSettings.groundAcceleration : movementSettings.airAcceleration)
            : (isGrounded ? movementSettings.groundDeceleration : movementSettings.airDeceleration);

        // 現在速度を目標速度へ徐々に近づける。
        Vector3 velocity = rb.linearVelocity;
        velocity.x = Mathf.MoveTowards(velocity.x, targetSpeed, accel * deltaTime);
        rb.linearVelocity = velocity;
    }

    private void ApplyJump()
    {
        // Update 側で拾った要求をこの物理フレームで消費する。
        bool requested = jumpRequested;
        jumpRequested = false;

        // 押下があったフレームでバッファを満たす。
        if (requested && movementSettings.useJumpBuffer)
        {
            jumpBufferTimer = movementSettings.jumpBufferTime;
        }

        // バッファ利用時は jumpBufferTimer、非利用時は当フレーム押下のみで判定する。
        bool hasJumpRequest = movementSettings.useJumpBuffer ? jumpBufferTimer > 0f : requested;
        if (!hasJumpRequest)
        {
            return;
        }

        // コヨーテタイム利用時は coyoteTimer、非利用時は接地中のみで判定する。
        bool canJump = movementSettings.useCoyoteTime ? coyoteTimer > 0f : isGrounded;
        if (!canJump)
        {
            return;
        }

        // 現在速度の Y 成分をジャンプ初速へ置き換える。
        Vector3 velocity = rb.linearVelocity;
        velocity.y = movementSettings.jumpVelocity;
        rb.linearVelocity = velocity;

        // ジャンプ成立後は各種猶予を使い切る。
        isGrounded = false;
        coyoteTimer = 0f;
        jumpBufferTimer = 0f;
    }

    private void ApplyVariableJumpCut()
    {
        // 可変ジャンプを使わない設定なら何もしない。
        if (!movementSettings.useVariableJump)
        {
            return;
        }

        // 押し続け中は上昇を維持する。
        if (playerInputReader.JumpHeld)
        {
            return;
        }

        Vector3 velocity = rb.linearVelocity;

        // 上昇中のみジャンプカットを適用する。
        if (velocity.y <= 0f)
        {
            return;
        }

        // jumpVelocity * jumpCutMultiplier を上向き速度の上限として短押しを表現する。
        float cutVelocityY = movementSettings.jumpVelocity * movementSettings.jumpCutMultiplier;
        velocity.y = Mathf.Min(velocity.y, cutVelocityY);
        rb.linearVelocity = velocity;
    }

    private void ApplyCustomGravity()
    {
        // 落下中は設定倍率に応じて追加重力を強める。
        float gravityMultiplier = movementSettings.gravityScale;
        if (rb.linearVelocity.y < 0f)
        {
            gravityMultiplier *= movementSettings.fallGravityMultiplier;
        }

        // 標準重力との差分を追加加速度として加える。
        if (!Mathf.Approximately(gravityMultiplier, 1f))
        {
            Vector3 extraGravity = Physics.gravity * (gravityMultiplier - 1f);
            rb.AddForce(extraGravity, ForceMode.Acceleration);
        }

        // 落下速度の下限を制限して、加速しすぎを防ぐ。
        Vector3 velocity = rb.linearVelocity;
        float minVelocityY = -movementSettings.maxFallSpeed;
        if (velocity.y < minVelocityY)
        {
            velocity.y = minVelocityY;
            rb.linearVelocity = velocity;
        }
    }
}