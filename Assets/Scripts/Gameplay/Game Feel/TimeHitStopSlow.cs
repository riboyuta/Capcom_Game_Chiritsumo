using UnityEngine;

[DisallowMultipleComponent]
public sealed class TimeHitStopSlow : MonoBehaviour, IRespawnResettable
{
    [Header("参照 / ルーム管理")]
    [Tooltip("現在の部屋と部屋遷移状態を確認するための RoomManager です。未設定の場合は Awake で自動取得します。")]
    [SerializeField] private RoomManager roomManager;

    [Header("参照 / 対象ルーム")]
    [Tooltip("このヒットストップ・スロー補助を有効にする対象ルームです。未設定の場合はルーム制限なしで動作します。")]
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

    [Header("初回 Stop 設定 / 発動距離")]
    [Tooltip("初回補助を発動する、追跡敵の前面からプレイヤーまでの距離です。値が小さいほどギリギリで発動します。")]
    [SerializeField, Min(0f)] private float firstTriggerDistance = 2f;

    [Header("初回 Stop 設定 / 時間倍率")]
    [Tooltip("初回補助中の Time.timeScale です。0 にすると完全停止、1 に近いほど通常速度に近づきます。")]
    [SerializeField, Range(0f, 1f)] private float firstTimeScale = 0f;

    [Header("初回 Stop 設定 / 最大継続時間")]
    [Tooltip("初回補助が自動終了するまでの最大時間です。unscaledDeltaTime 基準で計測します。")]
    [SerializeField, Min(0f)] private float firstMaxDuration = 0.18f;

    [Header("2回目以降 Slow 設定 / 発動距離")]
    [Tooltip("2回目以降の補助を発動する、追跡敵の前面からプレイヤーまでの距離です。")]
    [SerializeField, Min(0f)] private float repeatTriggerDistance = 2.5f;

    [Header("2回目以降 Slow 設定 / 時間倍率")]
    [Tooltip("2回目以降の補助中の Time.timeScale です。0 に近いほど強いスローになります。")]
    [SerializeField, Range(0f, 1f)] private float repeatTimeScale = 0.25f;

    [Header("2回目以降 Slow 設定 / 最大継続時間")]
    [Tooltip("2回目以降の補助が自動終了するまでの最大時間です。unscaledDeltaTime 基準で計測します。")]
    [SerializeField, Min(0f)] private float repeatMaxDuration = 0.35f;

    [Header("共通設定 / 補助開始待機時間")]
    [Tooltip("追跡開始直後に補助が即発動しないようにする待機時間です。敵出現直後の誤発動防止に使います。")]
    [SerializeField, Min(0f)] private float assistEnableDelayAfterEnemySpawn = 0.25f;

    [Header("共通設定 / クールダウン")]
    [Tooltip("補助終了後、次の補助が発動可能になるまでの待機時間です。連続発動を防ぎます。")]
    [SerializeField, Min(0f)] private float cooldownTime = 0.5f;

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
        repeatMaxDuration = Mathf.Max(0f, repeatMaxDuration);
        assistEnableDelayAfterEnemySpawn = Mathf.Max(0f, assistEnableDelayAfterEnemySpawn);
        cooldownTime = Mathf.Max(0f, cooldownTime);
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

        if (currentAssistCount >= maxAssistCount)
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

        if (playerFacade != null && playerFacade.LastAcceptedDashInputFrame > dashBaselineFrame)
        {
            EndAssist("dash input accepted");
            return;
        }

        if (assistTimer >= activeAssistMaxDuration)
        {
            EndAssist("max duration");
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

        float targetTimeScale = isFirstAssist ? firstTimeScale : repeatTimeScale;
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
            Debug.Log($"[TimeHitStopSlow] Assist started. type={(isFirstAssist ? "Stop" : "Slow")}, count={currentAssistCount}, distance={currentFrontDistance:F3}, timeScale={targetTimeScale:F3}", this);
        }
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
            Debug.Log($"[TimeHitStopSlow] Assist ended. reason={reason}, cooldown={cooldownTimer:F3}", this);
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
            Debug.Log($"[TimeHitStopSlow] Running assist force-cleared. reason={reason}", this);
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
            Debug.Log($"[TimeHitStopSlow] Time scale restored. reason={reason}, timeScale={originalTimeScale:F3}, fixedDeltaTime={originalFixedDeltaTime:F5}", this);
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