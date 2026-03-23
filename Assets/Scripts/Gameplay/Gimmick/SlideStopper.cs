using UnityEngine;

// スライドギミックの移動を遮るストッパー
// SlideGimmick側で、このコンポーネントを持つオブジェクトに触れた場合に移動を停止します。
[RequireComponent(typeof(Collider))]
public class SlideStopper : MonoBehaviour
{
    private void Awake()
    {
        // 念のためトリガー設定を有効にする
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }
}
