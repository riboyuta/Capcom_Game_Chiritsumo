using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemyProximityTimeAssist : MonoBehaviour, IRespawnResettable
{
    [Header("参照 / ルーム管理")]
    [Tooltip("現在の部屋と部屋遷移状態を確認するための RoomManager です。未設定の場合は Awake で自動取得します。")]
    [SerializeField] private RoomManager roomManager;

    [Header("参照 / 対象ルーム")]
    [Tooltip("この敵接近時の時間補助を有効にする対象ルームです。未設定の場合はルーム制限なしで動作します。")]
    [SerializeField] private Room targetRoom;

    [Header("参照 / プレイヤー")]
    [Tooltip("プレイヤーの位置、ダッシュ入力受付フレーム、ダッシュ回復処理を参照するための PlayerFacade です。未設定の場合は Awake で自動取得します。")]
    [SerializeField] private PlayerFacade playerFacade;

    [Header("参照 / 追跡敵の移動")]
    [Tooltip("追跡敵が有効かどうか、移動方向、カスタム移動軸を取得するためのコンポーネントです。")]
    [SerializeField] private HandChaserMovement handChaserMovement;

    [Header("参照 / 追跡敵の当たり判定")]
    [Tooltip("追跡敵の前面位置を計算するために使用する BoxCollider です。プレイヤーとの前方距離判定に使います。")]
    [SerializeField] private BoxCollider handChaserBoxCollider;

    [Header("初回補助設定 / 発動距離")]
    [Tooltip("初回補助を発動する、追跡敵の前面からプレイヤーまでの距離です。値が小さいほどギリギリで発動します。")]
    [SerializeField, Min(0f)] private float firstTriggerDistance = 2f;

    [Header("初回補助設定 / 時間倍率")]
    [Tooltip("初回補助中の Time.timeScale です。0 にすると完全停止、1 に近いほど通常速度に近づきます。")]
    [SerializeField, Range(0f, 1f)] private float firstTimeScale = 0f;

    [Header("初回補助設定 / 最大継続時間")]
    [Tooltip("初回補助が自動終了するまでの最大時間です。unscaledDeltaTime 基準で計測します。")]
    [SerializeField, Min(0f)] private float firstMaxDuration = 0.18f;

    [Header("2回目以降補助設定 / 発動距離")]
    [Tooltip("2回目以降の補助を発動する、追跡敵の前面からプレイヤーまでの距離です。")]
    [SerializeField, Min(0f)] private float repeatTriggerDistance = 2.5f;

    [Header("2回目補助設定 / 時間倍率")]
    [Tooltip("2回目の補助中の Time.timeScale です。0 に近いほど強い時間補助になります。")]
    [SerializeField, Range(0f, 1f)] private float repeatTimeScale = 0.25f;

    [Header("3回目以降補助設定 / 時間倍率")]
    [Tooltip("3回目以降の補助中の Time.timeScale です。回数制限に達するまで、この値を使い続けます。")]
    [SerializeField, Range(0f, 1f)] private float thirdAndLaterTimeScale = 0.15f;

    [Header("2回目以降補助設定 / 最大継続時間")]
    [Tooltip("2回目以降の補助が自動終了するまでの最大時間です。unscaledDeltaTime 基準で計測します。")]
    [SerializeField, Min(0f)] private float repeatMaxDuration = 0.35f;

    [Header("共通設定 / 補助開始待機時間")]
    [Tooltip("追跡開始直後に補助が即発動しないようにする待機時間です。敵出現直後の誤発動防止に使います。")]
    [SerializeField, Min(0f)] private float assistEnableDelayAfterEnemySpawn = 0.25f;

    [Header("共通設定 / クールダウン")]
    [Tooltip("補助終了後、次の補助が発動可能になるまでの待機時間です。連続発動を防ぎます。")]
    [SerializeField, Min(0f)] private float cooldownTime = 0.5f;

    [Header("解除設定 / ダッシュ入力")]
    [Tooltip("有効にすると、補助開始後にプレイヤーのダッシュ入力が受理された時点で補助を解除します。")]
    [SerializeField] private bool releaseOnDashInput = true;

    [Header("解除設定 / 最大継続時間")]
    [Tooltip("有効にすると、1回目または2回目以降の最大継続時間に達した時点で補助を解除します。時間計測は Time.timeScale の影響を受けません。")]
    [SerializeField] private bool useMaxDuration = true;

    [Header("解除設定 / 距離解除")]
    [Tooltip("有効にすると、補助中に追跡敵の前面からプレイヤーまでの距離が解除距離以上になった時点で補助を解除します。")]
    [SerializeField] private bool releaseOnDistance;

    [Header("解除設定 / 距離解除")]
    [Tooltip("距離解除を行う前面距離です。発動距離より大きい値にすると、補助開始直後の即解除を避けやすくなります。")]
    [SerializeField, Min(0f)] private float releaseDistance = 5f;

    [Header("共通設定 / 最大補助回数")]
    [Tooltip("有効にすると、1回の追跡中に発動できる補助回数を maxAssistCount で制限します。無効時は回数制限なしで発動します。")]
    [SerializeField] private bool useAssistCountLimit = true;

    [Header("共通設定 / 最大補助回数")]
    [Tooltip("1回の追跡中に発動できる補助の最大回数です。0 にすると補助は発動しません。")]
    [SerializeField, Min(0)] private int maxAssistCount = 3;

    [Header("共通設定 / デバッグログ")]
    [Tooltip("補助開始、補助終了、時間倍率復元などのログを Console に出力するかどうかです。")]
    [SerializeField] private bool showDebugLog;

    [Header("共通設定 / ギズモ表示")]
    [Tooltip("Scene ビューで追跡敵の範囲、前面位置、発動距離の目安を表示するかどうかです。")]
    [SerializeField] private bool showGizmos = true;

    [Header("デバッグ確認 / 現在の前方距離")]
    [Tooltip("追跡敵の前面からプレイヤーまでの現在距離です。実行中の発動条件確認に使います。")]
    [SerializeField] private float currentFrontDistance;

    [Header("デバッグ確認 / 追跡開始からの経過時間")]
    [Tooltip("追跡開始を検知してからの経過時間です。補助開始待機時間の確認に使います。")]
    [SerializeField] private float elapsedSinceChaseStart;

    [Header("デバッグ確認 / 現在の補助回数")]
    [Tooltip("現在の追跡中に補助が何回発動したかを確認するための値です。")]
    [SerializeField] private int currentAssistCount;

    private bool hasDetectedChaseStart;
    private float chaseStartUnscaledTime;
    private float cooldownTimer;
    private bool isAssisting;
    private float assistTimer;
    private float activeAssistMaxDuration;
    private int dashBaselineFrame = -1;

    private bool isTimeOverrideActive;
    private float originalTimeScale = 1f;
    private float originalFixedDeltaTime;

    private const float MinFixedDeltaTimeScale = 0.0001f;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnValidate()
    {
        firstTriggerDistance = Mathf.Max(0f, firstTriggerDistance);
        firstTimeScale = Mathf.Max(0f, firstTimeScale);
        firstMaxDuration = Mathf.Max(0f, firstMaxDuration);
        repeatTriggerDistance = Mathf.Max(0f, repeatTriggerDistance);
        repeatTimeScale = Mathf.Max(0f, repeatTimeScale);
        thirdAndLaterTimeScale = Mathf.Max(0f, thirdAndLaterTimeScale);
        repeatMaxDuration = Mathf.Max(0f, repeatMaxDuration);
        assistEnableDelayAfterEnemySpawn = Mathf.Max(0f, assistEnableDelayAfterEnemySpawn);
        cooldownTime = Mathf.Max(0f, cooldownTime);
        releaseDistance = Mathf.Max(0f, releaseDistance);
        maxAssistCount = Mathf.Max(0, maxAssistCount);
    }

    private void Update()
    {
        if (!IsRoomAssistAllowed())
        {
            ForceClearRunningAssist("target room exit or room transition");
            ResetChaseDetection();
            return;
        }

        if (handChaserMovement == null || !handChaserMovement.IsActive)
        {
            ForceClearRunningAssist("chaser inactive");
            ResetChaseDetection();
            return;
        }

        if (isAssisting)
        {
            UpdateRunningAssist();
            return;
        }

        UpdateChaseDetection();

        if (!hasDetectedChaseStart)
        {
            return;
        }

        if (cooldownTimer > 0f)
        {
            cooldownTimer = Mathf.Max(0f, cooldownTimer - Time.unscaledDeltaTime);
            return;
        }

        if (elapsedSinceChaseStart < assistEnableDelayAfterEnemySpawn)
        {
            return;
        }

        if (useAssistCountLimit && currentAssistCount >= maxAssistCount)
        {
            return;
        }

        if (!TryUpdateFrontDistance())
        {
            return;
        }

        float triggerDistance = currentAssistCount == 0 ? firstTriggerDistance : repeatTriggerDistance;
        if (currentFrontDistance <= triggerDistance)
        {
            StartAssist(currentAssistCount == 0);
        }
    }

    private void OnDisable()
    {
        ForceClearRunningAssist("OnDisable");
    }

    private void OnDestroy()
    {
        ForceClearRunningAssist("OnDestroy");
    }

    public void CaptureInitialState()
    {
        // このコンポーネントは開始時状態を追加保存する必要がない。
    }

    public void ResetToRespawnState()
    {
        isAssisting = false;
        RestoreTimeScaleIfNeeded("ResetToRespawnState");

        currentAssistCount = 0;
        cooldownTimer = 0f;
        ResetChaseDetection();
        assistTimer = 0f;
        activeAssistMaxDuration = 0f;
        dashBaselineFrame = -1;
        currentFrontDistance = 0f;
    }

    private void ResolveReferences()
    {
        if (roomManager == null)
        {
            roomManager = FindFirstObjectByType<RoomManager>();
        }

        if (playerFacade == null)
        {
            playerFacade = FindFirstObjectByType<PlayerFacade>();
        }
    }

    private bool IsRoomAssistAllowed()
    {
        if (roomManager != null && roomManager.IsTransitioning)
        {
            return false;
        }

        if (targetRoom == null)
        {
            return true;
        }

        return roomManager != null && roomManager.CurrentRoom == targetRoom;
    }

    private void UpdateChaseDetection()
    {
        if (handChaserMovement == null || !handChaserMovement.IsActive)
        {
            ResetChaseDetection();
            return;
        }

        if (!hasDetectedChaseStart)
        {
            hasDetectedChaseStart = true;
            chaseStartUnscaledTime = Time.unscaledTime;
            elapsedSinceChaseStart = 0f;
            return;
        }

        elapsedSinceChaseStart = Mathf.Max(0f, Time.unscaledTime - chaseStartUnscaledTime);
    }

    private void ResetChaseDetection()
    {
        hasDetectedChaseStart = false;
        chaseStartUnscaledTime = 0f;
        elapsedSinceChaseStart = 0f;
    }

    private void UpdateRunningAssist()
    {
        assistTimer += Time.unscaledDeltaTime;

        if (releaseOnDashInput && playerFacade != null && playerFacade.LastAcceptedDashInputFrame > dashBaselineFrame)
        {
            EndAssist("dash input accepted");
            return;
        }

        if (useMaxDuration && assistTimer >= activeAssistMaxDuration)
        {
            EndAssist("max duration");
            return;
        }

        if (ShouldReleaseByDistance())
        {
            EndAssist("release distance");
        }
    }

    private void StartAssist(bool isFirstAssist)
    {
        if (playerFacade == null)
        {
            return;
        }

        dashBaselineFrame = playerFacade.LastAcceptedDashInputFrame;
        playerFacade.TryRefillDash(DashRefillReason.TutorialAssist);

        float targetTimeScale = GetAssistTimeScale(currentAssistCount);
        activeAssistMaxDuration = isFirstAssist ? firstMaxDuration : repeatMaxDuration;
        assistTimer = 0f;
        isAssisting = true;
        currentAssistCount++;

        originalTimeScale = Time.timeScale;
        originalFixedDeltaTime = Time.fixedDeltaTime;
        isTimeOverrideActive = true;

        Time.timeScale = targetTimeScale;
        Time.fixedDeltaTime = originalFixedDeltaTime * Mathf.Max(targetTimeScale, MinFixedDeltaTimeScale);

        if (showDebugLog)
        {
            Debug.Log($"[EnemyProximityTimeAssist] Assist started. type={(isFirstAssist ? "First" : "Repeat")}, count={currentAssistCount}, distance={currentFrontDistance:F3}, timeScale={targetTimeScale:F3}", this);
        }
    }

    private float GetAssistTimeScale(int assistCountBeforeStart)
    {
        // 発動前の回数を基準に、1回目・2回目・3回目以降の時間補助強度を選ぶ。
        if (assistCountBeforeStart <= 0)
        {
            return firstTimeScale;
        }

        if (assistCountBeforeStart == 1)
        {
            return repeatTimeScale;
        }

        return thirdAndLaterTimeScale;
    }

    private bool ShouldReleaseByDistance()
    {
        if (!releaseOnDistance)
        {
            return false;
        }

        // 既存の前面距離計算を使い、敵から十分離れたときだけ補助を解除する。
        return TryUpdateFrontDistance() && currentFrontDistance >= releaseDistance;
    }

    private void EndAssist(string reason)
    {
        isAssisting = false;
        RestoreTimeScaleIfNeeded($"EndAssist: {reason}");

        assistTimer = 0f;
        activeAssistMaxDuration = 0f;
        dashBaselineFrame = -1;
        cooldownTimer = cooldownTime;

        if (showDebugLog)
        {
            Debug.Log($"[EnemyProximityTimeAssist] Assist ended. reason={reason}, cooldown={cooldownTimer:F3}", this);
        }
    }

    private void ForceClearRunningAssist(string reason)
    {
        if (!isAssisting)
        {
            RestoreTimeScaleIfNeeded(reason);
            return;
        }

        isAssisting = false;
        assistTimer = 0f;
        activeAssistMaxDuration = 0f;
        dashBaselineFrame = -1;
        RestoreTimeScaleIfNeeded(reason);

        if (showDebugLog)
        {
            Debug.Log($"[EnemyProximityTimeAssist] Running assist force-cleared. reason={reason}", this);
        }
    }

    private void RestoreTimeScaleIfNeeded(string reason)
    {
        if (!isTimeOverrideActive)
        {
            return;
        }

        Time.timeScale = originalTimeScale;
        Time.fixedDeltaTime = originalFixedDeltaTime;
        isTimeOverrideActive = false;

        if (showDebugLog)
        {
            Debug.Log($"[EnemyProximityTimeAssist] Time scale restored. reason={reason}, timeScale={originalTimeScale:F3}, fixedDeltaTime={originalFixedDeltaTime:F5}", this);
        }
    }

    private bool TryUpdateFrontDistance()
    {
        if (handChaserBoxCollider == null || playerFacade == null)
        {
            return false;
        }

        if (!TryGetMoveAxis(out Vector3 axis))
        {
            return false;
        }

        Bounds bounds = handChaserBoxCollider.bounds;
        Vector3 absAxis = new Vector3(Mathf.Abs(axis.x), Mathf.Abs(axis.y), Mathf.Abs(axis.z));
        float projectedHalfExtent =
            bounds.extents.x * absAxis.x +
            bounds.extents.y * absAxis.y +
            bounds.extents.z * absAxis.z;

        float frontPlane = Vector3.Dot(bounds.center, axis) + projectedHalfExtent;
        float playerPlane = Vector3.Dot(playerFacade.transform.position, axis);
        currentFrontDistance = playerPlane - frontPlane;
        return true;
    }

    private bool TryGetMoveAxis(out Vector3 axis)
    {
        axis = Vector3.right;

        if (handChaserMovement == null)
        {
            return false;
        }

        switch (handChaserMovement.Direction)
        {
            case MoveDirection.Right:
                axis = Vector3.right;
                break;
            case MoveDirection.Left:
                axis = Vector3.left;
                break;
            case MoveDirection.Up:
                axis = Vector3.up;
                break;
            case MoveDirection.Down:
                axis = Vector3.down;
                break;
            case MoveDirection.Custom:
                axis = handChaserMovement.CustomMoveAxis;
                break;
            default:
                axis = Vector3.right;
                break;
        }

        if (axis.sqrMagnitude <= Mathf.Epsilon)
        {
            axis = Vector3.right;
        }

        axis.Normalize();
        return true;
    }

    private void OnDrawGizmosSelected()
    {
        if (!showGizmos || handChaserBoxCollider == null)
        {
            return;
        }

        if (!TryGetMoveAxis(out Vector3 axis))
        {
            return;
        }

        Bounds bounds = handChaserBoxCollider.bounds;
        Vector3 absAxis = new Vector3(Mathf.Abs(axis.x), Mathf.Abs(axis.y), Mathf.Abs(axis.z));
        float projectedHalfExtent =
            bounds.extents.x * absAxis.x +
            bounds.extents.y * absAxis.y +
            bounds.extents.z * absAxis.z;

        Vector3 frontCenter = bounds.center + axis * projectedHalfExtent;

        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(bounds.center, bounds.size);
        Gizmos.DrawSphere(frontCenter, 0.12f);

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(frontCenter, frontCenter + axis * firstTriggerDistance);

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(frontCenter, frontCenter + axis * repeatTriggerDistance);

        if (playerFacade != null)
        {
            Gizmos.color = Color.white;
            Gizmos.DrawLine(frontCenter, playerFacade.transform.position);
        }
    }
}