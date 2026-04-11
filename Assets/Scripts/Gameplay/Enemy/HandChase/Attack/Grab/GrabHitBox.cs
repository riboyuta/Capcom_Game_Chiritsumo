using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public sealed class GrabHitbox : MonoBehaviour
{
    private BoxCollider boxCollider;

    private void Awake()
    {
        // BoxColliderを取得してトリガーに設定
        boxCollider = GetComponent<BoxCollider>();
        boxCollider.isTrigger = true;
    }

    // 掌み範囲内にプレイヤーがいるかチェック
    public GameObject FindPlayerInGrabArea()
    {
        if (boxCollider == null)
        {
            return null;
        }

        // BoxColliderのワールド空間の位置とサイズを計算
        Vector3 worldCenter = transform.TransformPoint(boxCollider.center);
        Vector3 halfExtents = Vector3.Scale(boxCollider.size * 0.5f, transform.lossyScale);
        Quaternion orientation = transform.rotation;

        // OverlapBoxでヒットした全てのColliderを取得
        Collider[] hits = Physics.OverlapBox(
            worldCenter,
            halfExtents,
            orientation,
            ~0,  // 全レイヤー
            QueryTriggerInteraction.Collide
        );

        // ヒットしたColliderからプレイヤーを探す
        for (int i = 0; i < hits.Length; i++)
        {
            Collider hit = hits[i];
            if (hit == null)
            {
                continue;
            }

            // "Player"タグを持つオブジェクトを返す
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