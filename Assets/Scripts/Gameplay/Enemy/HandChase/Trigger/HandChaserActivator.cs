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

    private Collider triggerCollider;
    private bool hasTriggered;

    private bool hasCapturedInitialState;
    private bool initialEnabled;
    private bool initialColliderEnabled;
    private bool initialHasTriggered;

    private void Awake()
    {
        // Colliderを取得してトリガーとして設定
        triggerCollider = GetComponent<Collider>();
        triggerCollider.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
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
    }
}
