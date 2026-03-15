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

    private void Awake()
    {
        // 必須コンポーネントを取得する。
        rb = GetComponent<Rigidbody>();
        capsuleCollider = GetComponent<CapsuleCollider>();

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

        // 横移動、ジャンプ、追加重力、Z軸固定を順に適用する。
        ApplyHorizontalMovement(Time.fixedDeltaTime);
        ApplyJump();
        ApplyCustomGravity();
        LockZAxis();
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
        // 接地していない、またはジャンプ入力がないなら何もしない。
        if (!isGrounded || !playerInputReader.JumpPressed)
        {
            return;
        }

        // 現在速度の Y 成分をジャンプ初速へ置き換える。
        Vector3 velocity = rb.linearVelocity;
        velocity.y = movementSettings.jumpVelocity;
        rb.linearVelocity = velocity;

        // この物理フレームでは地面を離れたものとして扱う。
        isGrounded = false;
    }

    private void ApplyCustomGravity()
    {
        // gravityScale が 1 のときは Unity 標準重力のままなので追加不要。
        if (Mathf.Approximately(movementSettings.gravityScale, 1f))
        {
            return;
        }

        // 標準重力との差分を追加加速度として加える。
        // 例:
        // gravityScale = 2 なら、標準重力 1 個分を追加する。
        Vector3 extraGravity = Physics.gravity * (movementSettings.gravityScale - 1f);
        rb.AddForce(extraGravity, ForceMode.Acceleration);
    }

    private void LockZAxis()
    {
        // 横スクロール前提なので、速度の Z 成分を 0 に戻す。
        Vector3 velocity = rb.linearVelocity;
        if (!Mathf.Approximately(velocity.z, 0f))
        {
            velocity.z = 0f;
            rb.linearVelocity = velocity;
        }

        // 位置の Z 成分も 0 に固定する。
        // これにより物理演算や衝突で奥行き方向へずれるのを防ぐ。
        Vector3 position = rb.position;
        if (!Mathf.Approximately(position.z, 0f))
        {
            position.z = 0f;
            rb.MovePosition(position);
        }
    }
}