using UnityEngine;

// 部屋に配置される敵の種類を定義する列挙型です。
public enum RoomEnemyType
{
    None,           // 敵なし
    HandChaser,     // 手の壁型の敵
    ShadowChaser,   // 影追跡型の敵
    SonarCharger    // ソナー突進型の敵
}

// 敵のデフォルト設定を一元管理し、各部屋の敵に適用するシステムです。
/// 
// 機能：
// - 敵タイプ別のデフォルト設定を保持
// - シーン開始時または手動で、全 RoomEnemySetting に設定を適用
// - Room 単位での設定適用もサポート

[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public sealed class RoomEnemySystem : MonoBehaviour
{
    [Header("Inspector 表示対象")]
    [Tooltip("RoomEnemySystem の Inspector に表示する敵設定です。")]
    [SerializeField] private RoomEnemyType editingEnemyType = RoomEnemyType.HandChaser;

    [Header("起動時適用")]
    [Tooltip("シーン開始時に、RoomEnemySystem のデフォルト設定を各 RoomEnemySetting の敵へ適用するかです。")]
    [SerializeField] private bool applyDefaultsOnStart = true;

    [Header("検索対象ルート")]
    [Tooltip("RoomEnemySetting を探す起点です。未設定ならシーン全体から探します。RoomRoot などを指定すると検索範囲を絞れます。")]
    [SerializeField] private Transform searchRoot;

    [Header("HandChaser デフォルト設定")]
    [Tooltip("HandChaserEnemy 全体のデフォルト調整値です。")]
    [SerializeField] private HandChaserSettings defaultHandChaserSettings = HandChaserSettings.Default;

    [Header("ShadowChaser デフォルト設定")]
    [Tooltip("ShadowChaserEnemy 全体のデフォルト調整値です。")]
    [SerializeField] private ShadowChaserSettings defaultShadowChaserSettings = ShadowChaserSettings.Default;

    [Header("SonarCharger デフォルト設定")]
    [Tooltip("SonarChargerEnemy 全体のデフォルト調整値です。")]
    [SerializeField] private SonarChargerSettings defaultSonarChargerSettings = new SonarChargerSettings();

    [Header("デバッグ")]
    [Tooltip("設定適用時のログを出すかです。")]
    [SerializeField] private bool logApplyResult = false;

    // 現在 Inspector で編集対象としている敵タイプを取得します。
    public RoomEnemyType EditingEnemyType => editingEnemyType;

    // HandChaser のデフォルト設定を取得します。
    public HandChaserSettings DefaultHandChaserSettings => defaultHandChaserSettings;

    // ShadowChaser のデフォルト設定を取得します。
    public ShadowChaserSettings DefaultShadowChaserSettings => defaultShadowChaserSettings;

    // SonarCharger のデフォルト設定を取得します。
    public SonarChargerSettings DefaultSonarChargerSettings => defaultSonarChargerSettings;

    // シーン開始時に、applyDefaultsOnStart が true なら全 RoomEnemySetting に設定を適用します。
    private void Start()
    {
        if (applyDefaultsOnStart)
        {
            ApplyDefaultSettingsToAllRoomEnemySettings();
        }
    }

    // シーン内の全 RoomEnemySetting を検索し、デフォルト設定を適用します。
    [ContextMenu("全 RoomEnemySetting にデフォルト敵設定を適用")]
    public void ApplyDefaultSettingsToAllRoomEnemySettings()
    {
        RoomEnemySetting[] settings = FindRoomEnemySettings();

        if (settings == null || settings.Length == 0)
        {
            LogApply("[RoomEnemySystem] RoomEnemySetting が見つかりません。");
            return;
        }

        for (int i = 0; i < settings.Length; i++)
        {
            ApplyDefaultSettingsToRoomEnemySetting(settings[i]);
        }
    }

    // 指定された Room に関連する RoomEnemySetting を検索し、デフォルト設定を適用します。
    public void ApplyDefaultSettingsToRoom(Room room)
    {
        if (room == null)
        {
            return;
        }

        RoomEnemySetting setting = room.GetComponent<RoomEnemySetting>();

        if (setting == null)
        {
            setting = room.GetComponentInChildren<RoomEnemySetting>(true);
        }

        ApplyDefaultSettingsToRoomEnemySetting(setting);
    }

    // 個別の RoomEnemySetting にデフォルト設定を適用します。
    public void ApplyDefaultSettingsToRoomEnemySetting(RoomEnemySetting setting)
    {
        if (setting == null)
        {
            return;
        }

        if (!setting.ApplySystemDefault)
        {
            return;
        }

        switch (setting.EnemyType)
        {
            case RoomEnemyType.HandChaser:
                ApplyHandChaserDefault(setting);
                break;

            case RoomEnemyType.ShadowChaser:
                ApplyShadowChaserDefault(setting);
                break;

            case RoomEnemyType.SonarCharger:
                ApplySonarChargerDefault(setting);
                break;

            case RoomEnemyType.None:
            default:
                LogApply($"[RoomEnemySystem] {setting.name} は EnemyType=None のため適用しません。");
                break;
        }
    }

    // HandChaser のデフォルト設定を取得します。
    public HandChaserSettings GetDefaultHandChaserSettings()
    {
        return defaultHandChaserSettings;
    }

    // ShadowChaser のデフォルト設定を取得します。
    public ShadowChaserSettings GetDefaultShadowChaserSettings()
    {
        return defaultShadowChaserSettings;
    }

    // SonarCharger のデフォルト設定を取得します。
    public SonarChargerSettings GetDefaultSonarChargerSettings()
    {
        return defaultSonarChargerSettings;
    }

    // ランタイムで生成された個別の敵にデフォルト設定を適用します。
    public void ApplyDefaultSettingsToEnemy(Component enemyComponent)
    {
        if (enemyComponent == null)
        {
            return;
        }

        switch (enemyComponent)
        {
            case HandChaserEnemy handChaser:
                handChaser.ApplySettings(defaultHandChaserSettings);
                LogApply($"[RoomEnemySystem] HandChaserEnemy {handChaser.name} にデフォルト設定を適用しました。");
                break;

            case ShadowChaserEnemy shadowChaser:
                shadowChaser.ApplySettings(defaultShadowChaserSettings);
                LogApply($"[RoomEnemySystem] ShadowChaserEnemy {shadowChaser.name} にデフォルト設定を適用しました。");
                break;

            case SonarChargerEnemy sonarCharger:
                sonarCharger.ApplySettings(defaultSonarChargerSettings);
                LogApply($"[RoomEnemySystem] SonarChargerEnemy {sonarCharger.name} にデフォルト設定を適用しました。");
                break;

            default:
                Debug.LogWarning($"[RoomEnemySystem] 未対応の敵タイプです: {enemyComponent.GetType().Name}", enemyComponent);
                break;
        }
    }

    // 指定された敵タイプのデフォルト設定を適用します。
    public void ApplyDefaultSettingsByType(RoomEnemyType enemyType, Transform enemyTransform)
    {
        if (enemyTransform == null)
        {
            return;
        }

        switch (enemyType)
        {
            case RoomEnemyType.HandChaser:
            {
                HandChaserEnemy enemy = enemyTransform.GetComponent<HandChaserEnemy>();
                if (enemy != null)
                {
                    enemy.ApplySettings(defaultHandChaserSettings);
                    LogApply($"[RoomEnemySystem] HandChaserEnemy {enemy.name} にデフォルト設定を適用しました。");
                }
                break;
            }

            case RoomEnemyType.ShadowChaser:
            {
                ShadowChaserEnemy enemy = enemyTransform.GetComponent<ShadowChaserEnemy>();
                if (enemy != null)
                {
                    enemy.ApplySettings(defaultShadowChaserSettings);
                    LogApply($"[RoomEnemySystem] ShadowChaserEnemy {enemy.name} にデフォルト設定を適用しました。");
                }
                break;
            }

            case RoomEnemyType.SonarCharger:
            {
                SonarChargerEnemy enemy = enemyTransform.GetComponent<SonarChargerEnemy>();
                if (enemy != null)
                {
                    enemy.ApplySettings(defaultSonarChargerSettings);
                    LogApply($"[RoomEnemySystem] SonarChargerEnemy {enemy.name} にデフォルト設定を適用しました。");
                }
                break;
            }

            case RoomEnemyType.None:
            default:
                LogApply($"[RoomEnemySystem] EnemyType={enemyType} は適用対象外です。");
                break;
        }
    }

    // RoomEnemySetting を検索します。
    private RoomEnemySetting[] FindRoomEnemySettings()
    {
        if (searchRoot != null)
        {
            return searchRoot.GetComponentsInChildren<RoomEnemySetting>(true);
        }

        return FindObjectsByType<RoomEnemySetting>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
    }

    // HandChaser 型の敵にデフォルト設定を適用します。
    private void ApplyHandChaserDefault(RoomEnemySetting setting)
    {
        Transform root = setting.SearchRoot;
        HandChaserEnemy[] enemies = root.GetComponentsInChildren<HandChaserEnemy>(true);

        if (enemies == null || enemies.Length == 0)
        {
            LogMissingEnemy(setting, "HandChaserEnemy");
            return;
        }

        for (int i = 0; i < enemies.Length; i++)
        {
            if (enemies[i] == null)
            {
                continue;
            }

            HandChaserSettings finalSettings = BuildHandChaserSettings(setting);
            enemies[i].ApplySettings(finalSettings);
        }

        LogApply($"[RoomEnemySystem] {setting.name} の HandChaserEnemy にデフォルト設定を適用しました。count={enemies.Length}");
    }

    // ShadowChaser 型の敵にデフォルト設定を適用します。
    private void ApplyShadowChaserDefault(RoomEnemySetting setting)
    {
        Transform root = setting.SearchRoot;
        ShadowChaserEnemy[] enemies = root.GetComponentsInChildren<ShadowChaserEnemy>(true);

        if (enemies == null || enemies.Length == 0)
        {
            LogMissingEnemy(setting, "ShadowChaserEnemy");
            return;
        }

        for (int i = 0; i < enemies.Length; i++)
        {
            if (enemies[i] == null)
            {
                continue;
            }

            enemies[i].ApplySettings(defaultShadowChaserSettings);
        }

        LogApply($"[RoomEnemySystem] {setting.name} の ShadowChaserEnemy にデフォルト設定を適用しました。count={enemies.Length}");
    }

    // SonarCharger 型の敵にデフォルト設定を適用します。
    private void ApplySonarChargerDefault(RoomEnemySetting setting)
    {
        Transform root = setting.SearchRoot;
        SonarChargerEnemy[] enemies = root.GetComponentsInChildren<SonarChargerEnemy>(true);

        if (enemies == null || enemies.Length == 0)
        {
            LogMissingEnemy(setting, "SonarChargerEnemy");
            return;
        }

        for (int i = 0; i < enemies.Length; i++)
        {
            if (enemies[i] == null)
            {
                continue;
            }

            enemies[i].ApplySettings(defaultSonarChargerSettings);
        }

        LogApply($"[RoomEnemySystem] {setting.name} の SonarChargerEnemy にデフォルト設定を適用しました。count={enemies.Length}");
    }

    private HandChaserSettings BuildHandChaserSettings(RoomEnemySetting setting)
    {
        HandChaserSettings result = HandChaserSettings.CloneFrom(defaultHandChaserSettings);

        if (setting == null)
        {
            return result;
        }

        HandChaserMovementSettings movement = setting.HandMovementSettings;

        movement.moveSpeed = Mathf.Max(0.0f, movement.moveSpeed);

        if (movement.moveDirection == MoveDirection.Custom)
        {
            movement.customMoveAxis = movement.customMoveAxis.sqrMagnitude > 0.0f
                ? movement.customMoveAxis.normalized
                : Vector3.right;
        }

        result.movement = movement;
        return result;
    }

    // 敵コンポーネントが見つからなかった場合の警告ログを出力します。
    private void LogMissingEnemy(RoomEnemySetting setting, string componentName)
    {
        if (setting == null || !setting.LogMissingTarget)
        {
            return;
        }

        Debug.LogWarning(
            $"[RoomEnemySystem] {setting.name} は EnemyType={setting.EnemyType} ですが、子階層に {componentName} が見つかりません。",
            setting);
    }

    // 設定適用結果のログを出力します。
    private void LogApply(string message)
    {
        if (!logApplyResult)
        {
            return;
        }

        Debug.Log(message, this);
    }
}