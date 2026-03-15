using UnityEngine;

public sealed class EnemyAttackController : MonoBehaviour
{
    [SerializeField] private EnemyAttackBase[] m_attacks;
    [SerializeField] private bool m_show_debug_log = false;

    private EnemyAttackBase m_current_attack;

    public bool IsAttacking => m_current_attack != null;
    public string CurrentAttackName => m_current_attack != null ? m_current_attack.AttackName : "None";

    private void Reset()
    {
        m_attacks = GetComponents<EnemyAttackBase>();
    }

    public bool TryStartAttack(EnemyContext context)
    {
        if (m_current_attack != null)
        {
            return false;
        }

        EnemyAttackBase next_attack = FindBestAttack(context);
        if (next_attack == null)
        {
            return false;
        }

        m_current_attack = next_attack;
        m_current_attack.StartAttack(context);

        LogDebug($"Start {m_current_attack.AttackName}");
        return true;
    }

    public void TickCurrentAttack(EnemyContext context)
    {
        if (m_current_attack == null)
        {
            return;
        }

        m_current_attack.TickAttack(context);

        if (m_current_attack.IsFinished())
        {
            LogDebug($"Finish {m_current_attack.AttackName}");
            m_current_attack.FinishAttack(context);
            m_current_attack = null;
        }
    }

    public void CancelCurrentAttack(EnemyContext context)
    {
        if (m_current_attack == null)
        {
            return;
        }

        LogDebug($"Cancel {m_current_attack.AttackName}");
        m_current_attack.CancelAttack(context);
        m_current_attack = null;
    }

    private EnemyAttackBase FindBestAttack(EnemyContext context)
    {
        if (m_attacks == null || m_attacks.Length == 0)
        {
            return null;
        }

        EnemyAttackBase best_attack = null;
        int best_priority = int.MinValue;

        foreach (EnemyAttackBase attack in m_attacks)
        {
            if (attack == null)
            {
                continue;
            }

            if (!attack.CanStart(context))
            {
                continue;
            }

            if (attack.Priority > best_priority)
            {
                best_priority = attack.Priority;
                best_attack = attack;
            }
        }

        return best_attack;
    }

    private void LogDebug(string message)
    {
        if (!m_show_debug_log)
        {
            return;
        }

        Debug.Log($"[EnemyAttackController] {name} : {message}");
    }
}