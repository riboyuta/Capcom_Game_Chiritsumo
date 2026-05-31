using System;
using UnityEngine;

// HandChaser の変速設定。
// プレイヤーとの距離に応じて速度を変えるための調整値。
[Serializable]
public struct HandChaserAdaptiveSpeedSettings
{
    [Header("変速機能")]
    [Tooltip("プレイヤーとの距離に応じて速度を変化させるかどうかです。false にすると等速になります。")]
    public bool enableAdaptiveSpeed;

    [Header("各ゾーンの速度")]
    [Tooltip("近すぎる時の速度です。")]
    [Min(0f)] public float nearSpeed;

    [Tooltip("理想距離にいる時の速度です。")]
    [Min(0f)] public float idealSpeed;

    [Tooltip("遠すぎる時の速度です。")]
    [Min(0f)] public float farSpeed;

    [Header("距離ゾーン境界")]
    [Tooltip("この距離以下なら近すぎ扱いです。")]
    [Min(0f)] public float nearThreshold;

    [Tooltip("理想距離の近側境界です。")]
    [Min(0f)] public float idealMinDistance;

    [Tooltip("理想距離の遠側境界です。")]
    [Min(0f)] public float idealMaxDistance;

    [Tooltip("この距離以上なら遠すぎ扱いです。")]
    [Min(0f)] public float farThreshold;

    [Header("速度変化スムーズ")]
    [Tooltip("速度変化を滑らかにする時間です。小さいほど鋭く変化します。")]
    [Min(0.001f)] public float speedSmoothTime;

    public static HandChaserAdaptiveSpeedSettings Default => new HandChaserAdaptiveSpeedSettings
    {
        enableAdaptiveSpeed = true,
        nearSpeed = 7f,
        idealSpeed = 9f,
        farSpeed = 12f,
        nearThreshold = 4f,
        idealMinDistance = 6f,
        idealMaxDistance = 8f,
        farThreshold = 12f,
        speedSmoothTime = 0.15f
    };
}

// HandChaser 全体のデフォルト調整値。
// RoomEnemySystem が保持し、Room ごとの上書きを合成して敵へ適用する。
[Serializable]
public sealed class HandChaserSettings
{
    [Header("開始設定")]
    [Tooltip("シーン開始時に自動で有効化するかです。")]
    public bool startActive = false;

    [Tooltip("有効化されるまで見た目を非表示にするかです。")]
    public bool hideUntilActivated = true;

    [Header("起動時ワープ")]
    [Tooltip("有効化時に指定位置へワープするかどうかです。")]
    public bool useSpawnPositionOnActivate = false;

    [Tooltip("有効化時のワープ位置です。")]
    public Vector3 spawnPositionOnActivate = Vector3.zero;

    [Header("プレイヤー判定")]
    [Tooltip("プレイヤー検索・接触判定に使うタグ名です。")]
    public string playerTag = "Player";

    [Header("接触死")]
    [Tooltip("プレイヤーと接触した時に即死させるかです。")]
    public bool killPlayerOnContact = true;

    [Tooltip("プレイヤーを倒した後、敵を無効化するかです。")]
    public bool disableAfterKill = false;

    [Header("移動設定")]
    [Tooltip("等速移動・移動方向に関する設定です。")]
    public HandChaserMovementSettings movement = HandChaserMovementSettings.Default;

    [Header("変速設定")]
    [Tooltip("プレイヤーとの距離に応じた速度変化設定です。")]
    public HandChaserAdaptiveSpeedSettings adaptiveSpeed = HandChaserAdaptiveSpeedSettings.Default;

    [Header("ヒットボックス")]
    [Tooltip("部屋サイズに応じてヒットボックスを自動調整するかどうかです。")]
    public bool autoAdjustHitbox = true;

    [Tooltip("ヒットボックスを視覚化するかどうかです。")]
    public bool visualizeHitbox = true;

    [Tooltip("ヒットボックス視覚化の色です。")]
    public Color hitboxColor = new Color(1f, 0f, 0f, 0.8f);

    [Header("デバッグ")]
    [Tooltip("デバッグログを出すかどうかです。")]
    public bool enableDebugLog = false;

    public static HandChaserSettings Default => new HandChaserSettings();

    public void CopyFrom(HandChaserSettings source)
    {
        if (source == null)
        {
            return;
        }

        startActive = source.startActive;
        hideUntilActivated = source.hideUntilActivated;

        useSpawnPositionOnActivate = source.useSpawnPositionOnActivate;
        spawnPositionOnActivate = source.spawnPositionOnActivate;

        playerTag = string.IsNullOrWhiteSpace(source.playerTag)
            ? "Player"
            : source.playerTag;

        killPlayerOnContact = source.killPlayerOnContact;
        disableAfterKill = source.disableAfterKill;

        movement = source.movement;
        adaptiveSpeed = source.adaptiveSpeed;

        autoAdjustHitbox = source.autoAdjustHitbox;
        visualizeHitbox = source.visualizeHitbox;
        hitboxColor = source.hitboxColor;

        enableDebugLog = source.enableDebugLog;
    }

    public static HandChaserSettings CloneFrom(HandChaserSettings source)
    {
        HandChaserSettings clone = new HandChaserSettings();
        clone.CopyFrom(source);
        return clone;
    }
}