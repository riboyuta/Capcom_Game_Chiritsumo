using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public sealed class SonarChargerActivator : MonoBehaviour, IRespawnResettable
{
    [Header("起動対象の敵")]
    [Tooltip("起動対象の SonarChargerEnemy です。")]
    [SerializeField] private SonarChargerEnemy targetEnemy;

    [Header("ゲーム進行管理")]
    [Tooltip("初回有効発動時に経過時間計測の開始通知を送る GameRoot です。未使用なら未設定で構いません。")]
    [SerializeField] private GameRoot gameRoot;

    [Header("スポーン位置使用フラグ")]
    [Tooltip("起動時に敵をこの位置へ移動させるかです。")]
    [SerializeField] private bool useSpawnPointOnActivate = false;

    [Header("スポーン位置")]
    [Tooltip("起動時のスポーン位置です。未設定時はこの GameObject の Transform を使います。")]
    [SerializeField] private Transform spawnPoint;

    [Header("プレイヤー判定タグ")]
    [Tooltip("プレイヤーとして判定するタグ名です。")]
    [SerializeField] private string playerTag = "Player";

    [Header("敵起動までの遅延時間")]
    [Tooltip("プレイヤーがこのセーフゾーンから出てから、敵が起動するまでの遅延時間です。0なら出た瞬間に起動します。")]
    [SerializeField] private float spawnDelay = 0.0f;

    [Header("所属する部屋")]
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

    // Unityライフサイクル: 初期化
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

    private void OnDisable()
    {
        StopSpawnCoroutine();
    }

    // ルーム遷移イベント: アクティブ状態を更新
    private void OnRoomTransitionComplete(Room newRoom)
    {
        if (parentRoom == null)
        {
            isRoomActive = true;
            return;
        }

        isRoomActive = newRoom == parentRoom;

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

    // セーフゾーン退出: プレイヤーが出たら敵を起動
    private void OnTriggerExit(Collider other)
    {
        // 部屋が非アクティブなら何もしない
        if (!isRoomActive)
        {
            return;
        }

        // 部屋遷移中なら何もしない
        if (roomManager != null && roomManager.IsTransitioning)
        {
            return;
        }

        // 敵参照がないなら何もしない
        if (targetEnemy == null)
        {
            return;
        }

        // プレイヤー以外は無視
        if (!IsPlayer(other))
        {
            return;
        }

        // 既に起動処理を開始していれば無視
        if (hasStartedSpawn)
        {
            return;
        }

        // 敵の起動開始
        StartSpawnAfterSafeZoneExit();
    }

    // 敵の起動処理：ディレイあり/なし
    private void StartSpawnAfterSafeZoneExit()
    {
        // 起動処理を開始済みとマーク
        hasStartedSpawn = true;

        // GameRootに経過時間開始を通知
        if (gameRoot != null)
        {
            gameRoot.StartElapsedTimeIfNeeded();
        }

        // 遅延設定があればコルーチンで遅延起動、なければ即起動
        if (spawnDelay > 0.0f)
        {
            StopSpawnCoroutine();
            spawnCoroutine = StartCoroutine(DelayedSpawnCoroutine());
        }
        else
        {
            ActivateEnemy();
        }
    }

    // ディレイ付き起動コルーチン
    private IEnumerator DelayedSpawnCoroutine()
    {
        // spawnDelay秒待機
        yield return new WaitForSeconds(spawnDelay);

        // 待機後に敵を起動
        ActivateEnemy();

        // コルーチン完了をマーク
        spawnCoroutine = null;
    }

    // 敵を実際に起動する
    private void ActivateEnemy()
    {
        if (targetEnemy == null)
        {
            return;
        }

        // スポーン位置を使用する設定なら、敵を指定位置に配置してから起動
        if (useSpawnPointOnActivate && spawnPoint != null)
        {
            targetEnemy.BeginChase(spawnPoint.position, spawnPoint.rotation);
        }
        else
        {
            // スポーン位置未使用なら現在位置のまま起動
            targetEnemy.BeginChase();
        }
    }

    // IRespawnResettable: 初期状態を保存
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

    // IRespawnResettable: リスポーン時に初期状態に戻す
    public void ResetToRespawnState()
    {
        // Collider参照がない場合は再取得
        if (safeZoneCollider == null)
        {
            safeZoneCollider = GetComponent<Collider>();
        }

        // 遅延起動コルーチンを中断
        StopSpawnCoroutine();

        // 初期状態が保存されていればそれを復元、そうでなければデフォルト値
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
            // デフォルト: 有効で未起動状態にリセット
            enabled = true;
            hasStartedSpawn = false;

            if (safeZoneCollider != null)
            {
                safeZoneCollider.enabled = true;
                safeZoneCollider.isTrigger = true;
            }
        }

        // 敵本体をリスポーンリセット
        if (targetEnemy != null)
        {
            targetEnemy.ResetEncounterForRespawn();
        }

        // Roomアクティブ状態を再取得
        RefreshRoomActiveState();
    }

    // ヘルパー: ルームのアクティブ状態を更新
    private void RefreshRoomActiveState()
    {
        // RoomManagerまたはRoomがない場合は常にアクティブ扱い
        if (roomManager == null || parentRoom == null)
        {
            isRoomActive = true;
            return;
        }

        // 現在のアクティブRoomが親と同じで、かつ遷移中でなければtrue
        isRoomActive = roomManager.CurrentRoom == parentRoom && !roomManager.IsTransitioning;
    }

    // ヘルパー: 実行中のコルーチンを停止
    private void StopSpawnCoroutine()
    {
        if (spawnCoroutine != null)
        {
            StopCoroutine(spawnCoroutine);
            spawnCoroutine = null;
        }
    }

    // ヘルパー: 当たったColliderがプレイヤーかを判定
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
}