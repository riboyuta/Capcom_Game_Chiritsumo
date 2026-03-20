using UnityEngine;

// EnemyChase システム全体の司令塔。
// 全体圧・攻撃采配・DeathZone 同期の入口を一元化する。
public sealed partial class EnemyChaseManager : MonoBehaviour
{
    [Header("参照")]
    [Tooltip("追跡対象となるプレイヤーの Transform です。攻撃ターゲット座標の取得に使用します。")]
    [SerializeField] private Transform m_playerTransform;

    [Header("参照")]
    [Tooltip("左手ユニットの制御コンポーネントです。全体圧と攻撃命令を渡します。")]
    [SerializeField] private EnemyUnitController m_leftHand;

    [Header("参照")]
    [Tooltip("右手ユニットの制御コンポーネントです。全体圧と攻撃命令を渡します。")]
    [SerializeField] private EnemyUnitController m_rightHand;

    [Header("参照")]
    [Tooltip("左から迫る即死ラインを担当する EnemyDeathZone コンポーネントです。")]
    [SerializeField] private EnemyDeathZone m_deathZone;

    [Header("参照")]
    [Tooltip("EnemyChase 全体の調整値を保持する ScriptableObject です。")]
    [SerializeField] private EnemyConfig m_config;

    [Header("初期設定")]
    [Tooltip("ゲーム開始時の圧基準 WorldX です。ここから PressureSpeed で右方向へ進みます。")]
    [SerializeField] private float m_initialPressureX = -12.0f;

    [Header("デバッグ")]
    [Tooltip("有効にすると攻撃指示とフォールバックの情報を日本語ログで出力します。")]
    [SerializeField] private bool m_showDebugLog;

    private float m_pressureX;
    private float m_attackTimer;
    private bool m_nextAttackLeft = true;

    private void Awake()
    {
        // 初期圧位置を保存し、開始直後に各手へ同期する。
        m_pressureX = m_initialPressureX;
        BroadcastPressure();
        SyncDeathZone();
    }

    // 毎フレームの進行管理。
    // 圧進行・攻撃采配・DeathZone 同期をこの順で呼び出す。
    private void Update()
    {
        if (m_config == null)
        {
            return;
        }

        TickAdvance(Time.deltaTime);
        TickAttackCoordinator(Time.deltaTime);
        SyncDeathZone();
    }

    private void SyncDeathZone()
    {
        // 圧位置から計算した基準 X を DeathZone に反映する。
        if (m_deathZone == null)
        {
            return;
        }

        m_deathZone.SetWorldX(CalculateDeathZoneX());
    }

    private void LogDebug(string message)
    {
        if (!m_showDebugLog)
        {
            return;
        }

        Debug.Log($"[EnemyChaseManager] {message}", this);
    }
}
