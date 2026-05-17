using System.Collections;
using UnityEngine;

// 特定エリア侵入で ShadowChaserEnemy を有効化するトリガー。
// トリガーごとに別のスポーン位置を持てる。
// StageResetSystem からは IRespawnResettable 経由で未使用状態へ戻される。
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public sealed class ShadowChaserActivator : MonoBehaviour, IRespawnResettable
{
    [Header("起動対象の敵")]
    [Tooltip("起動対象の ShadowChaserEnemy です。")]
    [SerializeField] private ShadowChaserEnemy targetEnemy;

    [Header("スポーン位置")]
    [Tooltip("このトリガーから起動した時のスポーン位置です。未設定時はこのトリガー自身の Transform を使います。")]
    [SerializeField] private Transform spawnPoint;

    [Header("ワンショットモード")]
    [Tooltip("一度起動したらこのトリガーを無効化するかです。")]
    [SerializeField] private bool oneShot = true;

    [Header("プレイヤータグ判定使用")]
    [Tooltip("Player タグで判定するかです。")]
    [SerializeField] private bool usePlayerTag = true;

    [Header("プレイヤータグ名")]
    [Tooltip("Player タグ判定に使うタグ名です。")]
    [SerializeField] private string playerTag = "Player";

    [Header("スポーンモード")]
    [Tooltip("トリガー: プレイヤーがトリガーに入った時に即座にスポーン\n時間: 部屋に入ってから指定秒数後にスポーン")]
    [SerializeField] private SpawnMode spawnMode = SpawnMode.Trigger;

    [Header("時間モード設定")]
    [Tooltip("時間モード専用: スポーンするまでの遅延時間（秒）です。0で即座にスポーン。")]
    [SerializeField] private float spawnDelay = 2.0f;

    [Tooltip("時間モード専用: このActivatorが属するRoomです。未設定時は親階層から自動検索します。")]
    [SerializeField] private Room parentRoom;

    public enum SpawnMode
    {
        Trigger,    // トリガーに入ったらスポーン
        Time        // 部屋に入ってから時間経過でスポーン
    }

    private Collider triggerCollider;
    private bool hasTriggered = false;
    private Coroutine spawnCoroutine;
    private RoomManager roomManager;

    // Respawn 用に保存する初期状態
    private bool hasCapturedInitialState;
    private bool initialEnabled;
    private bool initialColliderEnabled;
    private bool initialHasTriggered;

    // 初期化処理。
    // Collider を Trigger として設定し、スポーン位置の初期化を行う。
    private void Awake()
    {
        triggerCollider = GetComponent<Collider>();
        triggerCollider.isTrigger = true;

        // スポーン位置が未設定の場合は自身の Transform を使用
        if (spawnPoint == null)
        {
            spawnPoint = transform;
        }

        // 時間モードの場合、親Roomを検索
        if (spawnMode == SpawnMode.Time && parentRoom == null)
        {
            parentRoom = GetComponentInParent<Room>();
        }

        roomManager = FindFirstObjectByType<RoomManager>();
    }

    private void Start()
    {
        // 時間モードの場合、RoomManagerのイベントを監視
        if (spawnMode == SpawnMode.Time)
        {
            if (roomManager != null)
            {
                roomManager.OnRoomTransitionComplete += OnRoomTransitionComplete;

                // 既にこのRoomが現在の部屋なら即座にスポーン開始
                if (parentRoom != null && roomManager.CurrentRoom == parentRoom)
                {
                    TriggerSpawn();
                }
            }
            else
            {
                Debug.LogWarning("ShadowChaserActivator: 時間モードですがRoomManagerが見つかりません。", this);
            }
        }
    }

    private void OnDestroy()
    {
        // イベント登録を解除
        if (roomManager != null)
        {
            roomManager.OnRoomTransitionComplete -= OnRoomTransitionComplete;
        }
    }

    private void OnRoomTransitionComplete(Room newRoom)
    {
        // 自分が属するRoomに入った時だけスポーン開始
        if (spawnMode == SpawnMode.Time && parentRoom == newRoom && !hasTriggered)
        {
            TriggerSpawn();
        }
    }

    // トリガーに何かが侵入した時の処理。
    // プレイヤーが侵入したら ShadowChaserEnemy を起動する。
    private void OnTriggerEnter(Collider other)
    {
        // 時間モードの場合はトリガー判定を無視
        if (spawnMode == SpawnMode.Time)
        {
            return;
        }

        // 部屋遷移中は誤発火を防ぐため無視
        if (roomManager != null && roomManager.IsTransitioning)
        {
            return;
        }

        // ターゲットの敵が未設定なら何もしない
        if (targetEnemy == null)
        {
            return;
        }

        // プレイヤーでなければ無視
        if (!IsPlayer(other))
        {
            return;
        }

        // oneShot モードで既に発動済みなら無視
        if (hasTriggered && oneShot)
        {
            return;
        }

        TriggerSpawn();

        // oneShot モードなら、このトリガーを無効化
        if (oneShot)
        {
            enabled = false;

            if (triggerCollider != null)
            {
                triggerCollider.enabled = false;
            }
        }
    }

    // トリガーから何かが出た時の処理。
    // 遅延スポーン中にプレイヤーがトリガーから出た場合、スポーンをキャンセル。
    private void OnTriggerExit(Collider other)
    {
        // 時間モードの場合はトリガー判定を無視
        if (spawnMode == SpawnMode.Time)
        {
            return;
        }

        // トリガーモードは即座にスポーンするため、OnTriggerExitは不要
        // (将来的にトリガーモードでも遅延が必要になった場合のための予約)
    }

    private void TriggerSpawn()
    {
        hasTriggered = true;

        // 時間モードの場合のみ遅延処理を行う
        if (spawnMode == SpawnMode.Time && spawnDelay > 0f)
        {
            // 既存のコルーチンがあればキャンセル
            if (spawnCoroutine != null)
            {
                StopCoroutine(spawnCoroutine);
            }

            spawnCoroutine = StartCoroutine(DelayedSpawnCoroutine());
        }
        else
        {
            // トリガーモード、または遅延時間が0の場合は即座にスポーン
            ActivateEnemy();
        }
    }

    private IEnumerator DelayedSpawnCoroutine()
    {
        // 指定秒数待機
        yield return new WaitForSeconds(spawnDelay);

        // 敵を起動する
        ActivateEnemy();

        spawnCoroutine = null;
    }

    private void ActivateEnemy()
    {
        if (targetEnemy == null)
        {
            return;
        }

        // スポーン要求を作成し、敵を起動
        ShadowChaserSpawnRequest request = new ShadowChaserSpawnRequest(
            spawnPoint.position,
            spawnPoint.rotation);

        targetEnemy.Activate(request);
    }

    // Respawn システム用：初期状態をキャプチャする。
    // リスポーン時にこの状態に戻すことができる。
    public void CaptureInitialState()
    {
        // 既にキャプチャ済みなら何もしない
        if (hasCapturedInitialState)
        {
            return;
        }

        if (triggerCollider == null)
        {
            triggerCollider = GetComponent<Collider>();
        }

        // 初期状態を保存
        initialEnabled = enabled;
        initialColliderEnabled = triggerCollider != null && triggerCollider.enabled;
        initialHasTriggered = hasTriggered;

        hasCapturedInitialState = true;
    }

    // Respawn システム用：キャプチャした初期状態にリセットする。
    // キャプチャしていない場合はデフォルトの状態にリセットする。
    public void ResetToRespawnState()
    {
        if (triggerCollider == null)
        {
            triggerCollider = GetComponent<Collider>();
        }

        // 実行中のコルーチンをキャンセル
        if (spawnCoroutine != null)
        {
            StopCoroutine(spawnCoroutine);
            spawnCoroutine = null;
        }

        // 初期状態がキャプチャされている場合はそれを復元
        if (hasCapturedInitialState)
        {
            enabled = initialEnabled;
            hasTriggered = initialHasTriggered;

            if (triggerCollider != null)
            {
                triggerCollider.enabled = initialColliderEnabled;
                triggerCollider.isTrigger = true;
            }
        }
        else
        {
            // キャプチャされていない場合はデフォルトの状態に
            hasTriggered = false;
            enabled = true;

            if (triggerCollider != null)
            {
                triggerCollider.enabled = true;
                triggerCollider.isTrigger = true;
            }
        }

        // 時間モードで現在の部屋にいる場合、リスポーン後に再度スポーンをトリガー
        // (ただし、部屋遷移中はその完了イベントでトリガーされるため除外)
        if (spawnMode == SpawnMode.Time && !hasTriggered)
        {
            if (roomManager != null && parentRoom != null && roomManager.CurrentRoom == parentRoom && !roomManager.IsTransitioning)
            {
                TriggerSpawn();
            }
        }
    }

    // コンポーネントが無効化された時の処理。
    // 実行中のコルーチンをキャンセルする。
    private void OnDisable()
    {
        if (spawnCoroutine != null)
        {
            StopCoroutine(spawnCoroutine);
            spawnCoroutine = null;
        }
    }

    // プレイヤーかどうかを判定する。
    // タグ判定と PlayerController コンポーネントの有無で判定する。
    private bool IsPlayer(Collider other)
    {
        if (other == null)
        {
            return false;
        }

        // タグ判定が有効な場合はタグで判定
        if (usePlayerTag && other.CompareTag(playerTag))
        {
            return true;
        }

        // タグが無い場合は PlayerController コンポーネントの有無で判定
        PlayerController player = other.GetComponentInParent<PlayerController>();
        return player != null;
    }
}