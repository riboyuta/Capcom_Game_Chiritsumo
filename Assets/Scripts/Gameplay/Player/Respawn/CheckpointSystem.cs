using UnityEngine;

public sealed class CheckpointSystem : MonoBehaviour
{
    [Header("Respawn Checkpoint")]
    [Tooltip("シーン開始時に使う初期復帰地点。")]
    [SerializeField] private Transform initialCheckpoint;

    private Transform currentCheckpoint;

    private void Awake()
    {
        currentCheckpoint = initialCheckpoint;
    }

    public Transform GetCurrentCheckpoint()
    {
        return currentCheckpoint;
    }

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