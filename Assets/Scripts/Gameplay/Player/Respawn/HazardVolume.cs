using UnityEngine;

// 奈落や即死エリアの Trigger 判定から、プレイヤー死亡入口へ接続する薄い中継コンポーネント。
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public sealed class HazardVolume : MonoBehaviour
{
    [Header("Filter")]
    [Tooltip("ハザードと反応するタグ（通常は Player）")]
    [SerializeField] private string targetTag = "Player";

    private void Awake()
    {
        Collider colliderComponent = GetComponent<Collider>();
        if (colliderComponent != null && !colliderComponent.isTrigger)
        {
            Debug.LogWarning("[HazardVolume] Collider.isTrigger is false. Enable isTrigger for hazard detection.", this);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(targetTag))
        {
            return;
        }

        Debug.Log($"[HazardVolume] Hazard entered: {other.name}", this);

        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null)
        {
            Debug.LogWarning($"[HazardVolume] PlayerController not found for: {other.name}", this);
            return;
        }

        Debug.Log("[HazardVolume] Hazard death requested", this);
        player.RequestHazardDeath();
    }
}