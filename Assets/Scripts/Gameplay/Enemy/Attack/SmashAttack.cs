using UnityEngine;

public sealed class SmashAttack : EnemyAttackBase
{
    [Header("Range")]
    [SerializeField] private float m_range_x = 2.0f;
    [SerializeField] private float m_range_y = 2.0f;

    [Header("Timing")]
    [SerializeField] private float m_windup_time = 0.35f;
    [SerializeField] private float m_active_time = 0.15f;
    [SerializeField] private float m_recover_time = 0.4f;

    [Header("HitBox")]
    [SerializeField] private EnemyHitBox m_hit_box;

    [Header("Animation")]
    [SerializeField] private string m_trigger_name = "smash";

    [Header("Debug")]
    [SerializeField] private bool m_draw_range_gizmo = true;

    private float m_timer = 0.0f;

    protected override bool CheckCanStart(EnemyContext context)
    {
        if (context == null || context.player_transform == null || context.enemy_controller == null)
        {
            return false;
        }

        float distance_x = context.enemy_controller.GetPlayerDistanceX();
        float distance_y = context.enemy_controller.GetPlayerDistanceY();

        if (distance_x < 0.0f)
        {
            return false;
        }

        return distance_x <= m_range_x && distance_y <= m_range_y;
    }

    protected override void OnStartAttack(EnemyContext context)
    {
        m_timer = 0.0f;
        SetAttackState(AttackState.WindUp);

        if (m_hit_box != null)
        {
            m_hit_box.DeactivateHitBox();
        }

        if (context.enemy_animator != null && !string.IsNullOrEmpty(m_trigger_name))
        {
            context.enemy_animator.SetTrigger(m_trigger_name);
        }
    }

    protected override void OnTickAttack(EnemyContext context)
    {
        m_timer += Time.deltaTime;

        switch (State)
        {
            case AttackState.WindUp:
                {
                    if (m_timer >= m_windup_time)
                    {
                        m_timer = 0.0f;
                        SetAttackState(AttackState.Active);

                        if (m_hit_box != null)
                        {
                            m_hit_box.ActivateHitBox();
                        }
                    }
                    break;
                }

            case AttackState.Active:
                {
                    if (m_timer >= m_active_time)
                    {
                        m_timer = 0.0f;
                        SetAttackState(AttackState.Recover);

                        if (m_hit_box != null)
                        {
                            m_hit_box.DeactivateHitBox();
                        }
                    }
                    break;
                }

            case AttackState.Recover:
                {
                    break;
                }
        }
    }

    protected override bool CheckIsFinished()
    {
        if (State != AttackState.Recover)
        {
            return false;
        }

        return m_timer >= m_recover_time;
    }

    protected override void OnFinishAttack(EnemyContext context)
    {
        if (m_hit_box != null)
        {
            m_hit_box.DeactivateHitBox();
        }
    }

    protected override void OnCancelAttack(EnemyContext context)
    {
        if (m_hit_box != null)
        {
            m_hit_box.DeactivateHitBox();
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!m_draw_range_gizmo)
        {
            return;
        }

        Gizmos.color = Color.red;
        Vector3 center = transform.position + Vector3.right * (m_range_x * 0.5f);
        Vector3 size = new Vector3(m_range_x, m_range_y * 2.0f, 0.0f);
        Gizmos.DrawWireCube(center, size);
    }
}