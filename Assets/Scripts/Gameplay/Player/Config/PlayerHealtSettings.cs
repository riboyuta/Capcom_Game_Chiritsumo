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

    [Header("体力: 無敵モード")]
    [Tooltip("常時ダメージを無効化するデバッグ用フラグです。TakeDamage と Kill の受理判定に使います。有効にすると被ダメージ確認はしにくくなりますが、ステージ検証や挙動確認を安全に行えます。")]
    public bool invincible = false;

    [Header("デバッグ: 即死要求")]
    [Tooltip("true にすると 1 回だけ死亡要求を出し、実行後に自動で false に戻します。")]
    public bool debugRequestDeath = false;

    [Header("デバッグ: ハザード死要求")]
    [Tooltip("true にすると 1 回だけハザード死要求を出し、実行後に自動で false に戻します。")]
    public bool debugRequestHazardDeath = false;
}