using UnityEngine;

// EnemyChase 系の調整値をまとめて保持する ScriptableObject。
// 速度、状態時間、攻撃時間などのチューニングをここへ集約する。
[CreateAssetMenu(fileName = "EnemyConfig", menuName = "Game/Enemy/Enemy Config")]
public sealed class EnemyConfig : ScriptableObject
{
    [Header("全体圧の設定")]
    [Tooltip("左側から迫る全体圧の速度です。値が大きいほどプレイヤーを速く追い詰めます。")]
    [SerializeField] private float m_pressureSpeed = 1.5f;

    [Header("攻撃周期の設定")]
    [Tooltip("手が攻撃指示を出す間隔です。短いほど高頻度で攻撃します。")]
    [SerializeField] private float m_attackInterval = 2.0f;

    [Header("状態時間の設定")]
    [Tooltip("攻撃前の溜め時間です。EnemyUnitController の Windup 状態に使用します。")]
    [SerializeField] private float m_windupDuration = 0.5f;

    [Tooltip("攻撃後の硬直時間です。EnemyUnitController の Recovery 状態に使用します。")]
    [SerializeField] private float m_recoveryDuration = 0.4f;

    [Header("即死ラインの設定")]
    [Tooltip("全体圧の基準位置に対して、即死ラインをどれだけ前後へずらすかを設定します。")]
    [SerializeField] private float m_deathZoneOffset = 0.0f;

    [Header("Grab 攻撃の設定")]
    [Tooltip("Grab 攻撃全体の所要時間です。前半で伸び、後半で待機位置へ戻ります。")]
    [SerializeField] private float m_grabDuration = 0.6f;

    [Tooltip("Grab 攻撃の 3D Trigger 判定を有効化し始める時刻です。")]
    [SerializeField] private float m_grabActiveStartTime = 0.15f;

    [Tooltip("Grab 攻撃の 3D Trigger 判定を無効化する時刻です。")]
    [SerializeField] private float m_grabActiveEndTime = 0.35f;

    [Header("Smash 攻撃の設定")]
    [Tooltip("Smash 攻撃全体の所要時間です。溜め、振り下ろし、戻りをこの時間内で行います。")]
    [SerializeField] private float m_smashDuration = 0.7f;

    [Tooltip("Smash 攻撃の 3D Trigger 判定を有効化し始める時刻です。")]
    [SerializeField] private float m_smashActiveStartTime = 0.20f;

    [Tooltip("Smash 攻撃の 3D Trigger 判定を無効化する時刻です。")]
    [SerializeField] private float m_smashActiveEndTime = 0.45f;

    [Tooltip("Smash 攻撃前に手をどれだけ後方へ引くかを設定します。ローカル座標で使用します。")]
    [SerializeField] private Vector3 m_smashWindupOffset = new Vector3(-0.5f, 0.75f, 0.0f);

    public float PressureSpeed => Mathf.Max(0.0f, m_pressureSpeed);
    public float AttackInterval => Mathf.Max(0.01f, m_attackInterval);
    public float WindupDuration => Mathf.Max(0.01f, m_windupDuration);
    public float RecoveryDuration => Mathf.Max(0.01f, m_recoveryDuration);
    public float DeathZoneOffset => m_deathZoneOffset;

    public float GrabDuration => Mathf.Max(0.01f, m_grabDuration);
    public float GrabActiveStartTime => Mathf.Clamp(m_grabActiveStartTime, 0.0f, GrabDuration);
    public float GrabActiveEndTime => Mathf.Clamp(m_grabActiveEndTime, GrabActiveStartTime, GrabDuration);

    public float SmashDuration => Mathf.Max(0.01f, m_smashDuration);
    public float SmashActiveStartTime => Mathf.Clamp(m_smashActiveStartTime, 0.0f, SmashDuration);
    public float SmashActiveEndTime => Mathf.Clamp(m_smashActiveEndTime, SmashActiveStartTime, SmashDuration);
    public Vector3 SmashWindupOffset => m_smashWindupOffset;
}