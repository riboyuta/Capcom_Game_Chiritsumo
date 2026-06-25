using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public abstract class EnemySafeZoneActivatorBase : MonoBehaviour, IRespawnResettable
{
    [Header("判定設定")]
    [Tooltip("プレイヤーとして判定するタグ名です。")]
    [SerializeField] private string playerTag = "Player";

    [Header("セーフゾーン設定")]
    [Tooltip("プレイヤーがこのセーフゾーンから出てから、敵がスポーンするまでの遅延時間です。0なら出た瞬間にスポーンします。")]
    [SerializeField] private float spawnDelay = 0.0f;

    [Tooltip("このセーフゾーンが属する Room です。未設定時は親階層から自動検索します。")]
    [SerializeField] private Room parentRoom;

    private Collider safeZoneCollider;
    private RoomManager roomManager;
    private Coroutine spawnCoroutine;
    private Coroutine confirmExitCoroutine;
    private Collider lastPlayerCollider;

    private bool isRoomActive;
    private bool hasStartedSpawn;
    private bool hasConfirmedPlayerInsideSafeZone;

    private bool hasCapturedInitialState;
    private bool initialEnabled;
    private bool initialColliderEnabled;
    private bool initialHasStartedSpawn;

    protected Collider SafeZoneCollider => safeZoneCollider;
    protected RoomManager RoomManager => roomManager;
    protected bool IsRoomActive => isRoomActive;
    protected bool HasStartedSpawn => hasStartedSpawn;

    protected virtual void Awake()
    {
        EnsureSafeZoneCollider();

        if (parentRoom == null)
        {
            parentRoom = GetComponentInParent<Room>();
        }

        roomManager = FindFirstObjectByType<RoomManager>();
    }

    protected virtual void Start()
    {
        if (roomManager != null)
        {
            roomManager.OnRoomTransitionComplete += OnRoomTransitionComplete;
        }

        RefreshRoomActiveState();

        if (isRoomActive)
        {
            RefreshPlayerInsideSafeZoneState();
        }
    }

    protected virtual void OnDestroy()
    {
        if (roomManager != null)
        {
            roomManager.OnRoomTransitionComplete -= OnRoomTransitionComplete;
        }
    }

    protected virtual void OnDisable()
    {
        StopSpawnCoroutine();
        OnSafeZoneDisabled();
    }

    private void OnRoomTransitionComplete(Room newRoom)
    {
        if (parentRoom == null)
        {
            isRoomActive = true;
        }
        else
        {
            isRoomActive = newRoom == parentRoom;
        }

        StopSpawnCoroutine();

        if (isRoomActive)
        {
            hasStartedSpawn = false;
            hasConfirmedPlayerInsideSafeZone = false;
            RefreshPlayerInsideSafeZoneState();
            OnSafeZoneRoomActivated();
        }
        else
        {
            hasConfirmedPlayerInsideSafeZone = false;
            OnSafeZoneRoomDeactivated();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        TryConfirmPlayerInsideSafeZone(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TryConfirmPlayerInsideSafeZone(other);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!CanHandlePlayerTrigger(other))
        {
            return;
        }

        if (!HasValidTarget())
        {
            return;
        }

        if (!hasConfirmedPlayerInsideSafeZone)
        {
            return;
        }

        lastPlayerCollider = other;

        if (confirmExitCoroutine != null)
        {
            StopCoroutine(confirmExitCoroutine);
        }

        confirmExitCoroutine = StartCoroutine(ConfirmExitCoroutine(other));
    }

    private void TryConfirmPlayerInsideSafeZone(Collider other)
    {
        if (!CanHandlePlayerTrigger(other))
        {
            return;
        }

        hasConfirmedPlayerInsideSafeZone = true;
        OnPlayerInsideSafeZone();
    }

    private bool CanHandlePlayerTrigger(Collider other)
    {
        return isRoomActive
            && !(roomManager != null && roomManager.IsTransitioning)
            && !hasStartedSpawn
            && IsPlayer(other);
    }

    private void StartSpawnAfterSafeZoneExit()
    {
        hasStartedSpawn = true;

        OnSpawnSequenceStarted();

        if (spawnDelay > 0f)
        {
            StopSpawnCoroutine();
            spawnCoroutine = StartCoroutine(DelayedSpawnCoroutine());
        }
        else
        {
            ActivateTargetEnemy();
        }
    }

    private IEnumerator ConfirmExitCoroutine(Collider playerCollider)
    {
        yield return new WaitForFixedUpdate();

        confirmExitCoroutine = null;

        if (playerCollider == null)
        {
            yield break;
        }

        if (!isRoomActive)
        {
            yield break;
        }

        if (roomManager != null && roomManager.IsTransitioning)
        {
            yield break;
        }

        if (hasStartedSpawn)
        {
            yield break;
        }

        if (!hasConfirmedPlayerInsideSafeZone)
        {
            yield break;
        }

        if (!HasValidTarget())
        {
            yield break;
        }

        if (IsPlayerStillInsideSafeZone(playerCollider))
        {
            yield break;
        }

        OnPlayerExitSafeZoneConfirmed();

        StartSpawnAfterSafeZoneExit();
    }

    private bool IsPlayerStillInsideSafeZone(Collider playerCollider)
    {
        if (safeZoneCollider == null || !safeZoneCollider.enabled)
        {
            return false;
        }

        if (playerCollider == null || !playerCollider.enabled)
        {
            return false;
        }

        PlayerController player = playerCollider.GetComponentInParent<PlayerController>();

        if (player == null)
        {
            return safeZoneCollider.bounds.Intersects(playerCollider.bounds);
        }

        Collider[] playerColliders = player.GetComponentsInChildren<Collider>();

        for (int i = 0; i < playerColliders.Length; i++)
        {
            Collider currentCollider = playerColliders[i];

            if (currentCollider == null || !currentCollider.enabled)
            {
                continue;
            }

            if (safeZoneCollider.bounds.Intersects(currentCollider.bounds))
            {
                return true;
            }
        }

        return false;
    }

    private IEnumerator DelayedSpawnCoroutine()
    {
        yield return new WaitForSeconds(spawnDelay);

        ActivateTargetEnemy();

        spawnCoroutine = null;
    }

    public void CaptureInitialState()
    {
        if (hasCapturedInitialState)
        {
            return;
        }

        EnsureSafeZoneCollider();

        initialEnabled = enabled;
        initialColliderEnabled = safeZoneCollider != null && safeZoneCollider.enabled;
        initialHasStartedSpawn = hasStartedSpawn;

        hasCapturedInitialState = true;
    }

    public void ResetToRespawnState()
    {
        EnsureSafeZoneCollider();

        StopSpawnCoroutine();
        OnBeforeResetToRespawnState();

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

        hasConfirmedPlayerInsideSafeZone = false;

        ResetTargetEnemyForRespawn();

        RefreshRoomActiveState();

        if (isRoomActive)
        {
            RefreshPlayerInsideSafeZoneState();
        }
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

    private void RefreshPlayerInsideSafeZoneState()
    {
        if (safeZoneCollider == null || !safeZoneCollider.enabled)
        {
            return;
        }

        PlayerController player = FindFirstObjectByType<PlayerController>();

        if (player == null)
        {
            return;
        }

        Collider[] playerColliders = player.GetComponentsInChildren<Collider>();

        for (int i = 0; i < playerColliders.Length; i++)
        {
            Collider playerCollider = playerColliders[i];

            if (playerCollider == null || !playerCollider.enabled)
            {
                continue;
            }

            if (safeZoneCollider.bounds.Intersects(playerCollider.bounds))
            {
                hasConfirmedPlayerInsideSafeZone = true;
                OnPlayerInsideSafeZone();
                return;
            }
        }
    }

    private void StopSpawnCoroutine()
    {
        if (spawnCoroutine != null)
        {
            StopCoroutine(spawnCoroutine);
            spawnCoroutine = null;

            OnSpawnDelayCanceled();
        }

        if (confirmExitCoroutine != null)
        {
            StopCoroutine(confirmExitCoroutine);
            confirmExitCoroutine = null;
        }

        lastPlayerCollider = null;
    }

    private void EnsureSafeZoneCollider()
    {
        if (safeZoneCollider == null)
        {
            safeZoneCollider = GetComponent<Collider>();
        }

        if (safeZoneCollider != null)
        {
            safeZoneCollider.isTrigger = true;
        }
    }

    private bool IsPlayer(Collider other)
    {
        if (other == null)
        {
            return false;
        }

        if (other.CompareTag(playerTag))
        {
            return true;
        }

        PlayerController player = other.GetComponentInParent<PlayerController>();
        return player != null;
    }

    protected virtual void OnPlayerInsideSafeZone()
    {
    }

    protected virtual void OnPlayerExitSafeZoneConfirmed()
    {
    }

    protected virtual void OnSpawnSequenceStarted()
    {
    }

    protected virtual void OnSpawnDelayCanceled()
    {
    }

    protected virtual void OnSafeZoneRoomActivated()
    {
    }

    protected virtual void OnSafeZoneRoomDeactivated()
    {
    }

    protected virtual void OnSafeZoneDisabled()
    {
    }

    protected virtual void OnBeforeResetToRespawnState()
    {
    }

    protected virtual void ResetTargetEnemyForRespawn()
    {
    }

    protected abstract bool HasValidTarget();

    protected abstract void ActivateTargetEnemy();
}