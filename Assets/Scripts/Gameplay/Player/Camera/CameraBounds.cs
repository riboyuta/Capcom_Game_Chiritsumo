using UnityEngine;

// 同じ GameObject にこのコンポーネントを複数付けることを防ぐ。
[DisallowMultipleComponent]
// このコンポーネントは BoxCollider が必須。
[RequireComponent(typeof(BoxCollider))]
public sealed class CameraBounds : MonoBehaviour
{
    [Header("境界の左下ポイント（X小・Y小）")]
    // カメラ移動境界の左下側を決める基準点。
    // 2D横スクロール想定では「左下」に置くのが分かりやすい。
    [SerializeField] private Transform minPoint;

    [Header("境界の右上ポイント（X大・Y大）")]
    // カメラ移動境界の右上側を決める基準点。
    // 2D横スクロール想定では「右上」に置くのが分かりやすい。
    [SerializeField] private Transform maxPoint;

    [Header("境界計算に使う BoxCollider")]
    // 実際にカメラ制限用の Bounds を作るための BoxCollider。
    // minPoint / maxPoint の位置から center / size を自動計算して設定する。
    [SerializeField] private BoxCollider boxCollider;

    // 外部から参照する用のワールド座標系 Bounds。
    // PlayerCameraController などがこの範囲を使ってカメラ位置を Clamp する。
    public Bounds WorldBounds => boxCollider.bounds;

    private void Reset()
    {
        // 同じ GameObject についている BoxCollider を自動取得する。
        boxCollider = GetComponent<BoxCollider>();

        // 物理衝突ではなく境界定義用途なので Trigger に固定する。
        boxCollider.isTrigger = true;

        // Bounds 本体は回転・拡縮させず固定運用にする。
        // こうしておくと「ポイントから境界を作る」計算が分かりやすい。
        transform.rotation = Quaternion.identity;
        transform.localScale = Vector3.one;
    }

    private void Awake()
    {
        // 実行時に必要な参照を補完する。
        EnsureReferences();

        // ポイント位置から BoxCollider の形を反映する。
        ApplyFromPoints();
    }

    private void OnValidate()
    {
        // Inspector 変更時にも参照補完を行う。
        EnsureReferences();

        // 値変更に合わせてすぐ BoxCollider を更新する。
        ApplyFromPoints();
    }

    private void EnsureReferences()
    {
        // BoxCollider が未設定なら自動取得する。
        if (boxCollider == null)
        {
            boxCollider = GetComponent<BoxCollider>();
        }

        // BoxCollider が存在するなら Trigger に固定する。
        if (boxCollider != null)
        {
            boxCollider.isTrigger = true;
        }

        // このオブジェクト自体の回転・拡縮は固定にする。
        // 理由：
        // - 回転が入ると「左下」「右上」の意味が直感とズレやすい
        // - Scale が入ると size の見え方が分かりにくくなる
        transform.rotation = Quaternion.identity;
        transform.localScale = Vector3.one;
    }

    private void ApplyFromPoints()
    {
        // どれか未設定なら境界を更新できないので終了。
        if (boxCollider == null || minPoint == null || maxPoint == null)
        {
            return;
        }

        // 2点のうち、各軸で小さい方を min、大きい方を max として採用する。
        // そのため Scene 上で minPoint / maxPoint を逆に置いても壊れにくい。
        Vector3 min = Vector3.Min(minPoint.position, maxPoint.position);
        Vector3 max = Vector3.Max(minPoint.position, maxPoint.position);

        // 2点の中間が BoxCollider の中心になる。
        Vector3 center = (min + max) * 0.5f;

        // 2点の差分が BoxCollider のサイズになる。
        Vector3 size = max - min;

        // center はワールド座標なので、BoxCollider の local center に変換して入れる。
        boxCollider.center = transform.InverseTransformPoint(center);

        // size は軸平行な境界サイズとしてそのまま設定する。
        boxCollider.size = size;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        // BoxCollider 参照がなければ取得を試みる。
        if (boxCollider == null)
        {
            boxCollider = GetComponent<BoxCollider>();
            if (boxCollider == null)
            {
                // 取得できなければ Gizmo 描画できない。
                return;
            }
        }

        // BoxCollider のローカル座標系に合わせて Gizmo を描画する。
        Gizmos.matrix = transform.localToWorldMatrix;

        // 半透明の塗りで境界範囲を見やすく表示する。
        Gizmos.color = new Color(0f, 1f, 1f, 0.15f);
        Gizmos.DrawCube(boxCollider.center, boxCollider.size);

        // 枠線をシアンで表示する。
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(boxCollider.center, boxCollider.size);

        // minPoint は青で表示する。
        // Scene 上では「左下側の基準点」として使う想定。
        if (minPoint != null)
        {
            // ポイント球はワールド座標で描きたいので行列を戻す。
            Gizmos.matrix = Matrix4x4.identity;
            Gizmos.color = Color.blue;
            Gizmos.DrawSphere(minPoint.position, 0.15f);
        }

        // maxPoint は赤で表示する。
        // Scene 上では「右上側の基準点」として使う想定。
        if (maxPoint != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(maxPoint.position, 0.15f);
        }
    }
#endif
}