using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public sealed class PursuitSpeedArea : MonoBehaviour
{
    [Header("Speed")]
    [SerializeField] private float m_speed_multiplier = 0.7f;

    [Header("Debug")]
    [SerializeField] private bool m_draw_gizmos = true;

    private void Reset()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        PursuitEnemyController controller = other.GetComponent<PursuitEnemyController>();

        if (controller == null)
        {
            controller = other.GetComponentInParent<PursuitEnemyController>();
        }

        if (controller != null)
        {
            controller.SetAreaSpeedMultiplier(m_speed_multiplier);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        PursuitEnemyController controller = other.GetComponent<PursuitEnemyController>();

        if (controller == null)
        {
            controller = other.GetComponentInParent<PursuitEnemyController>();
        }

        if (controller != null)
        {
            controller.ResetAreaSpeedMultiplier();
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!m_draw_gizmos)
        {
            return;
        }

        Collider2D col = GetComponent<Collider2D>();
        if (col is not BoxCollider2D box)
        {
            return;
        }

        Gizmos.color = Color.green;

        Vector3 center = transform.TransformPoint(box.offset);
        Vector3 size = new Vector3(
            box.size.x * transform.lossyScale.x,
            box.size.y * transform.lossyScale.y,
            0.0f
        );

        Gizmos.DrawWireCube(center, size);
    }
}