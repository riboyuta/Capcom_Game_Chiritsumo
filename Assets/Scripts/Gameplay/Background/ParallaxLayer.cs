using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UIElements;


/// 1レイヤー分のパララックススクロールとループ処理を担当する。
/// 同じスプライトを持つ2つの子オブジェクト（spriteA / spriteB）を交互にループさせる。
/// </summary>
public sealed class ParallaxLayer : MonoBehaviour
{
    [Header("スプライト設定")]
    [Tooltip("背景スプライト Transform")]
    [SerializeField] private Transform[] sprite;

     //背景の描写イメージはこんな感じ
     // [A][B]
     // [C][D]

    [Header("パララックスのX設定")]
    [Tooltip("スクロール速度の倍率。0 = 静止、1 = カメラと同速。遠景ほど小さくする。")]
    [SerializeField, Range(0f, 1f)] private float speedMultiplierX = 0.5f;

    [Header("パララックスのY設定")]
    [Tooltip("スクロール速度の倍率。0 = 静止、1 = カメラと同速。遠景ほど小さくする。")]
    [SerializeField, Range(0f, 1f)] private float speedMultiplierY = 0.2f;

    [Header("グリッドサイズ")]
    [Tooltip("配置する枚数。n*nで配置")]
    [SerializeField, Range(2, 4)] private int gridSize = 3;

    [Header("スプライト間隔補正")]
    [Tooltip("間隔を近づける補正値。隙間のちらつき防止")]
    [SerializeField] private float overlap = 0.01f;

    // スプライト1枚分の幅（ワールド単位）。
    private float spriteWidth;
    private float spriteHeight;

    //スプライトにoverlapの補正を掛けた値
    private float tileWidth;
    private float tileHeight;

    private void Start()
    {
        CalculateSpriteWidth();
        InitializePositions();
    }

    /// スプライトの幅を SpriteRenderer の bounds から取得する。
    private void CalculateSpriteWidth()
    {

        if (sprite == null || sprite.Length == 0)
        {
            Debug.LogError("[ParallaxLayer] Sprite が設定されていません。");
            return;
        }

        SpriteRenderer sr = sprite[0].GetComponent<SpriteRenderer>();

        if (sr != null)
        {
            spriteWidth = sr.bounds.size.x;
            spriteHeight = sr.bounds.size.y;
        }
        else
        {
            Renderer renderer = sprite[0].GetComponent<Renderer>();

            if (renderer != null)
            {
                spriteWidth = renderer.bounds.size.x;
                spriteHeight = renderer.bounds.size.y;
            }
            else
            {
                Debug.LogError($"[ParallaxLayer] {sprite[0].name} に Renderer がありません。");
                spriteWidth = 20f;
                spriteHeight = 20f;
            }
        }

        tileWidth = spriteWidth - overlap;
        tileHeight = spriteHeight - overlap;

    }


    private void InitializePositions()
    {
        Vector3 origin = sprite[0].position;

        for (int y = 0; y < gridSize; y++)
        {
            for (int x = 0; x < gridSize; x++)
            {
                int index = y * gridSize + x;

                if (index >= sprite.Length)
                {
                    return;
                }

              
                sprite[index].position =
                    origin +
                    new Vector3(
                        (tileWidth) * x,
                        (tileHeight) * y,
                        0f);
            }

        }

    }


    /// 指定された移動量に speedMultiplier を掛けてスクロールし、
    /// 画面外に出たスプライトをループさせる。
    /// ParallaxBackground から毎フレーム呼ばれる。

    /// <param name="rawDelta">カメラの移動量または自動スクロール量（ワールド単位）。</param>
    /// <param name="cameraX">現在のカメラの X 座標（ループ判定の基準）。</param>
    /// <param name="cameraY">現在のカメラの Y 座標（ループ判定の基準）。</param>
    ///    
    public void Scroll(float rawDeltaX, float rawDeltaY, float cameraX, float cameraY)
    {
        float deltaX = rawDeltaX * speedMultiplierX;
        float deltaY = rawDeltaY * speedMultiplierY;

        Vector3 move =
            Vector3.left * deltaX +
            Vector3.down * deltaY;

        for (int i = 0; i < sprite.Length; i++)
        {
            sprite[i].position += move;
        }

        for (int i = 0; i < sprite.Length; i++)
        {
            WrapSprite(
                sprite[i],
                cameraX,
                cameraY);
        }

    }

    
    /// check がカメラから spriteWidth 以上離れた場合、
    /// other の反対側に回り込ませる。

    private void WrapSprite(Transform check, float cameraX, float cameraY)
    {
        float distanceX = check.position.x - cameraX;
        float distanceY = check.position.y - cameraY;

        float halfWidth = tileWidth * gridSize * 0.5f;
        float halfHeight = tileHeight * gridSize * 0.5f;

        if (distanceX < -halfWidth)
        {
            Vector3 pos = check.position;
            pos.x += tileWidth * gridSize;
            check.position = pos;
        }
        else if (distanceX > halfWidth)
        {
            Vector3 pos = check.position;
            pos.x -= tileWidth * gridSize;
            check.position = pos;
        }

        if (distanceY < -halfHeight)
        {
            Vector3 pos = check.position;
            pos.y += tileHeight * gridSize;
            check.position = pos;
        }
        else if (distanceY > halfHeight)
        {
            Vector3 pos = check.position;
            pos.y -= tileHeight * gridSize;
            check.position = pos;
        }
    }

    
    /// パララックス係数を返す（外部参照用）。

    public float SpeedMultiplier => speedMultiplierX;
}
