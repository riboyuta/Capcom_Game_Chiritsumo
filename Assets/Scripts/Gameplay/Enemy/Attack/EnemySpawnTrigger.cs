using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemySpawnTrigger : MonoBehaviour
{
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private EnemySimpleHand3D targetEnemy;
    [SerializeField] private bool triggerOnlyOnce = true;

    private bool hasTriggered;

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
}