using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public sealed class SmashHitBox : MonoBehaviour
{
    private HandSmashAttack owner;
    private BoxCollider boxCollider;
    private bool hitEnabled = false;

    private void Awake()
    {
        // BoxColliderを取得してトリガーに設定
        boxCollider = GetComponent<BoxCollider>();
        boxCollider.isTrigger = true;
    }

    // 親攻撃オブジェクトを設定
    public void Initialize(HandSmashAttack owner)
    {
        this.owner = owner;
    }

    // 当たり判定の有効/無効を設定
    public void SetHitEnabled(bool enabled)
    {
        hitEnabled = enabled;

        // Colliderも同時に有効/無効化
        if (boxCollider != null)
        {
            boxCollider.enabled = enabled;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // 当たり判定が無効なら何もしない
        if (!hitEnabled)
        {
            return;
        }

        // プレイヤー以外は無視
        if (!other.CompareTag("Player"))
        {
            return;
        }

        // 親攻撃オブジェクトにプレイヤーヒットを通知
        if (owner != null)
        {
            owner.NotifyPlayerHit(other.gameObject);
        }
    }
}
