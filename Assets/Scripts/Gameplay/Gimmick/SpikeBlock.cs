using UnityEngine;

// トゲブロックギミック。
// プレイヤーが接触するとリスポーン処理（ハザード死）を行う。
// Collider が Trigger でも固体でもどちらでも動作する。
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public sealed class SpikeBlock : MonoBehaviour
{
    [Header("フィルタ: 反応するタグ")]
    [Tooltip("このタグを持つオブジェクトが接触した場合のみリスポーン処理を行います。")]
    [SerializeField] private string targetTag = "Player";

    // ──────────────────────────────────────────────
    // 接触検出
    // Collider が Trigger なら OnTriggerEnter、
    // 固体コライダーなら OnCollisionEnter が呼ばれる。
    // どちらでも動作するよう両方を実装する。
    // ──────────────────────────────────────────────

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(targetTag)) return;
        TryKillPlayer(other);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!collision.collider.CompareTag(targetTag)) return;
        TryKillPlayer(collision.collider);
    }

    private void TryKillPlayer(Collider other)
    {
        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null)
        {
            Debug.LogWarning($"[SpikeBlock] PlayerController not found for: {other.name}", this);
            return;
        }

        player.RequestHazardDeath();
    }
}
