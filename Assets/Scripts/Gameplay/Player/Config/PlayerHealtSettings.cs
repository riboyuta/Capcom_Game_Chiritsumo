using System;
using UnityEngine;

[Serializable]
public sealed class PlayerHealthSettings
{
    [Header("最大体力")]
    [Tooltip("プレイヤーの最大 HP です。InitializeHealth と復帰時に currentHealth の基準値として使います。")]
    [Min(1)] public int maxHealth = 3;

    [Header("無敵時間: 継続秒数")]
    [Tooltip("ダメージを受けた後に被ダメージを無効化する時間です。")]
    [Min(0f)] public float invincibilityDuration = 1.0f;

    [Header("死亡後の復帰待機時間(秒)")]
    [Tooltip("死亡シーケンス開始からチェックポイント復帰まで待機する秒数です。")]
    [Min(0f)] public float respawnDelay = 1.0f;

    [Header("デバッグ: 即死要求")]
    [Tooltip("true にすると 1 回だけ死亡要求を出し、実行後に自動で false に戻します。")]
    public bool debugRequestDeath = false;

    [Header("デバッグ: ハザード死要求")]
    [Tooltip("true にすると 1 回だけハザード死要求を出し、実行後に自動で false に戻します。")]
    public bool debugRequestHazardDeath = false;
}