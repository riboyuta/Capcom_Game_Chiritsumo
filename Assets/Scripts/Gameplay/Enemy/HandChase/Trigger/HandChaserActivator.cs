using UnityEngine;

/// <summary>
/// 特定エリア侵入で HandChaserEnemy を有効化するトリガー。
/// トリガーごとに別のスポーン位置を持てる。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public sealed class HandChaserActivator : MonoBehaviour, IRespawnResettable
{
    [Header("起動対象の敵")]
    [Tooltip("起動対象の HandChaserEnemy です。")]
    [SerializeField] private HandChaserEnemy targetEnemy;

    [Header("スポーン位置")]
    [Tooltip("このトリガーから起動した時のスポーン位置です。未設定時はこのトリガー自身の Transform を使います。")]
    [SerializeField] private Transform spawnPoint;

    [Header("ゲーム進行")]
    [Tooltip("初回有効発動時に経過時間計測の開始通知を送る GameRoot です。")]
    [SerializeField] private GameRoot gameRoot;

    [Header("判定設定")]
    [Tooltip("プレイヤーとして判定するタグ名です。")]
    [SerializeField] private string playerTag = "Player";

    [Header("発動設定")]
    [Tooltip("一度発動した後に再発動させない場合は ON にします。")]
    [SerializeField] private bool triggerOnlyOnce = true;

    private Collider triggerCollider;
    private bool hasTriggered;

    private bool hasCapturedInitialState;
    private bool initialEnabled;
    private bool initialColliderEnabled;
    private bool initialHasTriggered;

    private void Awake()
    {
        triggerCollider = GetComponent<Collider>();
        triggerCollider.isTrigger = true;

        if (spawnPoint == null)
        {
            spawnPoint = transform;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (targetEnemy == null)
        {
            return;
        }

        if (!other.CompareTag(playerTag))
        {
            return;
        }

        if (hasTriggered && triggerOnlyOnce)
        {
            return;
        }

        hasTriggered = true;

        // 初回有効発動時に経過時間計測を開始する
        if (gameRoot != null)
        {
            gameRoot.StartElapsedTimeIfNeeded();
        }

        // 敵の追跡を開始する
        targetEnemy.BeginChase();

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

    public void CaptureInitialState()
    {
        if (hasCapturedInitialState)
        {
            return;
        }

        if (triggerCollider == null)
        {
            triggerCollider = GetComponent<Collider>();
        }

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
            hasTriggered = false;
            enabled = true;

            if (triggerCollider != null)
            {
                triggerCollider.enabled = true;
                triggerCollider.isTrigger = true;
            }
        }

        if (targetEnemy != null)
        {
            targetEnemy.ResetEncounterForRespawn();
        }
    }
}
