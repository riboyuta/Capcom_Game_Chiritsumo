using UnityEngine;

// 指定したスイッチが押されている間、指定方向にスライドして開くギミック。
[DisallowMultipleComponent]
public class SlideGimmick : MonoBehaviour
{
    [Header("Target Switch")]

    [Header("監視対象のスイッチ")]
    [SerializeField] private SwitchGimmick targetSwitch;

    [Header("Slide Settings")]

    [Header("スライドするローカル方向")]
    [SerializeField] private Vector3 slideLocalDirection = Vector3.right;

    [Header("スライドする距離")]
    [SerializeField, Min(0f)] private float slideDistance = 3.0f;

    [Header("スライドする速度 (m/s)")]
    [SerializeField, Min(0.1f)] private float slideSpeed = 2.0f;

    private Vector3 initialLocalPosition;

    private void Awake()
    {
        initialLocalPosition = transform.localPosition;
    }

    private void Update()
    {
        if (targetSwitch == null) return;

        // 目標位置の計算
        Vector3 targetLocalPosition = initialLocalPosition;
        if (targetSwitch.IsPressed)
        {
            targetLocalPosition = initialLocalPosition + (slideLocalDirection.normalized * slideDistance);
        }

        // 目標位置へ向かって一定の速度で移動
        transform.localPosition = Vector3.MoveTowards(
            transform.localPosition, 
            targetLocalPosition, 
            slideSpeed * Time.deltaTime
        );
    }
}
