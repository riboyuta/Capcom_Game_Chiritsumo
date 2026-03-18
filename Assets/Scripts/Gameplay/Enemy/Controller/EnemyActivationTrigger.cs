using UnityEngine;

// プレイヤーが通過すると敵を有効化するトリガー
// 特定地点を通過するまで敵を停止させておく際に使用
[RequireComponent(typeof(Collider))]
public class EnemyActivationTrigger : MonoBehaviour
{
    [Header("有効化する敵のリスト")]
    [SerializeField] private PursuitEnemyController[] enemiesToActivate;

    [Header("プレイヤータグ")]
    [SerializeField] private string playerTag = "Player";

    [Header("一度だけ発動")]
    [SerializeField] private bool triggerOnce = true;

    [Header("トリガー後に自動削除")]
    [SerializeField] private bool destroyAfterTrigger = true;

    [Header("デバッグログ表示")]
    [SerializeField] private bool showDebugLog = false;

    private bool hasTriggered = false;

    // コンポーネント追加時にトリガー設定を自動化
    private void Reset()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    // プレイヤーがトリガーに侵入した時
    private void OnTriggerEnter(Collider other)
    {
        // 一度だけ発動する設定で既に発動済みなら無視
        if (triggerOnce && hasTriggered)
        {
            return;
        }

        // プレイヤータグでなければ無視
        if (!other.CompareTag(playerTag))
        {
            return;
        }

        // 敵を有効化
        ActivateEnemies();

        hasTriggered = true;

        // トリガー後に自動削除
        if (destroyAfterTrigger)
        {
            Destroy(gameObject);
        }
    }

    // 敵を有効化する処理
    private void ActivateEnemies()
    {
        if (enemiesToActivate == null || enemiesToActivate.Length == 0)
        {
            return;
        }

        foreach (var enemy in enemiesToActivate)
        {
            if (enemy != null)
            {
                enemy.Activate();
            }
        }

        LogDebug($"Activated {enemiesToActivate.Length} enemies");
    }

    // デバッグログ出力
    private void LogDebug(string message)
    {
        if (!showDebugLog)
        {
            return;
        }

        Debug.Log($"[EnemyActivationTrigger] {name} : {message}");
    }

    // ギズモ描画（Scene View でトリガー範囲を可視化）
    private void OnDrawGizmos()
    {
        Gizmos.color = hasTriggered ? Color.gray : Color.yellow;

        Collider col = GetComponent<Collider>();
        if (col is BoxCollider box)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(box.center, box.size);
        }
    }
}
