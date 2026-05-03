using UnityEngine;

public sealed class CheckpointSystem : MonoBehaviour
{
    [Header("初期復帰地点")]
    [Tooltip("シーン開始時に使う初期の復帰地点。")]
    [SerializeField] private Transform initialCheckpoint;

    private Transform currentCheckpoint;

    private void Awake()
    {
        currentCheckpoint = initialCheckpoint;
    }

    // 現在の復帰地点を返す。
    public Transform GetCurrentCheckpoint()
    {
        return currentCheckpoint;
    }

    // 現在の復帰地点を更新する。
    public void SetCheckpoint(Transform checkpoint)
    {
        if (checkpoint == null)
        {
            Debug.LogWarning("[CheckpointSystem] SetCheckpoint ignored: checkpoint is null.", this);
            return;
        }

        currentCheckpoint = checkpoint;
    }
}