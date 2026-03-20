using UnityEngine;

// EnemyChaseManager の攻撃采配を担当する partial。
// 左手は Grab 固定、右手は Smash 固定で攻撃させる。
public sealed partial class EnemyChaseManager
{
    // 攻撃タイマーを進め、一定間隔ごとに左右の手へ攻撃命令を出す。
    private void TickAttackCoordinator(float deltaTime)
    {
        m_attackTimer += deltaTime;

        if (m_attackTimer < m_config.AttackInterval)
        {
            return;
        }

        m_attackTimer -= m_config.AttackInterval;

        Vector3 targetWorldPosition = m_playerTransform != null
            ? m_playerTransform.position
            : Vector3.zero;

        // 左手は Grab 固定
        if (m_leftHand != null && !m_leftHand.IsBusy)
        {
            m_leftHand.TryStartGrabAttack(targetWorldPosition);
        }

        // 右手は Smash 固定
        if (m_rightHand != null && !m_rightHand.IsBusy)
        {
            m_rightHand.TryStartSmashAttack(targetWorldPosition);
        }
    }
}