using UnityEngine;

public abstract class EnemyAttackBase : MonoBehaviour
{
    public enum AttackState
    {
        Idle,
        WindUp,
        Active,
        Recover
    }

    [Header("Base Attack Settings")]
    [SerializeField] protected string m_attack_name = "Attack";
    [SerializeField] protected int m_priority = 0;
    [SerializeField] protected float m_cooldown = 1.0f;

    [Header("Debug")]
    [SerializeField] protected bool m_show_debug_log = false;

    protected bool m_is_running = false;
    protected float m_last_attack_time = -999.0f;
    protected AttackState m_attack_state = AttackState.Idle;

    public string AttackName => m_attack_name;
    public int Priority => m_priority;
    public bool IsRunning => m_is_running;
    public AttackState State => m_attack_state;

    public bool CanStart(EnemyContext context)
    {
        if (m_is_running)
        {
            return false;
        }

        if (Time.time < m_last_attack_time + m_cooldown)
        {
            return false;
        }

        return CheckCanStart(context);
    }

    public void StartAttack(EnemyContext context)
    {
        m_is_running = true;
        m_attack_state = AttackState.WindUp;
        OnStartAttack(context);
        LogDebug("Start");
    }

    public void TickAttack(EnemyContext context)
    {
        if (!m_is_running)
        {
            return;
        }

        OnTickAttack(context);
    }

    public bool IsFinished()
    {
        if (!m_is_running)
        {
            return true;
        }

        return CheckIsFinished();
    }

    public void FinishAttack(EnemyContext context)
    {
        if (!m_is_running)
        {
            return;
        }

        m_is_running = false;
        m_last_attack_time = Time.time;
        m_attack_state = AttackState.Idle;

        OnFinishAttack(context);
        LogDebug("Finish");
    }

    public virtual void CancelAttack(EnemyContext context)
    {
        m_is_running = false;
        m_last_attack_time = Time.time;
        m_attack_state = AttackState.Idle;

        OnCancelAttack(context);
        LogDebug("Cancel");
    }

    protected void SetAttackState(AttackState next_state)
    {
        m_attack_state = next_state;
    }

    protected void LogDebug(string message)
    {
        if (!m_show_debug_log)
        {
            return;
        }

        Debug.Log($"[EnemyAttackBase] {name} ({m_attack_name}) : {message}");
    }

    protected abstract bool CheckCanStart(EnemyContext context);
    protected abstract void OnStartAttack(EnemyContext context);
    protected abstract void OnTickAttack(EnemyContext context);
    protected abstract bool CheckIsFinished();
    protected abstract void OnFinishAttack(EnemyContext context);

    protected virtual void OnCancelAttack(EnemyContext context)
    {
    }
}