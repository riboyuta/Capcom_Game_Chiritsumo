using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
public sealed class CameraZoneVolumeRelay : MonoBehaviour
{
    // 親の CameraZone を自動解決して使う。
    private CameraZone ownerZone;
    private BoxCollider boxCollider;

    private void Reset()
    {
        ResolveReferences();
    }

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnValidate()
    {
        ResolveReferences();
    }

    private void ResolveReferences()
    {
        if (boxCollider == null)
        {
            boxCollider = GetComponent<BoxCollider>();
        }

        if (boxCollider != null)
        {
            boxCollider.isTrigger = true;
        }

        if (ownerZone == null && transform.parent != null)
        {
            ownerZone = transform.parent.GetComponent<CameraZone>();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (ownerZone == null)
        {
            return;
        }

        ownerZone.NotifyEnter(other);
    }

    private void OnTriggerExit(Collider other)
    {
        if (ownerZone == null)
        {
            return;
        }

        ownerZone.NotifyExit(other);
    }
}