using UnityEngine;


/// 1レイヤー分のパララックススクロールとループ処理を担当する。
/// 同じスプライトを持つ2つの子オブジェクト（spriteA / spriteB）を交互にループさせる。
/// </summary>
public sealed class ParallaxLayer : MonoBehaviour
{
    [Header("スプライト設定")]
    [Tooltip("1枚目の背景スプライト Transform")]
    [SerializeField] private Transform spriteA;

    [Tooltip("2枚目の背景スプライト Transform（spriteA と同じ画像）")]
    [SerializeField] private Transform spriteB;

    [Header("パララックス設定")]
    [Tooltip("スクロール速度の倍率。0 = 静止、1 = カメラと同速。遠景ほど小さくする。")]
    [SerializeField, Range(0f, 1f)] private float speedMultiplier = 0.5f;

    // スプライト1枚分の幅（ワールド単位）。
    private float spriteWidth;

    private void Start()
    {
        CalculateSpriteWidth();
        InitializePositions();
    }

    /// スプライトの幅を SpriteRenderer の bounds から取得する。
    private void CalculateSpriteWidth()
    {
        SpriteRenderer sr = spriteA.GetComponent<SpriteRenderer>();

        if (sr != null)
        {
            spriteWidth = sr.bounds.size.x;
        }
        else
        {
            Renderer renderer = spriteA.GetComponent<Renderer>();

            if (renderer != null)
            {
                spriteWidth = renderer.bounds.size.x;
            }
            else
            {
                Debug.LogError($"[ParallaxLayer] {spriteA.name} に Renderer がありません。");
                spriteWidth = 20f;
            }
        }
    }

    /// spriteB を spriteA の右隣に配置する。
    private void InitializePositions()
    {
        Vector3 posB = spriteA.position;
        posB.x += spriteWidth;
        spriteB.position = posB;
    }

    
    /// 指定された移動量に speedMultiplier を掛けてスクロールし、
    /// 画面外に出たスプライトをループさせる。
    /// ParallaxBackground から毎フレーム呼ばれる。

    /// <param name="rawDelta">カメラの移動量または自動スクロール量（ワールド単位）。</param>
    /// <param name="cameraX">現在のカメラの X 座標（ループ判定の基準）。</param>
    public void Scroll(float rawDelta, float cameraX)
    {
        float delta = rawDelta * speedMultiplier;

        // 両スプライトを移動。
        spriteA.position += Vector3.left * delta;
        spriteB.position += Vector3.left * delta;

        // カメラ位置を基準にループ判定を行う。
        WrapSprite(spriteA, spriteB, cameraX);
        WrapSprite(spriteB, spriteA, cameraX);
    }

    
    /// check がカメラから spriteWidth 以上離れた場合、
    /// other の反対側に回り込ませる。

    private void WrapSprite(Transform check, Transform other, float cameraX)
    {
        float distance = check.position.x - cameraX;

        // 左に行き過ぎた → other の右隣へ移動。
        if (distance < -spriteWidth)
        {
            Vector3 pos = check.position;
            pos.x = other.position.x + spriteWidth;
            check.position = pos;
        }
        // 右に行き過ぎた → other の左隣へ移動（逆方向スクロール対応）。
        else if (distance > spriteWidth)
        {
            Vector3 pos = check.position;
            pos.x = other.position.x - spriteWidth;
            check.position = pos;
        }
    }

    
    /// パララックス係数を返す（外部参照用）。

    public float SpeedMultiplier => speedMultiplier;
}
