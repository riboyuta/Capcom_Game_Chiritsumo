using UnityEngine;

// 特定エリア侵入で ShadowChaserEnemy を有効化するトリガー。
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public sealed class ShadowChaserActivator : MonoBehaviour
{
    [Header("参照")]
    [Tooltip("起動対象の ShadowChaserEnemy です。")]
    [SerializeField] private ShadowChaserEnemy targetEnemy;

    [Header("挙動")]
    [Tooltip("一度起動したらこのトリガーを無効化するかです。")]
    [SerializeField] private bool oneShot = true;

    [Tooltip("Player タグで判定するかです。")]
    [SerializeField] private bool usePlayerTag = true;

    [Tooltip("Player タグ判定に使うタグ名です。")]
    [SerializeField] private string playerTag = "Player";

    private Collider triggerCollider;

    private void Awake()
    {
        triggerCollider = GetComponent<Collider>();
        triggerCollider.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (targetEnemy == null)
        {
            return;
        }

        if (!IsPlayer(other))
        {
            return;
        }

        targetEnemy.Activate();

        if (oneShot)
        {
            enabled = false;

            if (triggerCollider != null)
            {
                triggerCollider.enabled = false;
            }
        }
    }

    private bool IsPlayer(Collider other)
    {
        if (other == null)
        {
            return false;
        }

        if (usePlayerTag && other.CompareTag(playerTag))
        {
            return true;
        }

        PlayerController player = other.GetComponentInParent<PlayerController>();
        return player != null;
    }
}