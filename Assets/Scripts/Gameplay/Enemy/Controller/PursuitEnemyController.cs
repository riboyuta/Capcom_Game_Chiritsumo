using UnityEngine;

// プレイヤーを追跡する敵のコントローラー
// 追跡と攻撃の2つの状態を持ち、状況に応じて自動で切り替わる
[RequireComponent(typeof(Rigidbody))]
public sealed class PursuitEnemyController : MonoBehaviour
{
    // 敵の行動状態を表す列挙型
    public enum EnemyState
    {
        Chase,     // プレイヤーを追跡中
        Attack     // 攻撃実行中
    }

    [Header("References")]
    [Header("プレイヤーのTransform参照")]
    [SerializeField] private Transform player_transform;                  // プレイヤーのTransform参照
    [Header("攻撃コントローラーの参照")]
    [SerializeField] private EnemyAttackController attack_controller;     // 攻撃コントローラーの参照
    [Header("アニメーターの参照")]
    [SerializeField] private Animator animator;                           // アニメーターの参照

    [Header("Move")]
    [Header("基本移動速度")]
    [SerializeField] private float base_speed = 10.0f;                    // 基本移動速度
    [Header("この距離以下になったら停止（X軸）")]
    [SerializeField] private float stop_distance_x = 0.0f;                // この距離以下になったら停止（X軸）
    [Header("この距離以上離れると追いつき速度ブースト発動")]
    [SerializeField] private float catchup_distance = 50.0f;              // この距離以上離れると追いつき速度ブースト発動
    [Header("ブースト時の速度倍率")]
    [SerializeField] private float catchup_multiplier = 1.75f;            // 追いつき時の速度倍率
    [Header("最大移動速度")]
    [SerializeField] private float max_speed = 20.0f;                     // 最大移動速度

    private float area_speed_multiplier = 1.0f;                           // エリアによる速度倍率（外部から設定可能）

    [Header("Body Contact")]
    [Header("敵の接触判定を有効")]
    [SerializeField] private bool enable_body_contact = true;             // 敵の接触判定を有効にするか
    [Header("接触判定の対象")]
    [SerializeField] private string player_tag = "Player";               // 接触判定の対象となるタグ
    [Header("接触時に送信するメッセージ名")]
    [SerializeField] private string contact_message = "Kill";            // 接触時に送信するメッセージ名

    [Header("Debug")]
    [Header("デバッグログの表示")]
    [SerializeField] private bool show_debug_log = false;                 // デバッグログの表示フラグ

    private Rigidbody rigidbody;                                          // Rigidbodyコンポーネント
    private EnemyContext context;                                         // 敵の情報をまとめたコンテキスト

    private EnemyState state = EnemyState.Chase;                          // 現在の行動状態

    // プロパティ：外部からアクセス可能な読み取り専用情報
    public Transform PlayerTransform => player_transform;        // プレイヤーのTransform
    public Rigidbody EnemyRigidbody => rigidbody;                // 敵のRigidbody
    public Animator EnemyAnimator => animator;                   // 敵のAnimator
    public EnemyState State => state;                            // 現在の状態

    // 初期化処理
    private void Awake()
    {
        rigidbody = GetComponent<Rigidbody>();

        // 疑似3D横スク用の拘束をRigidbody側で設定
        // Z軸の位置と回転を固定し、2D的な動きに制限
        rigidbody.constraints = RigidbodyConstraints.FreezePositionZ
            | RigidbodyConstraints.FreezeRotationX
            | RigidbodyConstraints.FreezeRotationY
            | RigidbodyConstraints.FreezeRotationZ;

        // 攻撃コントローラーが設定されていない場合は自動取得
        if (attack_controller == null)
        {
            attack_controller = GetComponent<EnemyAttackController>();
        }

        context = new EnemyContext();
        RefreshContext();
    }

    // メインループ（毎フレーム呼ばれる）
    // 攻撃の実行と開始判定を行う
    private void Update()
    {
        if (player_transform == null)
        {
            return;
        }

        RefreshContext();

        // 攻撃実行中の場合は攻撃を更新
        if (attack_controller != null && attack_controller.IsAttacking)
        {
            state = EnemyState.Attack;
            attack_controller.TickCurrentAttack(context);
            return;
        }

        // 攻撃可能な場合は攻撃を開始
        if (attack_controller != null)
        {
            bool started_attack = attack_controller.TryStartAttack(context);
            if (started_attack)
            {
                state = EnemyState.Attack;
                LogDebug("Start Attack");
                return;
            }
        }

        // 攻撃していない場合は追跡状態
        state = EnemyState.Chase;
    }

    // 物理演算用の更新処理（固定フレームレートで呼ばれる）
    // プレイヤーの追跡処理を実行
    private void FixedUpdate()
    {
        if (player_transform == null)
        {
            return;
        }

        ChasePlayer();
    }

    // コンテキストを最新の状態に更新
    // 攻撃処理で使用する敵の情報を集約
    private void RefreshContext()
    {
        context.enemy_transform = transform;
        context.player_transform = player_transform;
        context.enemy_rigidbody = rigidbody;
        context.enemy_animator = animator;
        context.enemy_controller = this;
    }

    // プレイヤーを追跡する処理
    private void ChasePlayer()
    {
        float distance_x = GetPlayerDistanceX();

        // 停止距離以下の場合は移動を停止
        if (distance_x <= stop_distance_x)
        {
            StopMove();
            return;
        }

        // 移動速度を計算して適用
        float move_speed = CalculateMoveSpeed(distance_x);
        Vector3 velocity = rigidbody.linearVelocity;
        velocity.x = move_speed;
        rigidbody.linearVelocity = velocity;
    }

    // 移動速度を計算する
    // 基本速度に各種倍率を適用し、最大速度でクランプする
    private float CalculateMoveSpeed(float distance_x)
    {
        float speed = base_speed;

        speed *= CalculateCatchupMultiplier(distance_x);  // 追いつき倍率を適用
        speed *= area_speed_multiplier;                 // エリア速度倍率を適用

        speed = Mathf.Min(speed, max_speed);            // 最大速度でクランプ
        return speed;
    }

    // 追いつき倍率を計算する
    // プレイヤーが遠くに離れすぎた場合に速度ブーストを適用
    private float CalculateCatchupMultiplier(float distance_x)
    {
        // 追いつき距離以上離れている場合は倍率を適用
        if (distance_x >= catchup_distance)
        {
            return catchup_multiplier;
        }

        return 1.0f;
    }

    // 移動を停止する（Y軸の速度は維持）
    public void StopMove()
    {
        Vector3 velocity = rigidbody.linearVelocity;
        velocity.x = 0.0f;
        rigidbody.linearVelocity = velocity;
    }

    // プレイヤーとのX軸方向の距離を取得
    // 正の値 = プレイヤーが右側、負の値 = プレイヤーが左側
    public float GetPlayerDistanceX()
    {
        if (player_transform == null)
        {
            return float.MaxValue;
        }

        return player_transform.position.x - transform.position.x;
    }

    // プレイヤーとのY軸方向の距離を取得（絶対値）
    public float GetPlayerDistanceY()
    {
        if (player_transform == null)
        {
            return float.MaxValue;
        }

        return Mathf.Abs(player_transform.position.y - transform.position.y);
    }

    // プレイヤーのTransformを設定する（外部から呼び出し可能）
    public void SetPlayerTransform(Transform player_transform)
    {
        player_transform = player_transform;
    }

    // エリアによる速度倍率を設定する
    // 特定のエリアで敵の移動速度を変更する際に使用
    public void SetAreaSpeedMultiplier(float multiplier)
    {
        area_speed_multiplier = multiplier;
    }

    // エリア速度倍率をリセット（通常速度に戻す）
    public void ResetAreaSpeedMultiplier()
    {
        area_speed_multiplier = 1.0f;
    }

    // 物理衝突検出（Collisionモード）
    // 通常のCollider同士の衡突時に呼ばれる
    private void OnCollisionEnter(Collision collision)
    {
        if (!enable_body_contact)
        {
            return;
        }

        HandleBodyContact(collision.collider);
    }

    // トリガー衝突検出（Triggerモード）
    // トリガー設定されたColliderとの接触時に呼ばれる
    private void OnTriggerEnter(Collider other)
    {
        if (!enable_body_contact)
        {
            return;
        }

        HandleBodyContact(other);
    }

    // 敵の体当たり判定処理
    // プレイヤーと接触した際に、指定されたメッセージを送信する（例：即死処理）
    private void HandleBodyContact(Collider other)
    {
        // 対象タグでない場合は処理しない
        if (!other.CompareTag(player_tag))
        {
            return;
        }

        // ルートオブジェクトにメッセージを送信（例："Kill"メッセージでプレイヤーを死亡させる）
        other.transform.root.SendMessage(
            contact_message,
            SendMessageOptions.DontRequireReceiver
        );

        LogDebug($"Body Contact : {other.name}");
    }

    // デバッグログを出力（m_show_debug_logがtrueの場合のみ）
    private void LogDebug(string message)
    {
        if (!show_debug_log)
        {
            return;
        }

        Debug.Log($"[PursuitEnemyController] {name} : {message}");
    }

    // Unityエディタでオブジェクト選択時にギズモを描画（デバッグ用）
    // 黄色 = 停止距離、水色 = 追いつき距離
    private void OnDrawGizmosSelected()
    {
        // 停止距離を黄色で表示
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(
            transform.position,
            transform.position + Vector3.right * stop_distance_x
        );

        // 追いつき距離を水色で表示
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(
            transform.position,
            transform.position + Vector3.right * catchup_distance
        );
    }
}

// 敵の情報をまとめたコンテキストクラス
// 攻撃処理で必要な敵とプレイヤーの情報を一箇所にまとめて渡すために使用
public sealed class EnemyContext
{
    public Transform enemy_transform;                   // 敵のTransform
    public Transform player_transform;                  // プレイヤーのTransform
    public Rigidbody enemy_rigidbody;                   // 敵のRigidbody
    public Animator enemy_animator;                     // 敵のAnimator
    public PursuitEnemyController enemy_controller;     // 敵のコントローラー
}