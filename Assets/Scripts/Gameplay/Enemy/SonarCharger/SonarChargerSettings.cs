using System;
using UnityEngine;

// プレイヤーの移動検知モード
// ソナー検知時にどの方法でプレイヤーの「動き」を判定するかを指定
public enum SonarChargerMoveDetectMode
{
    Input,          // 入力だけで判定（MoveInputDirectionの大きさ）
    PositionDelta,  // 位置差分だけで判定（前フレームからの移動量）
    Either          // 入力または位置差分のいずれか（どちらかで検知）
}

// SonarChargerEnemy のすべての調整パラメータ
// 起動、接触死、速度、ソナー、Alert、突進、跳ね返り、スタンなどの全挙動を調整する集中管理クラス
[Serializable]
public sealed class SonarChargerSettings
{
    [Header("シーン開始時に自動起動")]
    [Tooltip("シーン開始時に自動で起動するかです。")]
    public bool startActive = false;

    [Header("起動前は見た目非表示")]
    [Tooltip("起動前は見た目を非表示にするかです。")]
    public bool hideUntilActivated = true;

    [Header("プレイヤー判定タグ")]
    [Tooltip("プレイヤー判定に使うタグ名です。")]
    public string playerTag = "Player";

    [Header("接触時に即死させる")]
    [Tooltip("敵本体に触れたプレイヤーを即死させるかです。")]
    public bool killPlayerOnContact = true;

    [Header("倒した後に無効化")]
    [Tooltip("プレイヤーを倒した後に敵を無効化するかです。")]
    public bool disableAfterKill = false;

    [Header("通常追跡速度")]
    [Tooltip("通常時にプレイヤーへ向かって移動する速度です。")]
    [Min(0.0f)]
    public float followSpeed = 1.2f;

    [Header("最初のソナー発信遅延")]
    [Tooltip("起動してから最初のソナーを出すまでの時間です。")]
    [Min(0.0f)]
    public float firstSonarDelay = 0.7f;

    [Header("ソナー発信間隔")]
    [Tooltip("ソナーを出す間隔です。")]
    [Min(0.01f)]
    public float sonarInterval = 2.5f;

    [Header("ソナーリング拡大速度")]
    [Tooltip("ソナーリングが広がる速度です。")]
    [Min(0.01f)]
    public float sonarExpandSpeed = 6.0f;

    [Header("ソナーリング最大半径")]
    [Tooltip("ソナーリングの最大半径です。")]
    [Min(0.01f)]
    public float sonarMaxRadius = 8.0f;

    [Header("ソナーリング判定の太さ")]
    [Tooltip("リング判定の太さです。")]
    [Min(0.01f)]
    public float sonarRingThickness = 0.35f;

    [Header("移動検知モード")]
    [Tooltip("ソナー接触時に、どの条件でプレイヤーが動いた扱いにするかです。")]
    public SonarChargerMoveDetectMode moveDetectMode = SonarChargerMoveDetectMode.Either;

    [Header("入力移動判定しきい値")]
    [Tooltip("入力判定で、これ以上の移動入力があれば動いた扱いにします。")]
    [Min(0.0f)]
    public float inputMoveThreshold = 0.1f;

    [Header("位置差分移動判定しきい値")]
    [Tooltip("位置差分判定で、前フレームからこれ以上動いていれば動いた扱いにします。")]
    [Min(0.0f)]
    public float positionMoveThreshold = 0.02f;

    [Header("ダッシュ入力の感知")]
    [Tooltip("有効にすると、Follow状態中にプレイヤーがダッシュ入力した瞬間、ソナーのクールタイムを無視してAlertへ移行します。")]
    public bool enableDashInputAlertTrigger = true;

    [Header("突進前の溜め時間")]
    [Tooltip("感知後、突進方向を確定するまでの溜め時間です。この間はプレイヤー位置を追い続けます。")]
    [Min(0.0f)]
    public float alertTime = 0.4f;

    [Header("突進方向の最低距離")]
    [Tooltip("突進方向が短すぎる時の最低距離です。")]
    [Min(0.001f)]
    public float minChargeTargetDistance = 0.05f;

    [Header("突進速度")]
    [Tooltip("直線突進の速度です。")]
    [Min(0.0f)]
    public float chargeSpeed = 12.0f;

    [Header("カメラ境界からの余白")]
    [Tooltip("カメラ表示範囲の端から内側にどれだけ余白を取って停止するかです。")]
    [Min(0.0f)]
    public float cameraBoundaryPadding = 0.1f;

    [Header("突進最大距離")]
    [Tooltip("突進がこの距離を超えたら、カメラ境界に入っていなくても強制的に停止します。")]
    [Min(0.0f)]
    public float maxChargeDistance = 18.0f;

    [Header("突進最大時間")]
    [Tooltip("突進がこの時間を超えたら、カメラ境界に入っていなくても強制的に停止します。")]
    [Min(0.0f)]
    public float maxChargeTime = 2.0f;

    [Header("跳ね返り距離")]
    [Tooltip("カメラ境界に当たった後、突進方向の逆へ戻る距離です。")]
    [Min(0.0f)]
    public float reboundDistance = 0.4f;

    [Header("跳ね返り時間")]
    [Tooltip("跳ね返りにかける時間です。")]
    [Min(0.001f)]
    public float reboundDuration = 0.15f;

    [Header("跳ね返り後の硬直時間")]
    [Tooltip("跳ね返った後の硬直時間です。")]
    [Min(0.0f)]
    public float stunTime = 0.5f;

    [Header("溜め中の揺れ幅")]
    [Tooltip("溜め中に Visual を小刻みに揺らす幅です。")]
    [Min(0.0f)]
    public float alertShakeAmplitude = 0.08f;

    [Header("溜め中の揺れ速さ")]
    [Tooltip("溜め中に Visual を揺らす速さです。")]
    [Min(0.0f)]
    public float alertShakeFrequency = 40.0f;

    [Header("溜め進行で揺れ強化")]
    [Tooltip("溜め時間が進むほど揺れを強めるかです。")]
    public bool growShakeTowardCharge = true;

    [Header("突進予測線を表示する")]
    [Tooltip("Alert中に突進方向の予測線を表示するかです。")]
    public bool showAlertPredictionLine = true;

    [Header("突進予測線の長さ")]
    [Tooltip("予測線の最大長さです。0以下なら突進最大距離を使います。")]
    [Min(0.0f)]
    public float alertPredictionLineLength = 0.0f;

    [Header("突進予測線の芯の太さ")]
    [Tooltip("予測線の中心線の太さです。")]
    [Min(0.001f)]
    public float alertPredictionCoreWidth = 0.04f;

    [Header("突進予測線の発光太さ")]
    [Tooltip("予測線の外側発光部分の太さです。")]
    [Min(0.001f)]
    public float alertPredictionGlowWidth = 0.14f;

    [Header("突進予測線の点滅速度")]
    [Tooltip("予測線の点滅速度です。")]
    [Min(0.0f)]
    public float alertPredictionPulseSpeed = 12.0f;

    [Header("突進予測線の最小透明度")]
    [Tooltip("予測線の点滅時の最小透明度です。")]
    [Range(0.0f, 1.0f)]
    public float alertPredictionMinAlpha = 0.35f;

    [Header("突進予測線の最大透明度")]
    [Tooltip("予測線の点滅時の最大透明度です。")]
    [Range(0.0f, 1.0f)]
    public float alertPredictionMaxAlpha = 1.0f;

    [Header("突進予測線先端マーカーの基準スケール")]
    [Tooltip("予測線の先端マーカーの基準スケールです。")]
    [Min(0.0f)]
    public float alertPredictionTargetMarkerScale = 0.35f;

    [Header("突進予測線先端マーカーの脈動量")]
    [Tooltip("予測線の先端マーカーの脈動量です。")]
    [Min(0.0f)]
    public float alertPredictionTargetMarkerPulseScale = 0.08f;

    [Header("デバッグログ出力")]
    [Tooltip("デバッグログを出すかです。")]
    public bool enableDebugLog = false;

    public void CopyFrom(SonarChargerSettings source)
    {
        if (source == null)
        {
            return;
        }

        startActive = source.startActive;
        hideUntilActivated = source.hideUntilActivated;

        playerTag = string.IsNullOrWhiteSpace(source.playerTag)
            ? "Player"
            : source.playerTag;

        killPlayerOnContact = source.killPlayerOnContact;
        disableAfterKill = source.disableAfterKill;

        followSpeed = Mathf.Max(0.0f, source.followSpeed);

        firstSonarDelay = Mathf.Max(0.0f, source.firstSonarDelay);
        sonarInterval = Mathf.Max(0.01f, source.sonarInterval);
        sonarExpandSpeed = Mathf.Max(0.01f, source.sonarExpandSpeed);
        sonarMaxRadius = Mathf.Max(0.01f, source.sonarMaxRadius);
        sonarRingThickness = Mathf.Max(0.01f, source.sonarRingThickness);

        moveDetectMode = source.moveDetectMode;
        inputMoveThreshold = Mathf.Max(0.0f, source.inputMoveThreshold);
        positionMoveThreshold = Mathf.Max(0.0f, source.positionMoveThreshold);

        enableDashInputAlertTrigger = source.enableDashInputAlertTrigger;

        alertTime = Mathf.Max(0.0f, source.alertTime);
        minChargeTargetDistance = Mathf.Max(0.001f, source.minChargeTargetDistance);
        chargeSpeed = Mathf.Max(0.0f, source.chargeSpeed);
        cameraBoundaryPadding = Mathf.Max(0.0f, source.cameraBoundaryPadding);

        maxChargeDistance = Mathf.Max(0.0f, source.maxChargeDistance);
        maxChargeTime = Mathf.Max(0.0f, source.maxChargeTime);

        reboundDistance = Mathf.Max(0.0f, source.reboundDistance);
        reboundDuration = Mathf.Max(0.001f, source.reboundDuration);
        stunTime = Mathf.Max(0.0f, source.stunTime);

        alertShakeAmplitude = Mathf.Max(0.0f, source.alertShakeAmplitude);
        alertShakeFrequency = Mathf.Max(0.0f, source.alertShakeFrequency);
        growShakeTowardCharge = source.growShakeTowardCharge;

        showAlertPredictionLine = source.showAlertPredictionLine;
        alertPredictionLineLength = Mathf.Max(0.0f, source.alertPredictionLineLength);
        alertPredictionCoreWidth = Mathf.Max(0.001f, source.alertPredictionCoreWidth);
        alertPredictionGlowWidth = Mathf.Max(0.001f, source.alertPredictionGlowWidth);
        alertPredictionPulseSpeed = Mathf.Max(0.0f, source.alertPredictionPulseSpeed);
        alertPredictionMinAlpha = Mathf.Clamp01(source.alertPredictionMinAlpha);
        alertPredictionMaxAlpha = Mathf.Clamp01(source.alertPredictionMaxAlpha);
        alertPredictionTargetMarkerScale = Mathf.Max(0.0f, source.alertPredictionTargetMarkerScale);
        alertPredictionTargetMarkerPulseScale = Mathf.Max(0.0f, source.alertPredictionTargetMarkerPulseScale);

        enableDebugLog = source.enableDebugLog;
    }
}