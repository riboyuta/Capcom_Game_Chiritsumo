using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemySpawnTrigger : MonoBehaviour, IRespawnResettable
{
    [Header("判定設定")]
    [Tooltip("プレイヤーとして判定するタグ名です。")]
    [SerializeField] private string playerTag = "Player";

    [Header("敵設定")]
    [Tooltip("この Trigger で追跡開始させる敵です。")]
    [SerializeField] private EnemySimpleHand3D targetEnemy;

    [Header("ゲーム進行")]
    [Tooltip("初回有効発動時に経過時間計測の開始通知を送る GameRoot です。")]
    [SerializeField] private GameRoot gameRoot;

    [Header("発動設定")]
    [Tooltip("一度発動した後に再発動させない場合は ON にします。")]
    [SerializeField] private bool triggerOnlyOnce = true;

    private bool hasTriggered;
    private bool initialHasTriggered;
    private bool hasCapturedInitialState;

    private void Reset()
    {
        // Trigger 用 Collider を自動で有効化する。
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // 一回だけ発動する設定で、すでに発動済みなら何もしない。
        if (hasTriggered && triggerOnlyOnce)
        {
            return;
        }

        // 指定タグ以外は無視する。
        if (!other.CompareTag(playerTag))
        {
            return;
        }

        // 敵参照が未設定なら処理できない。
        if (targetEnemy == null)
        {
            Debug.LogWarning("[EnemySpawnTrigger] targetEnemy が未設定です。", this);
            return;
        }

        // 初回有効発動時に経過時間計測を開始する。
        if (gameRoot != null)
        {
            gameRoot.StartElapsedTimeIfNeeded();
        }
        else
        {
            Debug.LogWarning("[EnemySpawnTrigger] gameRoot が未設定です。経過時間の開始通知を送れません。", this);
        }

        // 敵の追跡を開始する。
        targetEnemy.BeginChase();
        hasTriggered = true;
    }

    public void CaptureInitialState()
    {
        if (hasCapturedInitialState)
        {
            return;
        }

        initialHasTriggered = hasTriggered;
        hasCapturedInitialState = true;
    }

    public void ResetToRespawnState()
    {
        if (!hasCapturedInitialState)
        {
            CaptureInitialState();
        }

        if (targetEnemy != null)
        {
            targetEnemy.ResetEncounterForRespawn();
        }

        hasTriggered = initialHasTriggered;
    }
}