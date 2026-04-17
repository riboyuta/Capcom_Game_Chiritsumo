using UnityEngine;


/// 複数の ParallaxLayer を統括し、カメラ追従 または 自動スクロール で
/// パララックス背景を駆動するコントローラー。
/// </summary>
public sealed class ParallaxBackground : MonoBehaviour
{
    [Header("レイヤー設定")]
    [Tooltip("管理するパララックスレイヤーの配列。Inspector でアサインする。")]
    [SerializeField] private ParallaxLayer[] layers;

    [Header("カメラ追従設定")]
    [Tooltip("追従するカメラの Transform。null の場合は Camera.main を使用する。")]
    [SerializeField] private Transform cameraTransform;

    [Header("自動スクロール設定")]
    [Tooltip("自動スクロール速度（ワールド単位/秒）。0 の場合はカメラ追従モードになる。")]
    [SerializeField] private float autoScrollSpeed = 0f;

    // 前フレームのカメラX座標（追従モード用）。
    private float previousCameraX;

    // 自動スクロールかどうか。
    private bool IsAutoScroll => !Mathf.Approximately(autoScrollSpeed, 0f);

    private void Start()
    {
        // カメラが未設定の場合は Main Camera を取得する。
        if (cameraTransform == null)
        {
            Camera mainCam = Camera.main;

            if (mainCam != null)
            {
                cameraTransform = mainCam.transform;
            }
            else
            {
                Debug.LogWarning("[ParallaxBackground] Camera が見つかりません。自動スクロールのみ動作します。");
            }
        }

        if (cameraTransform != null)
        {
            previousCameraX = cameraTransform.position.x;
        }
    }

    
    /// LateUpdate でスクロール処理を実行する。
    /// カメラ移動後に背景を動かすため LateUpdate を使用する。

    private void LateUpdate()
    {
        float delta;
        float cameraX = cameraTransform != null ? cameraTransform.position.x : 0f;

        if (IsAutoScroll)
        {
            // 自動スクロールモード：一定速度でスクロール。
            delta = autoScrollSpeed * Time.deltaTime;
        }
        else if (cameraTransform != null)
        {
            // カメラ追従モード：カメラの移動量を取得。
            delta = cameraX - previousCameraX;
            previousCameraX = cameraX;
        }
        else
        {
            return;
        }

        // 各レイヤーにスクロール量とカメラ位置を通知。
        for (int i = 0; i < layers.Length; i++)
        {
            if (layers[i] != null)
            {
                layers[i].Scroll(delta, cameraX);
            }
        }
    }

    
    /// 自動スクロール速度を動的に変更する。

    public void SetAutoScrollSpeed(float speed)
    {
        autoScrollSpeed = speed;
    }

    
    /// 追従対象のカメラを動的に変更する。

    public void SetCameraTransform(Transform cam)
    {
        cameraTransform = cam;

        if (cameraTransform != null)
        {
            previousCameraX = cameraTransform.position.x;
        }
    }
}
