using UnityEngine;

// EnemyUnitController の状態遷移と更新ループを担当する partial。
// Idle → Windup → Attack → Recovery → Idle の循環で運用する。
public sealed partial class EnemyUnitController
{
    // 状態遷移と攻撃進行を更新し、最後に見た目同期を行う。
    private void Update()
    {
        TickState(Time.deltaTime);
        TickVisual(Time.deltaTime);
    }

    private void TickState(float deltaTime)
    {
        if (m_config == null)
        {
            return;
        }

        m_stateTimer += deltaTime;

        switch (m_state)
        {
            case EnemyUnitState.Idle:
                break;

            case EnemyUnitState.Windup:
                if (m_stateTimer >= m_config.WindupDuration)
                {
                    StartReservedAttack();
                }
                break;

            case EnemyUnitState.Attack:
                if (m_attackController == null || !m_attackController.IsRunning)
                {
                    ChangeState(EnemyUnitState.Recovery);
                }
                break;

            case EnemyUnitState.Recovery:
                if (m_stateTimer >= m_config.RecoveryDuration)
                {
                    ChangeState(EnemyUnitState.Idle);
                }
                break;
        }
    }

    // Windup 終了後に予約されていた攻撃を開始する。
    private void StartReservedAttack()
    {
        if (m_attackController == null)
        {
            ChangeState(EnemyUnitState.Recovery);
            return;
        }

        switch (m_reservedAttackType)
        {
            case EnemyAttackController.EnemyAttackType.Grab:
                m_attackController.BeginGrabAttack(m_reservedTargetWorld);
                break;

            case EnemyAttackController.EnemyAttackType.Smash:
                m_attackController.BeginSmashAttack(m_reservedTargetWorld);
                break;
        }

        ChangeState(EnemyUnitState.Attack);
    }

    private void ChangeState(EnemyUnitState next)
    {
        m_state = next;
        m_stateTimer = 0.0f;
    }
}