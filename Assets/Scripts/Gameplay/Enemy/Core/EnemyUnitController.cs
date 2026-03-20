using System.Collections.Generic;
using UnityEngine;

// 手1本ぶんの主体クラス。
// 攻撃命令の受付、参照の保持、状態更新の入口を担当する。
public sealed partial class EnemyUnitController : MonoBehaviour
{
    public enum EnemyUnitState
    {
        Idle,
        Windup,
        Attack,
        Recovery
    }

    [Header("参照")]
    [Tooltip("通常時の位置同期先となる Transform です。全体圧に合わせて移動します。")]
    [SerializeField] private Transform m_root;

    [Tooltip("攻撃時に先端として動かす Transform です。EnemyAttackController に渡して使用します。")]
    [SerializeField] private Transform m_palm;

    [Tooltip("Grab / Smash 攻撃の実行を担当する EnemyAttackController です。")]
    [SerializeField] private EnemyAttackController m_attackController;

    [Tooltip("将来の本体接触判定に使用する 3D Collider です。現段階では未使用です。")]
    [SerializeField] private Collider m_bodyHitbox;

    [Tooltip("状態に応じた見た目更新に使用する Animator です。不要なら未設定でも動作します。")]
    [SerializeField] private Animator m_animator;

    [Header("設定")]
    [Tooltip("この手ユニットが参照する設定データです。状態時間や攻撃時間の取得に使用します。")]
    [SerializeField] private EnemyConfig m_config;

    [Header("腕セグメント描画")]
    [Tooltip("腕セグメントをぶら下げる親 Transform です。通常は Root の子を設定します。")]
    [SerializeField] private Transform m_armSegmentsRoot;

    [Tooltip("腕1節ぶんの見た目プレハブです。SpriteRenderer を持つオブジェクトを設定します。")]
    [SerializeField] private GameObject m_armSegmentPrefab;

    [Tooltip("腕1節ぶんの基準長さです。Root と Palm の距離から必要本数を計算するために使います。")]
    [SerializeField] private float m_armSegmentLength = 0.5f;

    [Tooltip("生成・表示する腕セグメントの最大数です。極端な距離で増えすぎないように制限します。")]
    [SerializeField] private int m_maxArmSegmentCount = 16;

    [Tooltip("Root 側から何メートル空けてセグメント配置を始めるかを設定します。")]
    [SerializeField] private float m_rootSegmentOffset = 0.0f;

    [Tooltip("Palm 側の手前で何メートル止めるかを設定します。手の見た目との重なり調整に使います。")]
    [SerializeField] private float m_palmSegmentOffset = 0.0f;

    private readonly List<Transform> m_armSegmentInstances = new();

    protected EnemyUnitState m_state = EnemyUnitState.Idle;
    protected float m_stateTimer;
    protected float m_pressureX;
    protected Vector3 m_reservedTargetWorld;
    protected EnemyAttackController.EnemyAttackType m_reservedAttackType;

    public bool IsBusy => m_state != EnemyUnitState.Idle;
    public EnemyUnitState CurrentState => m_state;

    public Transform Root => m_root;
    public Transform Palm => m_palm;
    public EnemyConfig Config => m_config;

    private void Awake()
    {
        if (m_root == null)
        {
            Debug.LogWarning("EnemyUnitController: m_root が未設定です。通常時の位置同期ができません。");
        }

        if (m_palm == null)
        {
            Debug.LogWarning("EnemyUnitController: m_palm が未設定です。攻撃先端を制御できません。");
        }

        if (m_attackController == null)
        {
            Debug.LogWarning("EnemyUnitController: m_attackController が未設定です。攻撃を開始できません。");
        }

        if (m_config == null)
        {
            Debug.LogWarning("EnemyUnitController: m_config が未設定です。状態時間や攻撃時間を取得できません。");
        }

        if (m_armSegmentsRoot == null)
        {
            Debug.LogWarning("EnemyUnitController: m_armSegmentsRoot が未設定です。腕セグメントを描画できません。");
        }

        if (m_armSegmentPrefab == null)
        {
            Debug.LogWarning("EnemyUnitController: m_armSegmentPrefab が未設定です。腕セグメントを生成できません。");
        }

        if (m_attackController != null)
        {
            m_attackController.SetConfig(m_config);
            m_attackController.SetPalm(m_palm);
        }
    }

    // 全体圧から送られてきた X 座標を保持する。
    public void SetPressureX(float pressureX)
    {
        m_pressureX = pressureX;
    }

    // Grab 攻撃を予約する。
    public bool TryStartGrabAttack(Vector3 targetWorldPosition)
    {
        if (m_state != EnemyUnitState.Idle)
        {
            return false;
        }

        m_reservedTargetWorld = targetWorldPosition;
        m_reservedAttackType = EnemyAttackController.EnemyAttackType.Grab;
        ChangeState(EnemyUnitState.Windup);
        return true;
    }

    // Smash 攻撃を予約する。
    public bool TryStartSmashAttack(Vector3 targetWorldPosition)
    {
        if (m_state != EnemyUnitState.Idle)
        {
            return false;
        }

        m_reservedTargetWorld = targetWorldPosition;
        m_reservedAttackType = EnemyAttackController.EnemyAttackType.Smash;
        ChangeState(EnemyUnitState.Windup);
        return true;
    }
}