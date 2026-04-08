using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public sealed class GrabHitbox : MonoBehaviour
{
    private BoxCollider boxCollider;

    private void Awake()
    {
        boxCollider = GetComponent<BoxCollider>();
        boxCollider.isTrigger = true;
    }

    public GameObject FindPlayerInGrabArea()
    {
        if (boxCollider == null)
        {
            return null;
        }

        Vector3 worldCenter = transform.TransformPoint(boxCollider.center);
        Vector3 halfExtents = Vector3.Scale(boxCollider.size * 0.5f, transform.lossyScale);
        Quaternion orientation = transform.rotation;

        Collider[] hits = Physics.OverlapBox(
            worldCenter,
            halfExtents,
            orientation,
            ~0,
            QueryTriggerInteraction.Collide
        );

        for (int i = 0; i < hits.Length; i++)
        {
            Collider hit = hits[i];
            if (hit == null)
            {
                continue;
            }

            if (!hit.CompareTag("Player"))
            {
                continue;
            }

            return hit.gameObject;
        }

        return null;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        BoxCollider targetCollider = boxCollider != null ? boxCollider : GetComponent<BoxCollider>();
        if (targetCollider == null)
        {
            return;
        }

        Matrix4x4 oldMatrix = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(targetCollider.center, targetCollider.size);
        Gizmos.matrix = oldMatrix;
    }
#endif
}