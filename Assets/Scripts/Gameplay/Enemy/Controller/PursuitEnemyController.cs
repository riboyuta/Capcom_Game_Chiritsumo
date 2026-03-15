using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public sealed class PursuitEnemyController : MonoBehaviour
{
    public enum EnemyState
    {
        Chase,
        Attack
    }

    [Header("References")]
    [SerializeField] private Transform m_player_transform;
    [SerializeField] private EnemyAttackController m_attack_controller;
    [SerializeField] private Animator m_animator;

    [Header("Move")]
    [SerializeField] private float m_base_speed = 10.0f;
    [SerializeField] private float m_stop_distance_x = 0.0f;
    [SerializeField] private float m_catchup_distance = 50.0f;
    [SerializeField] private float m_catchup_multiplier = 1.75f;
    [SerializeField] private float m_max_speed = 20.0f;

    private float m_area_speed_multiplier = 1.0f;

    [Header("Debug")]
    [SerializeField] private bool m_show_debug_log = false;

    private Rigidbody2D m_rigidbody_2d;
    private EnemyContext m_context;

    private EnemyState m_state = EnemyState.Chase;

    public Transform PlayerTransform => m_player_transform;
    public Rigidbody2D EnemyRigidbody2D => m_rigidbody_2d;
    public Animator EnemyAnimator => m_animator;
    public EnemyState State => m_state;

    private void Awake()
    {
        m_rigidbody_2d = GetComponent<Rigidbody2D>();

        if (m_attack_controller == null)
        {
            m_attack_controller = GetComponent<EnemyAttackController>();
        }

        m_context = new EnemyContext();
        RefreshContext();
    }

    private void Update()
    {
        if (m_player_transform == null)
        {
            return;
        }

        RefreshContext();

        if (m_attack_controller != null && m_attack_controller.IsAttacking)
        {
            m_state = EnemyState.Attack;
            m_attack_controller.TickCurrentAttack(m_context);
            return;
        }

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

        m_state = EnemyState.Chase;
    }

    private void FixedUpdate()
    {
        if (m_player_transform == null)
        {
            return;
        }

        ChasePlayer();
    }

    private void RefreshContext()
    {
        m_context.enemy_transform = transform;
        m_context.player_transform = m_player_transform;
        m_context.enemy_rigidbody_2d = m_rigidbody_2d;
        m_context.enemy_animator = m_animator;
        m_context.enemy_controller = this;
    }

    private void ChasePlayer()
    {
        float distance_x = GetPlayerDistanceX();

        if (distance_x <= m_stop_distance_x)
        {
            StopMove();
            return;
        }

        float move_speed = CalculateMoveSpeed(distance_x);
        m_rigidbody_2d.linearVelocity = new Vector2(move_speed, m_rigidbody_2d.linearVelocity.y);
    }

    private float CalculateMoveSpeed(float distance_x)
    {
        float speed = m_base_speed;

        speed *= CalculateCatchupMultiplier(distance_x);
        speed *= m_area_speed_multiplier;

        speed = Mathf.Min(speed, m_max_speed);
        return speed;
    }

    private float CalculateCatchupMultiplier(float distance_x)
    {
        if (distance_x >= m_catchup_distance)
        {
            return m_catchup_multiplier;
        }

        return 1.0f;
    }

    public void StopMove()
    {
        m_rigidbody_2d.linearVelocity = new Vector2(0.0f, m_rigidbody_2d.linearVelocity.y);
    }

    public float GetPlayerDistanceX()
    {
        if (m_player_transform == null)
        {
            return float.MaxValue;
        }

        return m_player_transform.position.x - transform.position.x;
    }

    public float GetPlayerDistanceY()
    {
        if (m_player_transform == null)
        {
            return float.MaxValue;
        }

        return Mathf.Abs(m_player_transform.position.y - transform.position.y);
    }

    public void SetPlayerTransform(Transform player_transform)
    {
        m_player_transform = player_transform;
    }

    public void SetAreaSpeedMultiplier(float multiplier)
    {
        m_area_speed_multiplier = multiplier;
    }

    public void ResetAreaSpeedMultiplier()
    {
        m_area_speed_multiplier = 1.0f;
    }

    private void LogDebug(string message)
    {
        if (!m_show_debug_log)
        {
            return;
        }

        Debug.Log($"[PursuitEnemyController] {name} : {message}");
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(
            transform.position,
            transform.position + Vector3.right * m_stop_distance_x
        );

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(
            transform.position,
            transform.position + Vector3.right * m_catchup_distance
        );
    }
}

public sealed class EnemyContext
{
    public Transform enemy_transform;
    public Transform player_transform;
    public Rigidbody2D enemy_rigidbody_2d;
    public Animator enemy_animator;
    public PursuitEnemyController enemy_controller;
}