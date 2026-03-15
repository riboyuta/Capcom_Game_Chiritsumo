using UnityEngine;

public sealed class EnemyHitBox : MonoBehaviour
{
    public enum HitEffectType
    {
        Damage,
        Grab,
        Knockback
    }

    [Header("HitBox Settings")]
    [SerializeField] private LayerMask m_target_layer;
    [SerializeField] private HitEffectType m_hit_effect_type = HitEffectType.Damage;
    [SerializeField] private int m_damage = 1;
    [SerializeField] private float m_knockback_force = 5.0f;
    [SerializeField] private float m_grab_duration = 1.0f;
    [SerializeField] private bool m_hit_once_per_activation = true;

    [Header("Debug")]
    [SerializeField] private bool m_show_debug_log = false;

    private bool m_is_active = false;
    private bool m_has_hit = false;

    public bool IsActive => m_is_active;

    public void ActivateHitBox()
    {
        m_is_active = true;
        m_has_hit = false;
        LogDebug("Activate");
    }

    public void DeactivateHitBox()
    {
        m_is_active = false;
        LogDebug("Deactivate");
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!m_is_active)
        {
            return;
        }

        if (m_hit_once_per_activation && m_has_hit)
        {
            return;
        }

        if (!IsTargetLayer(other.gameObject.layer))
        {
            return;
        }

        Vector2 hit_direction = (other.transform.position - transform.position).normalized;
        bool did_hit = false;

        switch (m_hit_effect_type)
        {
            case HitEffectType.Damage:
                {
                    IDamageable damageable = other.GetComponentInParent<IDamageable>();
                    if (damageable != null)
                    {
                        damageable.TakeDamage(m_damage, hit_direction, 0.0f);
                        did_hit = true;
                    }
                    break;
                }

            case HitEffectType.Grab:
                {
                    IGrabReceiver grab_receiver = other.GetComponentInParent<IGrabReceiver>();
                    if (grab_receiver != null)
                    {
                        grab_receiver.OnGrabbed(m_grab_duration);
                        did_hit = true;
                    }
                    break;
                }

            case HitEffectType.Knockback:
                {
                    IDamageable damageable = other.GetComponentInParent<IDamageable>();
                    if (damageable != null)
                    {
                        damageable.TakeDamage(m_damage, hit_direction, m_knockback_force);
                        did_hit = true;
                    }
                    break;
                }
        }

        if (did_hit)
        {
            m_has_hit = true;
            LogDebug($"Hit : {other.name}");
        }
    }

    private bool IsTargetLayer(int layer)
    {
        return ((1 << layer) & m_target_layer) != 0;
    }

    private void LogDebug(string message)
    {
        if (!m_show_debug_log)
        {
            return;
        }

        Debug.Log($"[EnemyHitBox] {name} : {message}");
    }

    private void OnDrawGizmosSelected()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col == null)
        {
            return;
        }

        Gizmos.color = m_is_active ? Color.red : Color.gray;

        if (col is BoxCollider2D box)
        {
            Vector3 center = transform.TransformPoint(box.offset);
            Vector3 size = new Vector3(
                box.size.x * transform.lossyScale.x,
                box.size.y * transform.lossyScale.y,
                0.0f
            );

            Gizmos.DrawWireCube(center, size);
        }
    }
}

public interface IDamageable
{
    void TakeDamage(int damage, Vector2 hit_direction, float knockback_force);
}

public interface IGrabReceiver
{
    void OnGrabbed(float duration);
}