using UnityEngine;
 
// 敵の攻撃判定を管理するヒットボックス
// トリガーコリジョンを使用してプレイヤーとの接触を検出し、ダメージやノックバックなどの効果を適用
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
    [Header("ヒット対象")]
    [SerializeField] private string playerTag = "Player";                    // ヒット対象となるタグ
    [Header("ヒット効果タイプ")]
    [SerializeField] private HitEffectType hitEffectType = HitEffectType.Damage;  // ヒット効果のタイプ
    [Header("与ダメージ量")]
    [SerializeField] private int damage = 1;                                  // 与えるダメージ量
    [Header("ノックバックの力")]
    [SerializeField] private float knockbackForce = 5.0f;                    // ノックバックの力
    [Header("掴み攻撃持続時間")]
    [SerializeField] private float grabDuration = 1.0f;                      // 掴み攻撃の持続時間（秒）
    [Header("1回の有効化で1回だけヒットするか")]
    [SerializeField] private bool hitOncePerActivation = true;             // 1回の有効化で1回だけヒットするか
    [Header("Debug")]
    [Header("デバッグログ表示")]
    [SerializeField] private bool showDebugLog = false;                     // デバッグログの表示フラグ

    private bool isActive = false;                                           // ヒットボックスが有効かどうか
    private bool hasHit = false;                                             // 今回の有効化でヒット済みかどうか

    // プロパティ：外部からアクセス可能な読み取り専用情報
    public bool IsActive => isActive;                                        // ヒットボックスがアクティブか
    // ヒットボックスを有効化する
    // 攻撃開始時に呼び出され、衝突判定を開始する
    public void ActivateHitBox()
    {
        isActive = true;
        hasHit = false;  // ヒット済みフラグをリセット
        LogDebug("Activate");
    }

    // ヒットボックスを無効化する
    // 攻撃終了時やキャンセル時に呼び出され、衝突判定を停止する
    public void DeactivateHitBox()
    {
        isActive = false;
        LogDebug("Deactivate");
    }

    // Unity の物理エンジンによるトリガー衝突検出
    // ヒットボックスが有効な状態で対象レイヤーのオブジェクトと接触した際に呼ばれる
    private void OnTriggerEnter(Collider other)
    {
        // ヒットボックスが無効な場合は処理しない
        if (!isActive)
        {
            return;
        }

        // 1回の有効化で1回だけヒットする設定の場合、既にヒット済みなら処理しない
        if (hitOncePerActivation && hasHit)
        {
            return;
        }

        // 対象タグでない場合は処理しない
        if (!IsTargetTag(other.gameObject))
        {
            return;
        }

        // ヒット方向を計算（敵→プレイヤー方向）
        Vector3 hit_direction = (other.transform.position - transform.position).normalized;
        bool did_hit = false;

        // ヒット効果のタイプに応じて処理を分岐
        switch (hitEffectType)
        {
            case HitEffectType.Damage:  // ダメージのみ（ノックバックなし）
                {
                    IDamageable damageable = other.GetComponentInParent<IDamageable>();
                    if (damageable != null)
                    {
                        damageable.TakeDamage(damage, hit_direction, 0.0f);
                        did_hit = true;
                    }
                    break;
                }

            case HitEffectType.Grab:  // 掴み攻撃
                {
                    IGrabReceiver grab_receiver = other.GetComponentInParent<IGrabReceiver>();
                    if (grab_receiver != null)
                    {
                        grab_receiver.OnGrabbed(grabDuration);
                        did_hit = true;
                    }
                    break;
                }

            case HitEffectType.Knockback:  // ノックバック付きダメージ
                {
                    IDamageable damageable = other.GetComponentInParent<IDamageable>();
                    if (damageable != null)
                    {
                        damageable.TakeDamage(damage, hit_direction, knockbackForce);
                        did_hit = true;
                    }
                    break;
                }
        }

        // ヒットした場合、ヒット済みフラグを立てる
        if (did_hit)
        {
            hasHit = true;
            LogDebug($"Hit : {other.name}");
        }
    }

    // 指定されたオブジェクトが対象タグを持っているかチェック
    private bool IsTargetTag(GameObject obj)
    {
        return obj.CompareTag(playerTag);
    }

    // デバッグログを出力（m_show_debug_logがtrueの場合のみ）
    private void LogDebug(string message)
    {
        if (!showDebugLog)
        {
            return;
        }

        Debug.Log($"[EnemyHitBox] {name} : {message}");
    }

    // Unityエディタでオブジェクト選択時にギズモを描画（デバッグ用）
    // ヒットボックスの範囲を視覚的に表示（アクティブ時は赤、非アクティブ時は灰色）
    private void OnDrawGizmosSelected()
    {
        Collider col = GetComponent<Collider>();
        if (col == null)
        {
            return;
        }

        // アクティブ状態に応じて色を変更（赤=有効、灰色=無効）
        Gizmos.color = isActive ? Color.red : Color.gray;

        // BoxColliderの場合、そのサイズに合わせてワイヤーボックスを描画
        if (col is BoxCollider box)
        {
            Vector3 center = transform.TransformPoint(box.center);
            Vector3 size = Vector3.Scale(box.size, transform.lossyScale);

            Gizmos.DrawWireCube(center, size);
        }
    }
}

// ダメージを受け取ることができるオブジェクト用のインターフェース
// プレイヤーなどダメージを受ける対象がこのインターフェースを実装する
public interface IDamageable
{
    // ダメージを受ける処理
    // 受けるダメージ量 (damage)
    // ヒットした方向（ノックバック方向の計算に使用） (hit_direction)
    // ノックバックの力（0の場合はノックバックなし） (knockback_force)
    void TakeDamage(int damage, Vector3 hit_direction, float knockback_force);
}

// 掴み攻撃を受け取ることができるオブジェクト用のインターフェース
// プレイヤーなど掴まれる対象がこのインターフェースを実装する
public interface IGrabReceiver
{
    // 掴まれた時の処理
    // 掴まれている時間（秒） (duration)
    void OnGrabbed(float duration);
}