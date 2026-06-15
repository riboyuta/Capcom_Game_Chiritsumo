using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemyProximityTimeAssist : MonoBehaviour, IRespawnResettable
{
    [Header("参照 / ルーム管理")]
    [Tooltip("現在の部屋と部屋遷移状態を確認するための RoomManager です。未設定の場合は Awake で自動取得します。")]
    [SerializeField] private RoomManager roomManager;

    [Header("参照 / プレイヤー")]
    [Tooltip("プレイヤーの位置、ダッシュ入力受付フレーム、ダッシュ回復処理を参照するための PlayerFacade です。未設定の場合は Awake で自動取得します。")]
    [SerializeField] private PlayerFacade playerFacade;

    [Header("参照 / 追跡敵の移動")]
    [Tooltip("追跡敵が有効かどうか、移動方向、カスタム移動軸を取得するためのコンポーネントです。")]
    [SerializeField] private HandChaserMovement handChaserMovement;

    [Header("参照 / 追跡敵の当たり判定")]
    [Tooltip("追跡敵の前面位置を計算するために使用する BoxCollider です。プレイヤーとの前方距離判定に使います。")]
    [SerializeField] private BoxCollider handChaserBoxCollider;


    [Header("対象ルーム / 有効ルーム一覧")]
    [Tooltip("この敵接近時の時間補助を有効にする対象ルーム一覧です。1つ以上設定されている場合は、この一覧を優先して判定します。")]
    [SerializeField] private Room[] targetRooms;

    [Header("対象ルーム / 移行用")]
    [Tooltip("旧設定用の対象ルームです。targetRooms に有効な Room がない場合だけ参照します。既存 Scene の参照保護のため残しています。")]
    [SerializeField] private Room targetRoom;


    [Header("発動条件 / 追跡開始後の待機時間")]
    [Tooltip("追跡開始直後に補助が即発動しないようにする待機時間です。敵出現直後の誤発動防止に使います。")]
    [SerializeField, Min(0f)] private float assistEnableDelayAfterEnemySpawn = 0.25f;

    [Header("発動条件 / クールダウン")]
    [Tooltip("補助終了後、次の補助が発動可能になるまでの待機時間です。連続発動を防ぎます。")]
    [SerializeField, Min(0f)] private float cooldownTime = 0.5f;

    [Header("発動条件 / ダッシュ可否")]
    [Tooltip("有効にすると、補助開始前にプレイヤーが今ダッシュ可能かを確認し、ダッシュ可能な場合だけ補助を開始します。")]
    [SerializeField] private bool requireDashAvailableToStart;

    [Header("発動条件 / 最大補助回数を使う")]
    [Tooltip("有効にすると、1回の追跡中に発動できる補助回数を maxAssistCount で制限します。無効時は回数制限なしで発動します。")]
    [SerializeField] private bool useAssistCountLimit = true;

    [Header("発動条件 / 最大補助回数")]
    [Tooltip("1回の追跡中に発動できる補助の最大回数です。0 にすると補助は発動しません。")]
    [SerializeField, Min(0)] private int maxAssistCount = 3;


    [Header("補助開始時 / ダッシュ回復")]
    [Tooltip("有効にすると、補助開始時にプレイヤーのダッシュを1回だけ回復します。チュートリアル用の救済として使います。")]
    [SerializeField] private bool refillDashOnAssistStart = true;


    [Header("1回目補助 / 発動距離")]
    [Tooltip("初回補助を発動する、追跡敵の前面からプレイヤーまでの距離です。値が小さいほどギリギリで発動します。")]
    [SerializeField, Min(0f)] private float firstTriggerDistance = 2f;

    [Header("1回目補助 / 時間倍率")]
    [Tooltip("初回補助中の Time.timeScale です。0 にすると完全停止、1 に近いほど通常速度に近づきます。")]
    [SerializeField, Range(0f, 1f)] private float firstTimeScale = 0f;

    [Header("2回目補助 / 発動距離")]
    [Tooltip("2回目の補助を発動する、追跡敵の前面からプレイヤーまでの距離です。")]
    [SerializeField, Min(0f)] private float repeatTriggerDistance = 2.5f;

    [Header("2回目補助 / 時間倍率")]
    [Tooltip("2回目の補助中の Time.timeScale です。0 に近いほど強い時間補助になります。")]
    [SerializeField, Range(0f, 1f)] private float repeatTimeScale = 0.25f;

    [Header("3回目以降補助 / 発動距離")]
    [Tooltip("3回目以降の補助を発動する距離です。既存 Scene 維持のため、初期値は4mです。")]
    [SerializeField, Min(0f)] private float thirdAndLaterTriggerDistance = 4f;

    [Header("3回目以降補助 / 時間倍率")]
    [Tooltip("3回目以降の補助中の Time.timeScale です。回数制限に達するまで、この値を使い続けます。")]
    [SerializeField, Range(0f, 1f)] private float thirdAndLaterTimeScale = 0.15f;


    [Header("解除条件 / ダッシュ入力で解除")]
    [Tooltip("有効にすると、補助開始後にプレイヤーのダッシュ入力が受理された時点で補助を解除します。")]
    [SerializeField] private bool releaseOnDashInput = true;

    [Header("解除条件 / 最大継続時間を使う")]
    [Tooltip("有効にすると、最大継続時間に達した時点で補助を解除します。時間計測は Time.timeScale の影響を受けません。")]
    [SerializeField] private bool useMaxDuration = true;

    [Header("解除条件 / 最大継続時間")]
    [Tooltip("補助が自動終了するまでの共通最大時間です。1回目、2回目、3回目以降のすべてで使います。unscaledDeltaTime 基準で計測します。")]
    [SerializeField, Min(0f)] private float maxDuration = 5f;

    [Header("解除条件 / 距離で解除")]
    [Tooltip("有効にすると、補助中に追跡敵の前面からプレイヤーまでの距離が解除距離以上になった時点で補助を解除します。")]
    [SerializeField] private bool releaseOnDistance;

    [Header("解除条件 / 距離解除しきい値")]
    [Tooltip("距離解除を行う前面距離です。発動距離より大きい値にすると、補助開始直後の即解除を避けやすくなります。")]
    [SerializeField, Min(0f)] private float releaseDistance = 5f;


    [Header("表示 / ギズモ表示")]
    [Tooltip("Scene ビューで追跡敵の範囲、前面位置、発動距離の目安を表示するかどうかです。")]
    [SerializeField] private bool showGizmos = true;

    [Header("表示 / デバッグログ")]
    [Tooltip("補助開始、補助終了、時間倍率復元などのログを Console に出力するかどうかです。")]
    [SerializeField] private bool showDebugLog;


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
    private readonly List<EnemyProximityAssistTarget> assistTargetBuffer = new List<EnemyProximityAssistTarget>();

    private const float MinFixedDeltaTimeScale = 0.0001f;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnValidate()
    {
        firstTriggerDistance = Mathf.Max(0f, firstTriggerDistance);
        firstTimeScale = Mathf.Max(0f, firstTimeScale);
        repeatTriggerDistance = Mathf.Max(0f, repeatTriggerDistance);
        thirdAndLaterTriggerDistance = Mathf.Max(0f, thirdAndLaterTriggerDistance);
        repeatTimeScale = Mathf.Max(0f, repeatTimeScale);
        thirdAndLaterTimeScale = Mathf.Max(0f, thirdAndLaterTimeScale);
        maxDuration = Mathf.Max(0f, maxDuration);
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

        bool hasActiveAssistSource = HasActiveAssistSource();
        if (!hasActiveAssistSource)
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

        UpdateChaseDetection(hasActiveAssistSource);

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

        float triggerDistance = GetAssistTriggerDistance(currentAssistCount);
        if (currentFrontDistance >= 0f && currentFrontDistance <= triggerDistance)
        {
            if (CanStartAssistByDashAvailability())
            {
                StartAssist(currentAssistCount == 0);
            }
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

        return roomManager != null && IsCurrentRoomTarget(roomManager.CurrentRoom);
    }

    private bool IsCurrentRoomTarget(Room currentRoom)
    {
        if (currentRoom == null)
        {
            return false;
        }

        bool hasTargetRooms = false;
        if (targetRooms != null)
        {
            for (int i = 0; i < targetRooms.Length; i++)
            {
                Room candidate = targetRooms[i];
                if (candidate == null)
                {
                    continue;
                }

                hasTargetRooms = true;
                if (candidate == currentRoom)
                {
                    return true;
                }
            }
        }

        if (hasTargetRooms)
        {
            return false;
        }

        return targetRoom != null && currentRoom == targetRoom;
    }

    private void UpdateChaseDetection(bool hasActiveAssistSource)
    {
        if (!hasActiveAssistSource)
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
        if (refillDashOnAssistStart)
        {
            playerFacade.TryRefillDash(DashRefillReason.TutorialAssist);
        }

        float targetTimeScale = GetAssistTimeScale(currentAssistCount);
        activeAssistMaxDuration = maxDuration;
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

    private bool CanStartAssistByDashAvailability()
    {
        if (!requireDashAvailableToStart)
        {
            return true;
        }

        // ダッシュ可能な場面だけ時間補助を開始し、回復やTimeScale変更へ入る前に止める。
        return playerFacade != null && playerFacade.CanUseDashNow;
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

    private float GetAssistTriggerDistance(int assistCountBeforeStart)
    {
        // 発動前の回数を基準に、既存の2回目以降距離を保ちながら3回目以降の個別設定へ切り替える。
        if (assistCountBeforeStart <= 0)
        {
            return firstTriggerDistance;
        }

        if (assistCountBeforeStart == 1)
        {
            return repeatTriggerDistance;
        }

        return thirdAndLaterTriggerDistance;

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
        if (playerFacade == null)
        {
            return false;
        }

        if (TryUpdateMarkerFrontDistance(out float nearestFrontDistance))
        {
            currentFrontDistance = nearestFrontDistance;
            return true;
        }

        if (!TryUpdateLegacyFrontDistance(out nearestFrontDistance))
        {
            return false;
        }

        currentFrontDistance = nearestFrontDistance;
        return true;
    }

    private bool HasActiveAssistSource()
    {
        if (TryGetCurrentRoom(out Room currentRoom))
        {
            EnemyProximityAssistTarget.CollectValidTargets(currentRoom, assistTargetBuffer);
            if (assistTargetBuffer.Count > 0)
            {
                return true;
            }
        }
        else
        {
            assistTargetBuffer.Clear();
        }

        return handChaserMovement != null && handChaserMovement.IsActive;
    }

    private bool TryUpdateMarkerFrontDistance(out float nearestFrontDistance)
    {
        nearestFrontDistance = 0f;

        if (!TryGetCurrentRoom(out Room currentRoom))
        {
            assistTargetBuffer.Clear();
            return false;
        }

        EnemyProximityAssistTarget.CollectValidTargets(currentRoom, assistTargetBuffer);

        bool hasDistance = false;
        for (int i = 0; i < assistTargetBuffer.Count; i++)
        {
            EnemyProximityAssistTarget target = assistTargetBuffer[i];
            if (target == null)
            {
                continue;
            }

            if (!TryCalculateFrontDistance(target.Movement, target.Collider, out float frontDistance))
            {
                continue;
            }

            if (frontDistance < 0f)
            {
                continue;
            }

            if (!hasDistance || frontDistance < nearestFrontDistance)
            {
                nearestFrontDistance = frontDistance;
                hasDistance = true;
            }
        }

        return hasDistance;
    }

    private bool TryUpdateLegacyFrontDistance(out float frontDistance)
    {
        frontDistance = 0f;

        if (!TryCalculateFrontDistance(handChaserMovement, handChaserBoxCollider, out frontDistance))
        {
            return false;
        }

        return true;
    }

    private bool TryGetCurrentRoom(out Room currentRoom)
    {
        currentRoom = roomManager != null ? roomManager.CurrentRoom : null;
        return currentRoom != null;
    }

    private bool TryCalculateFrontDistance(HandChaserMovement movement, BoxCollider boxCollider, out float frontDistance)
    {
        frontDistance = 0f;

        if (movement == null || boxCollider == null || playerFacade == null)
        {
            return false;
        }

        if (!TryGetMoveAxis(movement, out Vector3 axis))
        {
            return false;
        }

        Bounds bounds = boxCollider.bounds;
        Vector3 absAxis = new Vector3(Mathf.Abs(axis.x), Mathf.Abs(axis.y), Mathf.Abs(axis.z));
        float projectedHalfExtent =
            bounds.extents.x * absAxis.x +
            bounds.extents.y * absAxis.y +
            bounds.extents.z * absAxis.z;

        float frontPlane = Vector3.Dot(bounds.center, axis) + projectedHalfExtent;
        float playerPlane = Vector3.Dot(playerFacade.transform.position, axis);
        frontDistance = playerPlane - frontPlane;
        return true;
    }

    private bool TryGetMoveAxis(out Vector3 axis)
    {
        return TryGetMoveAxis(handChaserMovement, out axis);
    }

    private bool TryGetMoveAxis(HandChaserMovement movement, out Vector3 axis)
    {
        axis = Vector3.right;

        if (movement == null)
        {
            return false;
        }

        switch (movement.Direction)
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
                axis = movement.CustomMoveAxis;
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
