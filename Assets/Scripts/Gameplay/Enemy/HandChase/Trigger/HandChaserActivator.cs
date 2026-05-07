using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public sealed class HandChaserActivator : MonoBehaviour, IRespawnResettable
{
    [Header("起動対象の敵")]
    [Tooltip("起動対象の HandChaserEnemy です。")]
    [SerializeField] private HandChaserEnemy targetEnemy;

    [Header("ゲーム進行")]
    [Tooltip("初回有効発動時に経過時間計測の開始通知を送る GameRoot です。")]
    [SerializeField] private GameRoot gameRoot;

    [Header("判定設定")]
    [Tooltip("プレイヤーとして判定するタグ名です。")]
    [SerializeField] private string playerTag = "Player";

    [Header("発動設定")]
    [Tooltip("一度発動した後に再発動させない場合は ON にします。")]
    [SerializeField] private bool triggerOnlyOnce = true;

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
    private bool hasTriggered;
    private Coroutine spawnCoroutine;
    private RoomManager roomManager;

    private bool hasCapturedInitialState;
    private bool initialEnabled;
    private bool initialColliderEnabled;
    private bool initialHasTriggered;

    private void Awake()
    {
        // Colliderを取得してトリガーとして設定
        triggerCollider = GetComponent<Collider>();
        triggerCollider.isTrigger = true;

        // 時間モードの場合、親Roomを検索
        if (spawnMode == SpawnMode.Time && parentRoom == null)
        {
            parentRoom = GetComponentInParent<Room>();
        }
    }

    private void Start()
    {
        // 時間モードの場合、RoomManagerのイベントを監視
        if (spawnMode == SpawnMode.Time)
        {
            roomManager = FindFirstObjectByType<RoomManager>();
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
                Debug.LogWarning("HandChaserActivator: 時間モードですがRoomManagerが見つかりません。", this);
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

    private void OnTriggerEnter(Collider other)
    {
        // 時間モードの場合はトリガー判定を無視
        if (spawnMode == SpawnMode.Time)
        {
            return;
        }

        // 対象の敵が未設定なら何もしない
        if (targetEnemy == null)
        {
            return;
        }

        // プレイヤー以外は反応しない
        if (!other.CompareTag(playerTag))
        {
            return;
        }

        // 一度だけ発動する設定の場合、既に発動済みならスキップ
        if (hasTriggered && triggerOnlyOnce)
        {
            return;
        }

        TriggerSpawn();

        // oneShot モードなら、このトリガーを無効化
        if (triggerOnlyOnce)
        {
            enabled = false;

            if (triggerCollider != null)
            {
                triggerCollider.enabled = false;
            }
        }
    }

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

        // 初回有効発動時に経過時間計測を開始する
        if (gameRoot != null)
        {
            gameRoot.StartElapsedTimeIfNeeded();
        }

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
            SpawnEnemy();
        }
    }

    private void SpawnEnemy()
    {
        if (targetEnemy != null)
        {
            targetEnemy.BeginChase();
        }
    }

    private IEnumerator DelayedSpawnCoroutine()
    {
        // 指定秒数待機
        yield return new WaitForSeconds(spawnDelay);

        // 敵の追跡を開始する
        SpawnEnemy();

        spawnCoroutine = null;
    }

    public void CaptureInitialState()
    {
        // 既にキャプチャ済みなら何もしない
        if (hasCapturedInitialState)
        {
            return;
        }

        // Colliderが未取得なら取得
        if (triggerCollider == null)
        {
            triggerCollider = GetComponent<Collider>();
        }

        // 現在の状態を保存
        initialEnabled = enabled;
        initialColliderEnabled = triggerCollider != null && triggerCollider.enabled;
        initialHasTriggered = hasTriggered;

        hasCapturedInitialState = true;
    }

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

        // 初期状態が保存されていればそれに従う
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
            // 初期状態がなければデフォルトにリセット
            hasTriggered = false;
            enabled = true;

            if (triggerCollider != null)
            {
                triggerCollider.enabled = true;
                triggerCollider.isTrigger = true;
            }
        }

        // 敵もリセット
        if (targetEnemy != null)
        {
            targetEnemy.ResetEncounterForRespawn();
        }

        // 時間モードで現在の部屋にいる場合、リスポーン後に再度スポーンをトリガー
        if (spawnMode == SpawnMode.Time && !hasTriggered)
        {
            if (roomManager != null && parentRoom != null && roomManager.CurrentRoom == parentRoom)
            {
                TriggerSpawn();
            }
        }
    }

    private void OnDisable()
    {
        // コンポーネントが無効化された時はコルーチンをキャンセル
        if (spawnCoroutine != null)
        {
            StopCoroutine(spawnCoroutine);
            spawnCoroutine = null;
        }
    }
}
