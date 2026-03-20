using UnityEngine;

// EnemyChaseManager の前進圧処理を担当する partial。
// 全体圧を前進させ、左右の手へ同じ基準 X を配布する。
public sealed partial class EnemyChaseManager
{
    // 全体圧を前進させ、左右の手へ同じ基準 X を配布する。
    private void TickAdvance(float deltaTime)
    {
        m_pressureX += m_config.PressureSpeed * deltaTime;
        BroadcastPressure();
    }

    // 圧位置を左右の手に通知する。
    // それぞれの手はこの値をもとに見た目位置を更新する。
    private void BroadcastPressure()
    {
        if (m_leftHand != null)
        {
            m_leftHand.SetPressureX(m_pressureX);
        }

        if (m_rightHand != null)
        {
            m_rightHand.SetPressureX(m_pressureX);
        }
    }

    // DeathZone の同期に使う WorldX を算出する。
    private float CalculateDeathZoneX()
    {
        return m_pressureX + m_config.DeathZoneOffset;
    }
}
