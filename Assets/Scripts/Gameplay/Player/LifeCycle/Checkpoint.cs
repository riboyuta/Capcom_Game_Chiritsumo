using UnityEngine;

public sealed class Checkpoint : MonoBehaviour
{
    [Header("復帰地点")]
    [Tooltip("このチェックポイントが有効になったときに使う復帰座標。未設定なら親Transformを使う。")]
    [SerializeField] private Transform respawnPoint;

    [Header("システム参照")]
    [Tooltip("現在の復帰地点を管理するチェックポイントシステム。")]
    [SerializeField] private CheckpointSystem checkpointSystem;

    private void Awake()
    {
        // 復帰地点未設定時は親を使う。
        if (respawnPoint == null && transform.parent != null)
        {
            respawnPoint = transform.parent;
        }
    }

    // プレイヤーがトリガーに入ったら復帰地点を更新する。
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") == false)
        {
            return;
        }

        if (respawnPoint == null)
        {
            Debug.LogWarning("[Checkpoint] respawnPoint is null.", this);
            return;
        }

        if (checkpointSystem == null)
        {
            Debug.LogWarning("[Checkpoint] checkpointSystem is null.", this);
            return;
        }

        checkpointSystem.SetCheckpoint(respawnPoint);
    }
}