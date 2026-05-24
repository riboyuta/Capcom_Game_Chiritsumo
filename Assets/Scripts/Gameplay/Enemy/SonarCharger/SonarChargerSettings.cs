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
    [Header("起動")]
    [Tooltip("シーン開始時に自動で起動するかです。")]
    public bool startActive = false;

    [Tooltip("起動前は見た目を非表示にするかです。")]
    public bool hideUntilActivated = true;

    [Tooltip("プレイヤー判定に使うタグ名です。")]
    public string playerTag = "Player";

    [Header("接触死")]
    [Tooltip("敵本体に触れたプレイヤーを即死させるかです。")]
    public bool killPlayerOnContact = true;

    [Tooltip("プレイヤーを倒した後に敵を無効化するかです。")]
    public bool disableAfterKill = false;

    [Header("通常追跡")]
    [Tooltip("通常時にプレイヤーへ向かって移動する速度です。")]
    [Min(0.0f)]
    public float followSpeed = 1.2f;

    [Header("ソナー")]
    [Tooltip("起動してから最初のソナーを出すまでの時間です。")]
    [Min(0.0f)]
    public float firstSonarDelay = 0.7f;

    [Tooltip("ソナーを出す間隔です。")]
    [Min(0.01f)]
    public float sonarInterval = 2.5f;

    [Tooltip("ソナーリングが広がる速度です。")]
    [Min(0.01f)]
    public float sonarExpandSpeed = 6.0f;

    [Tooltip("ソナーリングの最大半径です。")]
    [Min(0.01f)]
    public float sonarMaxRadius = 8.0f;

    [Tooltip("リング判定の太さです。")]
    [Min(0.01f)]
    public float sonarRingThickness = 0.35f;

    [Header("移動検知")]
    [Tooltip("ソナー接触時に、どの条件でプレイヤーが動いた扱いにするかです。")]
    public SonarChargerMoveDetectMode moveDetectMode = SonarChargerMoveDetectMode.Either;

    [Tooltip("入力判定で、これ以上の移動入力があれば動いた扱いにします。")]
    [Min(0.0f)]
    public float inputMoveThreshold = 0.1f;

    [Tooltip("位置差分判定で、前フレームからこれ以上動いていれば動いた扱いにします。")]
    [Min(0.0f)]
    public float positionMoveThreshold = 0.02f;

    [Header("ダッシュ感知")]
    [Tooltip("有効にすると、Follow状態中にプレイヤーがダッシュ入力した瞬間、ソナーのクールタイムを無視してAlertへ移行します。")]
    public bool enableDashInputAlertTrigger = true;

    [Header("溜め")]
    [Tooltip("感知後、突進方向を確定するまでの溜め時間です。この間はプレイヤー位置を追い続けます。")]
    [Min(0.0f)]
    public float alertTime = 0.4f;

    [Tooltip("突進方向が短すぎる時の最低距離です。")]
    [Min(0.001f)]
    public float minChargeTargetDistance = 0.05f;

    [Header("突進")]
    [Tooltip("直線突進の速度です。")]
    [Min(0.0f)]
    public float chargeSpeed = 12.0f;

    [Tooltip("カメラ表示範囲の端から内側にどれだけ余白を取って停止するかです。")]
    [Min(0.0f)]
    public float cameraBoundaryPadding = 0.1f;

    [Tooltip("突進がこの距離を超えたら、カメラ境界に入っていなくても強制的に停止します。")]
    [Min(0.0f)]
    public float maxChargeDistance = 18.0f;

    [Tooltip("突進がこの時間を超えたら、カメラ境界に入っていなくても強制的に停止します。")]
    [Min(0.0f)]
    public float maxChargeTime = 2.0f;

    [Header("跳ね返り")]
    [Tooltip("カメラ境界に当たった後、突進方向の逆へ戻る距離です。")]
    [Min(0.0f)]
    public float reboundDistance = 0.4f;

    [Tooltip("跳ね返りにかける時間です。")]
    [Min(0.001f)]
    public float reboundDuration = 0.15f;

    [Header("硬直")]
    [Tooltip("跳ね返った後の硬直時間です。")]
    [Min(0.0f)]
    public float stunTime = 0.5f;

    [Header("Alert見た目")]
    [Tooltip("溜め中に Visual を小刻みに揺らす幅です。")]
    [Min(0.0f)]
    public float alertShakeAmplitude = 0.08f;

    [Tooltip("溜め中に Visual を揺らす速さです。")]
    [Min(0.0f)]
    public float alertShakeFrequency = 40.0f;

    [Tooltip("溜め時間が進むほど揺れを強めるかです。")]
    public bool growShakeTowardCharge = true;

    [Header("デバッグ")]
    [Tooltip("デバッグログを出すかです。")]
    public bool enableDebugLog = false;
}