using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemySpawnTrigger : MonoBehaviour, IRespawnResettable
{
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private EnemySimpleHand3D targetEnemy;
    [SerializeField] private bool triggerOnlyOnce = true;

    private bool hasTriggered;
    private bool initialHasTriggered;
    private bool hasCapturedInitialState;
    private void Reset()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasTriggered && triggerOnlyOnce)
        {
            return;
        }

        if (!other.CompareTag(playerTag))
        {
            return;
        }

        if (targetEnemy == null)
        {
            Debug.LogWarning("[EnemySpawnTrigger3D] targetEnemy が未設定です。", this);
            return;
        }

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

        hasTriggered = initialHasTriggered;
    }
}
