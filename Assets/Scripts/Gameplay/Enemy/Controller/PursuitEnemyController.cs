using UnityEngine;

/// <summary>
/// プレイヤーを追跡する敵のコントローラー
/// 追跡と攻撃の2つの状態を持ち、状況に応じて自動で切り替わる
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public sealed class PursuitEnemyController : MonoBehaviour
{
    // 敵の行動状態を表す列挙型
    public enum EnemyState
    {
        Chase,     // プレイヤーを追跡中
        Attack     // 攻撃実行中
    }

    [Header("References")]
    [SerializeField] private Transform m_player_transform;                  // プレイヤーのTransform参照
    [SerializeField] private EnemyAttackController m_attack_controller;     // 攻撃コントローラーの参照
    [SerializeField] private Animator m_animator;                           // アニメーターの参照

    [Header("Move")]
    [SerializeField] private float m_base_speed = 10.0f;                    // 基本移動速度
    [SerializeField] private float m_stop_distance_x = 0.0f;                // この距離以下になったら停止（X軸）
    [SerializeField] private float m_catchup_distance = 50.0f;              // この距離以上離れると追いつき速度ブースト発動
    [SerializeField] private float m_catchup_multiplier = 1.75f;            // 追いつき時の速度倍率
    [SerializeField] private float m_max_speed = 20.0f;                     // 最大移動速度

    private float m_area_speed_multiplier = 1.0f;                           // エリアによる速度倍率（外部から設定可能）

    [Header("Debug")]
    [SerializeField] private bool m_show_debug_log = false;                 // デバッグログの表示フラグ

    private Rigidbody2D m_rigidbody_2d;                                     // Rigidbody2Dコンポーネント
    private EnemyContext m_context;                                         // 敵の情報をまとめたコンテキスト

    private EnemyState m_state = EnemyState.Chase;                          // 現在の行動状態

    // プロパティ：外部からアクセス可能な読み取り専用情報
    public Transform PlayerTransform => m_player_transform;        // プレイヤーのTransform
    public Rigidbody2D EnemyRigidbody2D => m_rigidbody_2d;         // 敵のRigidbody2D
    public Animator EnemyAnimator => m_animator;                   // 敵のAnimator
    public EnemyState State => m_state;                            // 現在の状態

    /// <summary>
    /// 初期化処理
    /// </summary>
    private void Awake()
    {
        m_rigidbody_2d = GetComponent<Rigidbody2D>();

        // 攻撃コントローラーが設定されていない場合は自動取得
        if (m_attack_controller == null)
        {
            m_attack_controller = GetComponent<EnemyAttackController>();
        }

        m_context = new EnemyContext();
        RefreshContext();
    }

    /// <summary>
    /// メインループ（毎フレーム呼ばれる）
    /// 攻撃の実行と開始判定を行う
    /// </summary>
    private void Update()
    {
        if (m_player_transform == null)
        {
            return;
        }

        RefreshContext();

        // 攻撃実行中の場合は攻撃を更新
        if (m_attack_controller != null && m_attack_controller.IsAttacking)
        {
            m_state = EnemyState.Attack;
            m_attack_controller.TickCurrentAttack(m_context);
            return;
        }

        // 攻撃可能な場合は攻撃を開始
        if (m_attack_controller != null)
        {
            bool started_attack = m_attack_controller.TryStartAttack(m_context);
            if (started_attack)
            {
                m_state = EnemyState.Attack;
                LogDebug("Start Attack");
                return;
            }
        }

        // 攻撃していない場合は追跡状態
        m_state = EnemyState.Chase;
    }

    /// <summary>
    /// 物理演算用の更新処理（固定フレームレートで呼ばれる）
    /// プレイヤーの追跡処理を実行
    /// </summary>
    private void FixedUpdate()
    {
        if (m_player_transform == null)
        {
            return;
        }

        ChasePlayer();
    }

    /// <summary>
    /// コンテキストを最新の状態に更新
    /// 攻撃処理で使用する敵の情報を集約
    /// </summary>
    private void RefreshContext()
    {
        m_context.enemy_transform = transform;
        m_context.player_transform = m_player_transform;
        m_context.enemy_rigidbody_2d = m_rigidbody_2d;
        m_context.enemy_animator = m_animator;
        m_context.enemy_controller = this;
    }

    /// <summary>
    /// プレイヤーを追跡する処理
    /// </summary>
    private void ChasePlayer()
    {
        float distance_x = GetPlayerDistanceX();

        // 停止距離以下の場合は移動を停止
        if (distance_x <= m_stop_distance_x)
        {
            StopMove();
            return;
        }

        // 移動速度を計算して適用
        float move_speed = CalculateMoveSpeed(distance_x);
        m_rigidbody_2d.linearVelocity = new Vector2(move_speed, m_rigidbody_2d.linearVelocity.y);
    }

    /// <summary>
    /// 移動速度を計算する
    /// 基本速度に各種倍率を適用し、最大速度でクランプする
    /// </summary>
    private float CalculateMoveSpeed(float distance_x)
    {
        float speed = m_base_speed;

        speed *= CalculateCatchupMultiplier(distance_x);  // 追いつき倍率を適用
        speed *= m_area_speed_multiplier;                 // エリア速度倍率を適用

        speed = Mathf.Min(speed, m_max_speed);            // 最大速度でクランプ
        return speed;
    }

    /// <summary>
    /// 追いつき倍率を計算する
    /// プレイヤーが遠くに離れすぎた場合に速度ブーストを適用
    /// </summary>
    private float CalculateCatchupMultiplier(float distance_x)
    {
        // 追いつき距離以上離れている場合は倍率を適用
        if (distance_x >= m_catchup_distance)
        {
            return m_catchup_multiplier;
        }

        return 1.0f;
    }

    /// <summary>
    /// 移動を停止する（Y軸の速度は維持）
    /// </summary>
    public void StopMove()
    {
        m_rigidbody_2d.linearVelocity = new Vector2(0.0f, m_rigidbody_2d.linearVelocity.y);
    }

    /// <summary>
    /// プレイヤーとのX軸方向の距離を取得
    /// 正の値 = プレイヤーが右側、負の値 = プレイヤーが左側
    /// </summary>
    public float GetPlayerDistanceX()
    {
        if (m_player_transform == null)
        {
            return float.MaxValue;
        }

        return m_player_transform.position.x - transform.position.x;
    }

    /// <summary>
    /// プレイヤーとのY軸方向の距離を取得（絶対値）
    /// </summary>
    public float GetPlayerDistanceY()
    {
        if (m_player_transform == null)
        {
            return float.MaxValue;
        }

        return Mathf.Abs(m_player_transform.position.y - transform.position.y);
    }

    /// <summary>
    /// プレイヤーのTransformを設定する（外部から呼び出し可能）
    /// </summary>
    public void SetPlayerTransform(Transform player_transform)
    {
        m_player_transform = player_transform;
    }

    /// <summary>
    /// エリアによる速度倍率を設定する
    /// 特定のエリアで敵の移動速度を変更する際に使用
    /// </summary>
    public void SetAreaSpeedMultiplier(float multiplier)
    {
        m_area_speed_multiplier = multiplier;
    }

    /// <summary>
    /// エリア速度倍率をリセット（通常速度に戻す）
    /// </summary>
    public void ResetAreaSpeedMultiplier()
    {
        m_area_speed_multiplier = 1.0f;
    }

    /// <summary>
    /// デバッグログを出力（m_show_debug_logがtrueの場合のみ）
    /// </summary>
    private void LogDebug(string message)
    {
        if (!m_show_debug_log)
        {
            return;
        }

        Debug.Log($"[PursuitEnemyController] {name} : {message}");
    }

    /// <summary>
    /// Unityエディタでオブジェクト選択時にギズモを描画（デバッグ用）
    /// 黄色 = 停止距離、水色 = 追いつき距離
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        // 停止距離を黄色で表示
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(
            transform.position,
            transform.position + Vector3.right * m_stop_distance_x
        );

        // 追いつき距離を水色で表示
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(
            transform.position,
            transform.position + Vector3.right * m_catchup_distance
        );
    }
}

/// <summary>
/// 敵の情報をまとめたコンテキストクラス
/// 攻撃処理で必要な敵とプレイヤーの情報を一箇所にまとめて渡すために使用
/// </summary>
public sealed class EnemyContext
{
    public Transform enemy_transform;                   // 敵のTransform
    public Transform player_transform;                  // プレイヤーのTransform
    public Rigidbody2D enemy_rigidbody_2d;              // 敵のRigidbody2D
    public Animator enemy_animator;                     // 敵のAnimator
    public PursuitEnemyController enemy_controller;     // 敵のコントローラー
}