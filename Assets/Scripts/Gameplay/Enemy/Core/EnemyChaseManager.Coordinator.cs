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
        attackTimer += deltaTime;

        // まだ攻撃間隔に達していない場合は何もしない
        if (attackTimer < config.AttackInterval)
        {
            return;
        }

        // 攻撃間隔に達したのでタイマーをリセット（余剰時間は次回に持ち越す）
        attackTimer -= config.AttackInterval;

        // プレイヤーの現在位置を攻撃ターゲットとして取得
        Vector3 targetWorldPosition = playerTransform != null
            ? playerTransform.position
            : Vector3.zero;

        // 左手は Grab 攻撃固定（手が忙しくない場合のみ命令を出す）
        if (leftHand != null && !leftHand.IsBusy)
        {
            leftHand.TryStartGrabAttack(targetWorldPosition);
        }

        // 右手は Smash 攻撃固定（手が忙しくない場合のみ命令を出す）
        if (rightHand != null && !rightHand.IsBusy)
        {
            rightHand.TryStartSmashAttack(targetWorldPosition);
        }
    }
}