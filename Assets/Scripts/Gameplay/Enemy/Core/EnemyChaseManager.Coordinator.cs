using UnityEngine;

// EnemyChaseManager の攻撃采配を担当する partial。
// 左手は Grab 固定、右手は Smash 固定で攻撃させる。
public sealed partial class EnemyChaseManager
{
    // 攻撃タイマーを進め、一定間隔ごとに左右の手へ攻撃命令を出す。
    // 左手は Grab 攻撃、右手は Smash 攻撃を担当する。
    // 手が既に攻撃中（IsBusy）なら命令をスキップする。
    private void TickAttackCoordinator(float deltaTime)
    {
        // 攻撃タイマーを進める
        m_attackTimer += deltaTime;

        // まだ攻撃間隔に達していない場合は何もしない
        if (m_attackTimer < m_config.AttackInterval)
        {
            return;
        }

        // 攻撃間隔に達したのでタイマーをリセット（余剰時間は次回に持ち越す）
        m_attackTimer -= m_config.AttackInterval;

        // プレイヤーの現在位置を攻撃ターゲットとして取得
        Vector3 targetWorldPosition = m_playerTransform != null
            ? m_playerTransform.position
            : Vector3.zero;

        // 左手は Grab 攻撃固定（手が忙しくない場合のみ命令を出す）
        if (m_leftHand != null && !m_leftHand.IsBusy)
        {
            m_leftHand.TryStartGrabAttack(targetWorldPosition);
        }

        // 右手は Smash 攻撃固定（手が忙しくない場合のみ命令を出す）
        if (m_rightHand != null && !m_rightHand.IsBusy)
        {
            m_rightHand.TryStartSmashAttack(targetWorldPosition);
        }
    }
}