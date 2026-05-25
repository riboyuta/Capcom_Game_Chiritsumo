using UnityEngine;

[DisallowMultipleComponent]
public sealed class SonarChargerSonarDetector : MonoBehaviour
{
    [Header("ソナー中心")]
    [Tooltip("ソナーの発生中心です。未設定時はこの GameObject の位置を使います。")]
    [SerializeField] private Transform sonarOrigin;

    [Header("Gizmo")]
    [SerializeField] private bool drawGizmos = true;
    [SerializeField] private Color currentRingColor = new Color(0.0f, 0.8f, 1.0f, 0.9f);
    [SerializeField] private Color maxRadiusColor = new Color(0.0f, 0.8f, 1.0f, 0.25f);

    // パルス状態
    private bool isPulseActive;
    private bool hasCompletedFirstPulse;
    private float intervalTimer;
    private float currentRadius;
    private float lastMaxRadius;

    public bool IsPulseActive => isPulseActive;
    public float CurrentRadius => currentRadius;

    public void ResetDetector(SonarChargerSettings settings)
    {
        ResetPulseState(false, false);
        lastMaxRadius = settings?.sonarMaxRadius ?? 0.0f;
    }

    public void CancelPulse()
    {
        ResetPulseState(false, true);
    }

    // ソナー更新: パルスを展開し、プレイヤーがリング上で動いていれば検知
    public bool TickSonar(
        PlayerController targetPlayer,
        SonarChargerPlayerMotionDetector motionDetector,
        SonarChargerSettings settings,
        float deltaTime,
        out Vector3 detectedPosition)
    {
        detectedPosition = Vector3.zero;

        // 必要な参照がない場合は検知失敗
        if (targetPlayer == null || motionDetector == null || settings == null)
        {
            return false;
        }

        // 最大半径を記録（Gizmo描画用）
        lastMaxRadius = settings.sonarMaxRadius;

        // パルス状態を更新、アクティブでない場合はfalse
        if (!UpdatePulse(settings, deltaTime))
        {
            return false;
        }

        Vector3 origin = GetOriginPosition();
        Vector3 playerPosition = targetPlayer.transform.position;

        // プレイヤーがリング上にいない、または動いていない場合は検知失敗
        if (!IsPlayerOnRing(origin, playerPosition, settings) || !motionDetector.IsMoving(settings.moveDetectMode))
        {
            return false;
        }

        // 検知成功！プレイヤー位置を返し、パルスをキャンセル
        detectedPosition = playerPosition;
        CancelPulse();
        return true;
    }

    // パルス更新: インターバル待機、パルス開始、半径拡大、終了判定
    private bool UpdatePulse(SonarChargerSettings settings, float deltaTime)
    {
        // パルスが非アクティブな場合、インターバル待機
        if (!isPulseActive)
        {
            intervalTimer += deltaTime;
            // 初回と２回目以降で待機時間が異なる
            float waitTime = hasCompletedFirstPulse ? settings.sonarInterval : settings.firstSonarDelay;

            // 待機時間未達ならfalse
            if (intervalTimer < waitTime)
            {
                return false;
            }

            // 待機時間経過、パルス開始
            ResetPulseState(true, hasCompletedFirstPulse);
        }

        // パルスの半径を展開
        currentRadius += settings.sonarExpandSpeed * deltaTime;

        // 最大半径に達したらパルス終了
        if (currentRadius > settings.sonarMaxRadius)
        {
            ResetPulseState(false, true);
            return false; // パルス終了
        }

        return true; // パルスアクティブ
    }

    // プレイヤーがソナーリング上にいるかを判定
    private bool IsPlayerOnRing(Vector3 origin, Vector3 playerPosition, SonarChargerSettings settings)
    {
        // 原点からプレイヤーまでの距離を計算
        float distance = Vector2.Distance(ToVector2(origin), ToVector2(playerPosition));
        // リングの半分の太さ
        float halfThickness = settings.sonarRingThickness * 0.5f;
        // 現在の半径±半分の太さの範囲内ならリング上
        return distance >= currentRadius - halfThickness && distance <= currentRadius + halfThickness;
    }

    // ソナーの発生中心位置を取得
    private Vector3 GetOriginPosition()
    {
        return sonarOrigin != null ? sonarOrigin.position : transform.position;
    }

    // ヘルパー: パルス状態をリセット
    private void ResetPulseState(bool active, bool completed)
    {
        isPulseActive = active;
        hasCompletedFirstPulse = completed;
        intervalTimer = 0.0f;
        currentRadius = 0.0f;
    }

    // ユーティリティ: Vector3をVector2に変換
    private static Vector2 ToVector2(Vector3 vector)
    {
        return new Vector2(vector.x, vector.y);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos)
        {
            return;
        }

        Vector3 origin = GetOriginPosition();

        if (lastMaxRadius > 0.0f)
        {
            Gizmos.color = maxRadiusColor;
            Gizmos.DrawWireSphere(origin, lastMaxRadius);
        }

        if (isPulseActive && currentRadius > 0.0f)
        {
            Gizmos.color = currentRingColor;
            Gizmos.DrawWireSphere(origin, currentRadius);
        }
    }
#endif
}