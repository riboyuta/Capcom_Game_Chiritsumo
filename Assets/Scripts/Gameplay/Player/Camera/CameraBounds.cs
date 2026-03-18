using UnityEngine;

// 同一GameObjectに複数付かないようにする。
[DisallowMultipleComponent]
// 必ず BoxCollider が必要なコンポーネントであることを保証する。
[RequireComponent(typeof(BoxCollider))]
public sealed class CameraBounds : MonoBehaviour
{
    [Header("指定コライダー")]
    // カメラ移動範囲を表す BoxCollider 参照。
    [SerializeField] private BoxCollider boxCollider;

    // ワールド座標系での境界情報を外部へ公開する。
    public Bounds WorldBounds => boxCollider.bounds;

    private void Reset()
    {
        // コンポーネント追加時や Reset 時に BoxCollider を自動取得する。
        boxCollider = GetComponent<BoxCollider>();

        // 境界判定用途のみなので Trigger に固定する。
        boxCollider.isTrigger = true;
    }

    private void OnValidate()
    {
        // インスペクター上で参照が外れていたら自動で再取得する。
        if (boxCollider == null)
        {
            boxCollider = GetComponent<BoxCollider>();
        }

        // BoxCollider が存在する場合は Trigger に固定する。
        if (boxCollider != null)
        {
            boxCollider.isTrigger = true;
        }
    }

    private void Awake()
    {
        // 実行時に参照が未設定なら取得しておく。
        if (boxCollider == null)
        {
            boxCollider = GetComponent<BoxCollider>();
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        // Gizmo 描画時に参照がなければ取得を試みる。
        if (boxCollider == null)
        {
            boxCollider = GetComponent<BoxCollider>();
            if (boxCollider == null)
            {
                // それでも取得できなければ描画しない。
                return;
            }
        }

        // BoxCollider のローカル位置・回転・スケールを反映して Gizmo を描画する。
        Gizmos.matrix = transform.localToWorldMatrix;

        // 半透明の塗りつぶしで範囲を見やすくする。
        Gizmos.color = new Color(0f, 1f, 1f, 0.2f);
        Gizmos.DrawCube(boxCollider.center, boxCollider.size);

        // 枠線をシアンで描画する。
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(boxCollider.center, boxCollider.size);
    }
#endif
}