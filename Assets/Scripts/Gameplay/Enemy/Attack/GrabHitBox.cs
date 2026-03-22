using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public sealed class GrabHitbox : MonoBehaviour
{
    private HandGrabAttack owner;
    private BoxCollider boxCollider;
    private bool hitEnabled = false;

    private void Awake()
    {
        boxCollider = GetComponent<BoxCollider>();
        boxCollider.isTrigger = true;
    }

    public void Initialize(HandGrabAttack owner)
    {
        this.owner = owner;
    }

    public void SetHitEnabled(bool enabled)
    {
        hitEnabled = enabled;

        if (boxCollider != null)
        {
            boxCollider.enabled = enabled;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!hitEnabled)
        {
            return;
        }

        if (!other.CompareTag("Player"))
        {
            return;
        }

        if (owner != null)
        {
            owner.NotifyGrabHit(other.gameObject);
        }
    }
}