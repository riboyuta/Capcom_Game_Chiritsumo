using UnityEngine;

/// <summary>
/// 敵の攻撃判定を管理するヒットボックス
/// トリガーコリジョンを使用してプレイヤーとの接触を検出し、ダメージやノックバックなどの効果を適用
/// </summary>
public sealed class EnemyHitBox : MonoBehaviour
{
    // ヒット時に適用する効果のタイプ
    public enum HitEffectType
    {
        Damage,      // ダメージのみ
        Grab,        // 掴み攻撃
        Knockback    // ノックバック付きダメージ
    }

    [Header("HitBox Settings")]
    [SerializeField] private LayerMask targetLayer;                             // ヒット対象となるレイヤー
    [SerializeField] private HitEffectType hitEffectType = HitEffectType.Damage;  // ヒット効果のタイプ
    [SerializeField] private int damage = 1;                                     // 与えるダメージ量
    [SerializeField] private float knockbackForce = 5.0f;                        // ノックバックの力
    [SerializeField] private float grabDuration = 1.0f;                          // 掴み攻撃の持続時間（秒）
    [SerializeField] private bool hitOncePerActivation = true;                   // 1回の有効化で1回だけヒットするか

    [Header("Debug")]
    [SerializeField] private bool showDebugLog = false;                          // デバッグログの表示フラグ

    private bool isActive = false;                                               // ヒットボックスが有効かどうか
    private bool hasHit = false;                                                 // 今回の有効化でヒット済みかどうか

    // プロパティ：外部からアクセス可能な読み取り専用情報
    public bool IsActive => isActive;                                            // ヒットボックスがアクティブか

    /// <summary>
    /// ヒットボックスを有効化する
    /// 攻撃開始時に呼び出され、衝突判定を開始する
    /// </summary>
    public void ActivateHitBox()
    {
        isActive = true;
        hasHit = false;  // ヒット済みフラグをリセット
        LogDebug("Activate");
    }

    /// <summary>
    /// ヒットボックスを無効化する
    /// 攻撃終了時やキャンセル時に呼び出され、衝突判定を停止する
    /// </summary>
    public void DeactivateHitBox()
    {
        isActive = false;
        LogDebug("Deactivate");
    }

    /// <summary>
    /// Unity の物理エンジンによるトリガー衝突検出
    /// ヒットボックスが有効な状態で対象レイヤーのオブジェクトと接触した際に呼ばれる
    /// </summary>
    private void OnTriggerEnter2D(Collider2D other)
    {
        // ヒットボックスが無効な場合は処理しない
        if (!m_is_active)
        {
            return;
        }

        // 1回の有効化で1回だけヒットする設定の場合、既にヒット済みなら処理しない
        if (m_hit_once_per_activation && m_has_hit)
        {
            return;
        }

        // 対象レイヤーでない場合は処理しない
        if (!IsTargetLayer(other.gameObject.layer))
        {
            return;
        }

        // ヒット方向を計算（敵→プレイヤー方向）
        Vector2 hit_direction = (other.transform.position - transform.position).normalized;
        bool did_hit = false;

        // ヒット効果のタイプに応じて処理を分岐
        switch (m_hit_effect_type)
        {
            case HitEffectType.Damage:  // ダメージのみ（ノックバックなし）
                {
                    IDamageable damageable = other.GetComponentInParent<IDamageable>();
                    if (damageable != null)
                    {
                        damageable.TakeDamage(m_damage, hit_direction, 0.0f);
                        did_hit = true;
                    }
                    break;
                }

            case HitEffectType.Grab:  // 掴み攻撃
                {
                    IGrabReceiver grab_receiver = other.GetComponentInParent<IGrabReceiver>();
                    if (grab_receiver != null)
                    {
                        grab_receiver.OnGrabbed(m_grab_duration);
                        did_hit = true;
                    }
                    break;
                }

            case HitEffectType.Knockback:  // ノックバック付きダメージ
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

        // ヒットした場合、ヒット済みフラグを立てる
        if (did_hit)
        {
            m_has_hit = true;
            LogDebug($"Hit : {other.name}");
        }
    }

    /// <summary>
    /// 指定されたレイヤーが対象レイヤーに含まれているかチェック
    /// </summary>
    private bool IsTargetLayer(int layer)
    {
        return ((1 << layer) & m_target_layer) != 0;
    }

    /// <summary>
    /// デバッグログを出力（m_show_debug_logがtrueの場合のみ）
    /// </summary>
    private void LogDebug(string message)
    {
        if (!m_show_debug_log)
        {
            return;
        }

        Debug.Log($"[EnemyHitBox] {name} : {message}");
    }

    /// <summary>
    /// Unityエディタでオブジェクト選択時にギズモを描画（デバッグ用）
    /// ヒットボックスの範囲を視覚的に表示（アクティブ時は赤、非アクティブ時は灰色）
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col == null)
        {
            return;
        }

        // アクティブ状態に応じて色を変更（赤=有効、灰色=無効）
        Gizmos.color = m_is_active ? Color.red : Color.gray;

        // BoxCollider2Dの場合、そのサイズに合わせてワイヤーボックスを描画
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

/// <summary>
/// ダメージを受け取ることができるオブジェクト用のインターフェース
/// プレイヤーなどダメージを受ける対象がこのインターフェースを実装する
/// </summary>
public interface IDamageable
{
    /// <summary>
    /// ダメージを受ける処理
    /// </summary>
    /// <param name="damage">受けるダメージ量</param>
    /// <param name="hitDirection">ヒットした方向（ノックバック方向の計算に使用）</param>
    /// <param name="knockbackForce">ノックバックの力（0の場合はノックバックなし）</param>
    void TakeDamage(int damage, Vector2 hitDirection, float knockbackForce);
}

/// <summary>
/// 掴み攻撃を受け取ることができるオブジェクト用のインターフェース
/// プレイヤーなど掴まれる対象がこのインターフェースを実装する
/// </summary>
public interface IGrabReceiver
{
    /// <summary>
    /// 掴まれた時の処理
    /// </summary>
    /// <param name="duration">掴まれている時間（秒）</param>
    void OnGrabbed(float duration);
}