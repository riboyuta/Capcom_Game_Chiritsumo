using UnityEngine;

// EnemyChaseManager の前進圧処理を担当する partial。
// 全体圧を前進させ、左右の手へ同じ基準 X を配布する。
public sealed partial class EnemyChaseManager
{
    // 全体圧を前進させ、左右の手へ同じ基準 X を配布する。
    // 毎フレーム呼び出され、PressureSpeed に応じて左から右へ進む。
    private void TickAdvance(float deltaTime)
    {
        // 設定された速度で圧位置を右へ進める
        pressureX += config.PressureSpeed * deltaTime;
        // 更新した圧位置を左右の手に通知
        BroadcastPressure();
    }

    // 圧位置を左右の手に通知する。
    // それぞれの手はこの値をもとに見た目位置を更新する。
    // 各手の Root Transform の X 座標をこの値に同期する。
    private void BroadcastPressure()
    {
        // 左手が設定されていれば圧位置を通知
        if (leftHand != null)
        {
            leftHand.SetPressureX(pressureX);
        }

        // 右手が設定されていれば圧位置を通知
        if (rightHand != null)
        {
            rightHand.SetPressureX(pressureX);
        }
    }

    // DeathZone の同期に使う WorldX を算出する。
    // 圧位置にオフセットを加えた値を返す。
    // オフセットは Config で調整可能（正なら右側、負なら左側）。
    private float CalculateDeathZoneX()
    {
        return pressureX + config.DeathZoneOffset;
    }
}
