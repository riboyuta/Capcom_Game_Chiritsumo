using UnityEngine;

/// <summary>
/// 敵の移動速度を変更するエリア
/// このエリア内に敵が入ると、速度倍率が適用され、出ると元に戻る
/// 例：沼地や氷の上などで敵の移動速度を遅くしたり速くしたりする
/// </summary>
[RequireComponent(typeof(Collider))]
public sealed class PursuitSpeedArea : MonoBehaviour
{
    [Header("Speed")]
    [SerializeField] private float m_speed_multiplier = 0.7f;       // このエリア内での速度倍率（1.0未満で減速、1.0より大きいと加速）

    [Header("Debug")]
    [SerializeField] private bool m_draw_gizmos = true;             // エリア範囲のギズモを描画するか

    /// <summary>
    /// Unityエディタでコンポーネント追加時に自動で呼ばれる
    /// Colliderを自動的にトリガーモードに設定
    /// </summary>
    private void Reset()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    /// <summary>
    /// エリアに何かが侵入した時に呼ばれる
    /// PursuitEnemyControllerを持つオブジェクトの場合、速度倍率を適用
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        // まず直接PursuitEnemyControllerを取得
        PursuitEnemyController controller = other.GetComponent<PursuitEnemyController>();

        // 見つからなければ親オブジェクトから探す
        if (controller == null)
        {
            controller = other.GetComponentInParent<PursuitEnemyController>();
        }

        // コントローラーが見つかった場合、速度倍率を適用
        if (controller != null)
        {
            controller.SetAreaSpeedMultiplier(m_speed_multiplier);
        }
    }

    /// <summary>
    /// エリアから何かが退出した時に呼ばれる
    /// PursuitEnemyControllerを持つオブジェクトの場合、速度倍率をリセット（通常速度に戻す）
    /// </summary>
    private void OnTriggerExit(Collider other)
    {
        // まず直接PursuitEnemyControllerを取得
        PursuitEnemyController controller = other.GetComponent<PursuitEnemyController>();

        // 見つからなければ親オブジェクトから探す
        if (controller == null)
        {
            controller = other.GetComponentInParent<PursuitEnemyController>();
        }

        // コントローラーが見つかった場合、速度倍率をリセット
        if (controller != null)
        {
            controller.ResetAreaSpeedMultiplier();
        }
    }

    /// <summary>
    /// Unityエディタでオブジェクト選択時にギズモを描画（デバッグ用）
    /// エリアの範囲を緑色のワイヤーボックスで視覚的に表示
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        if (!m_draw_gizmos)
        {
            return;
        }

        Collider col = GetComponent<Collider>();
        if (col is not BoxCollider box)
        {
            return;
        }

        // エリア範囲を緑色のワイヤーボックスで描画
        Gizmos.color = Color.green;

        Vector3 center = transform.TransformPoint(box.center);  // エリアの中心位置
        Vector3 size = Vector3.Scale(box.size, transform.lossyScale);

        Gizmos.DrawWireCube(center, size);
    }
}