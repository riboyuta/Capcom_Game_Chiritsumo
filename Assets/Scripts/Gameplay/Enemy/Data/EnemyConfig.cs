using UnityEngine;

// EnemyChase 系の調整値をまとめて保持する ScriptableObject。
// 速度、状態時間、攻撃時間などのチューニングをここへ集約する。
[CreateAssetMenu(fileName = "EnemyConfig", menuName = "Game/Enemy/Enemy Config")]
public sealed class EnemyConfig : ScriptableObject
{
    [Header("全体圧の設定")]
    [Header("圧の速度")]
    [Tooltip("左側から迫る全体圧の速度です。値が大きいほどプレイヤーを速く追い詰めます。")]
    [SerializeField] private float pressureSpeed = 1.5f;

    [Header("攻撃周期の設定")]
    [Header("攻撃間隔")]
    [Tooltip("手が攻撃指示を出す間隔です。短いほど高頻度で攻撃します。")]
    [SerializeField] private float attackInterval = 2.0f;

    [Header("状態時間の設定")]
    [Header("溜め時間")]
    [Tooltip("攻撃前の溜め時間です。EnemyUnitController の Windup 状態に使用します。")]
    [SerializeField] private float windupDuration = 0.5f;

    [Header("硬直時間")]
    [Tooltip("攻撃後の硬直時間です。EnemyUnitController の Recovery 状態に使用します。")]
    [SerializeField] private float recoveryDuration = 0.4f;

    [Header("即死ラインの設定")]
    [Header("即死ラインオフセット")]
    [Tooltip("全体圧の基準位置に対して、即死ラインをどれだけ前後へずらすかを設定します。")]
    [SerializeField] private float deathZoneOffset = 0.0f;

    [Header("Grab 攻撃の設定")]
    [Header("Grab総時間")]
    [Tooltip("Grab 攻撃全体の所要時間です。前半で伸び、後半で待機位置へ戻ります。")]
    [SerializeField] private float grabDuration = 0.6f;

    [Header("Grab判定開始時刻")]
    [Tooltip("Grab 攻撃の 3D Trigger 判定を有効化し始める時刻です。")]
    [SerializeField] private float grabActiveStartTime = 0.15f;

    [Header("Grab判定終了時刻")]
    [Tooltip("Grab 攻撃の 3D Trigger 判定を無効化する時刻です。")]
    [SerializeField] private float grabActiveEndTime = 0.35f;

    [Header("Smash 攻撃の設定")]
    [Header("Smash総時間")]
    [Tooltip("Smash 攻撃全体の所要時間です。溜め、振り下ろし、戻りをこの時間内で行います。")]
    [SerializeField] private float smashDuration = 0.7f;

    [Header("Smash判定開始時刻")]
    [Tooltip("Smash 攻撃の 3D Trigger 判定を有効化し始める時刻です。")]
    [SerializeField] private float smashActiveStartTime = 0.20f;

    [Header("Smash判定終了時刻")]
    [Tooltip("Smash 攻撃の 3D Trigger 判定を無効化する時刻です。")]
    [SerializeField] private float smashActiveEndTime = 0.45f;

    [Header("Smash溜めオフセット")]
    [Tooltip("Smash 攻撃前に手をどれだけ後方へ引くかを設定します。ローカル座標で使用します。")]
    [SerializeField] private Vector3 smashWindupOffset = new Vector3(-0.5f, 0.75f, 0.0f);

    // 以下のプロパティは、設定値を外部から安全に取得するためのもの。
    // 値の妥当性を保証するため、最小値チェックなどを行う。

    // 全体圧の速度（負の値を防ぐ）
    public float PressureSpeed => Mathf.Max(0.0f, pressureSpeed);
    // 攻撃間隔（最小値 0.01 秒を保証）
    public float AttackInterval => Mathf.Max(0.01f, attackInterval);
    // 溜め時間（最小値 0.01 秒を保証）
    public float WindupDuration => Mathf.Max(0.01f, windupDuration);
    // 硬直時間（最小値 0.01 秒を保証）
    public float RecoveryDuration => Mathf.Max(0.01f, recoveryDuration);
    // 即死ゾーンのオフセット
    public float DeathZoneOffset => deathZoneOffset;
    // Grab 攻撃の総時間（最小値 0.01 秒を保証）
    public float GrabDuration => Mathf.Max(0.01f, grabDuration);
    // Grab 攻撃の有効開始時刻（0 ～ Duration の範囲にクランプ）
    public float GrabActiveStartTime => Mathf.Clamp(grabActiveStartTime, 0.0f, GrabDuration);
    // Grab 攻撃の有効終了時刻（開始時刻 ～ Duration の範囲にクランプ）
    public float GrabActiveEndTime => Mathf.Clamp(grabActiveEndTime, GrabActiveStartTime, GrabDuration);
    // Smash 攻撃の総時間（最小値 0.01 秒を保証）
    public float SmashDuration => Mathf.Max(0.01f, smashDuration);
    // Smash 攻撃の有効開始時刻（0 ～ Duration の範囲にクランプ）
    public float SmashActiveStartTime => Mathf.Clamp(smashActiveStartTime, 0.0f, SmashDuration);
    // Smash 攻撃の有効終了時刻（開始時刻 ～ Duration の範囲にクランプ）
    public float SmashActiveEndTime => Mathf.Clamp(smashActiveEndTime, SmashActiveStartTime, SmashDuration);
    // Smash 攻撃の溜めオフセット
    public Vector3 SmashWindupOffset => smashWindupOffset;
}