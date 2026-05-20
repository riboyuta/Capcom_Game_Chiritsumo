using System.Collections;
using UnityEngine;

// 各部屋のセーフゾーンからプレイヤーが出た時に ShadowChaserEnemy を有効化する。
// StageResetSystem からは IRespawnResettable 経由で未使用状態へ戻される。
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public sealed class ShadowChaserActivator : MonoBehaviour, IRespawnResettable
{
    [Header("起動対象の敵")]
    [Tooltip("起動対象の ShadowChaserEnemy です。")]
    [SerializeField] private ShadowChaserEnemy targetEnemy;

    [Header("スポーン位置")]
    [Tooltip("セーフゾーン退出後に敵を出現させる位置です。未設定時はこの GameObject の Transform を使います。")]
    [SerializeField] private Transform spawnPoint;

    [Header("プレイヤータグ判定使用")]
    [Tooltip("Player タグで判定するかです。")]
    [SerializeField] private bool usePlayerTag = true;

    [Header("プレイヤータグ名")]
    [Tooltip("Player タグ判定に使うタグ名です。")]
    [SerializeField] private string playerTag = "Player";

    [Header("セーフゾーン設定")]
    [Tooltip("プレイヤーがこのセーフゾーンから出てから、敵がスポーンするまでの遅延時間です。0なら出た瞬間にスポーンします。")]
    [SerializeField] private float spawnDelay = 0.0f;

    [Tooltip("このセーフゾーンが属する Room です。未設定時は親階層から自動検索します。")]
    [SerializeField] private Room parentRoom;

    private Collider safeZoneCollider;
    private RoomManager roomManager;
    private Coroutine spawnCoroutine;

    private bool isRoomActive;
    private bool hasStartedSpawn;

    private bool hasCapturedInitialState;
    private bool initialEnabled;
    private bool initialColliderEnabled;
    private bool initialHasStartedSpawn;

    private void Awake()
    {
        safeZoneCollider = GetComponent<Collider>();
        safeZoneCollider.isTrigger = true;

        if (spawnPoint == null)
        {
            spawnPoint = transform;
        }

        if (parentRoom == null)
        {
            parentRoom = GetComponentInParent<Room>();
        }

        roomManager = FindFirstObjectByType<RoomManager>();
    }

    private void Start()
    {
        if (roomManager != null)
        {
            roomManager.OnRoomTransitionComplete += OnRoomTransitionComplete;
        }

        RefreshRoomActiveState();
    }

    private void OnDestroy()
    {
        if (roomManager != null)
        {
            roomManager.OnRoomTransitionComplete -= OnRoomTransitionComplete;
        }
    }

    private void OnRoomTransitionComplete(Room newRoom)
    {
        if (parentRoom == null)
        {
            isRoomActive = true;
            return;
        }

        isRoomActive = newRoom == parentRoom;

        // この部屋に入った時点ではスポーンしない。
        // セーフゾーンを出た時にだけスポーン処理を開始する。
        if (isRoomActive)
        {
            StopSpawnCoroutine();
            hasStartedSpawn = false;
        }
        else
        {
            StopSpawnCoroutine();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!isRoomActive)
        {
            return;
        }

        if (roomManager != null && roomManager.IsTransitioning)
        {
            return;
        }

        if (targetEnemy == null)
        {
            return;
        }

        if (!IsPlayer(other))
        {
            return;
        }

        if (hasStartedSpawn)
        {
            return;
        }

        StartSpawnAfterSafeZoneExit();
    }

    private void StartSpawnAfterSafeZoneExit()
    {
        hasStartedSpawn = true;

        if (spawnDelay > 0f)
        {
            StopSpawnCoroutine();
            spawnCoroutine = StartCoroutine(DelayedSpawnCoroutine());
        }
        else
        {
            ActivateEnemy();
        }
    }

    private IEnumerator DelayedSpawnCoroutine()
    {
        yield return new WaitForSeconds(spawnDelay);

        ActivateEnemy();

        spawnCoroutine = null;
    }

    private void ActivateEnemy()
    {
        if (targetEnemy == null)
        {
            return;
        }

        ShadowChaserSpawnRequest request = new ShadowChaserSpawnRequest(
            spawnPoint.position,
            spawnPoint.rotation);

        targetEnemy.Activate(request);
    }

    public void CaptureInitialState()
    {
        if (hasCapturedInitialState)
        {
            return;
        }

        if (safeZoneCollider == null)
        {
            safeZoneCollider = GetComponent<Collider>();
        }

        initialEnabled = enabled;
        initialColliderEnabled = safeZoneCollider != null && safeZoneCollider.enabled;
        initialHasStartedSpawn = hasStartedSpawn;

        hasCapturedInitialState = true;
    }

    public void ResetToRespawnState()
    {
        if (safeZoneCollider == null)
        {
            safeZoneCollider = GetComponent<Collider>();
        }

        StopSpawnCoroutine();

        if (hasCapturedInitialState)
        {
            enabled = initialEnabled;
            hasStartedSpawn = initialHasStartedSpawn;

            if (safeZoneCollider != null)
            {
                safeZoneCollider.enabled = initialColliderEnabled;
                safeZoneCollider.isTrigger = true;
            }
        }
        else
        {
            enabled = true;
            hasStartedSpawn = false;

            if (safeZoneCollider != null)
            {
                safeZoneCollider.enabled = true;
                safeZoneCollider.isTrigger = true;
            }
        }

        // リスポーン時に即スポーンしない。
        // 現在部屋なら、セーフゾーン退出待ちに戻す。
        RefreshRoomActiveState();
    }

    private void RefreshRoomActiveState()
    {
        if (roomManager == null || parentRoom == null)
        {
            isRoomActive = true;
            return;
        }

        isRoomActive = roomManager.CurrentRoom == parentRoom && !roomManager.IsTransitioning;
    }

    private void StopSpawnCoroutine()
    {
        if (spawnCoroutine != null)
        {
            StopCoroutine(spawnCoroutine);
            spawnCoroutine = null;
        }
    }

    private void OnDisable()
    {
        StopSpawnCoroutine();
    }

    private bool IsPlayer(Collider other)
    {
        if (other == null)
        {
            return false;
        }

        if (usePlayerTag && other.CompareTag(playerTag))
        {
            return true;
        }

        PlayerController player = other.GetComponentInParent<PlayerController>();
        return player != null;
    }
}