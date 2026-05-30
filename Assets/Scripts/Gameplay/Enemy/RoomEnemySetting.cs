using UnityEngine;

// Room ごとの敵設定を管理するコンポーネントです。
//
// 役割：
// - Room で使用する敵タイプを指定
// - 敵の検索範囲を定義
// - RoomEnemySystem のデフォルト設定適用を制御
//
// 使い方：
// 1. Room GameObject または Room 配下にアタッチ
// 2. enemyType で使用する敵を選択
// 3. 必要に応じて enemyRoot で検索範囲を限定
// 4. applySystemDefault を true にすると、RoomEnemySystem の設定が自動適用される
[DisallowMultipleComponent]
public sealed class RoomEnemySetting : MonoBehaviour
{
    [Header("敵タイプ")]
    [Tooltip("この Room で使用する敵の種類です。")]
    [SerializeField] private RoomEnemyType enemyType = RoomEnemyType.None;

    [Header("敵検索ルート")]
    [Tooltip("敵を探す起点です。未設定ならこの GameObject の子階層から探します。")]
    [SerializeField] private Transform enemyRoot;

    [Header("RoomEnemySystem デフォルト適用")]
    [Tooltip("RoomEnemySystem のデフォルト設定をこの Room の敵へ適用するかです。")]
    [SerializeField] private bool applySystemDefault = true;

    [Header("警告ログ")]
    [Tooltip("指定した敵タイプの敵が見つからなかった場合に警告を出すかです。")]
    [SerializeField] private bool logMissingTarget = true;

    [Header("HandChaser 移動設定")]
    [Tooltip("この Room の HandChaser に使う移動設定です。")]
    [SerializeField]
    private HandChaserMovementSettings handMovementSettings =
    HandChaserMovementSettings.Default;

    // この Room で使用する敵タイプを取得します。
    public RoomEnemyType EnemyType => enemyType;

    // RoomEnemySystem のデフォルト設定を適用するかどうかを取得します。
    public bool ApplySystemDefault => applySystemDefault;

    // 敵が見つからなかった場合に警告ログを出すかどうかを取得します。
    public bool LogMissingTarget => logMissingTarget;

    // HandChaser に使う移動設定を取得します。
    public HandChaserMovementSettings HandMovementSettings => handMovementSettings;

    // 敵を検索する起点の Transform を取得します。
    // enemyRoot が設定されていればそれを返し、未設定なら自身の transform を返します。
    public Transform SearchRoot
    {
        get
        {
            if (enemyRoot != null)
            {
                return enemyRoot;
            }

            return transform;
        }
    }

    // Context Menu から RoomEnemySystem のデフォルト設定を適用します。
    [ContextMenu("RoomEnemySystem のデフォルト設定を適用")]
    private void ApplySystemDefaultFromContextMenu()
    {
        RoomEnemySystem system = FindFirstObjectByType<RoomEnemySystem>();

        if (system == null)
        {
            Debug.LogWarning("[RoomEnemySetting] RoomEnemySystem が見つかりません。", this);
            return;
        }

        system.ApplyDefaultSettingsToRoomEnemySetting(this);
    }
}