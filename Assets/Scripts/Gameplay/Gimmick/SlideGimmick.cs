using UnityEngine;

// 指定したスイッチが押されている間、指定方向にスライドして開くギミック。
[DisallowMultipleComponent]
public class SlideGimmick : MonoBehaviour
{
    [Header("ターゲットスイッチ")]
    [Tooltip("監視対象の SwitchGimmick。割り当てたスイッチが押されている間、本ギミックが動作します。シーン内のスイッチオブジェクトをセットしてください。")]
    [SerializeField] private SwitchGimmick targetSwitch;

    [Header("スライド方向（ローカル）")]
    [Tooltip("オブジェクトのローカル空間でのスライド方向。正規化されスライド距離と掛け合わされ、目標位置が決まります。")]
    [SerializeField] private Vector3 slideLocalDirection = Vector3.right;

    [Header("スライド距離（メートル）")]
    [Tooltip("スイッチが押されたときに移動する距離（m）。大きくするとより遠くまでスライドします。")]
    [SerializeField, Min(0f)] private float slideDistance = 3.0f;

    [Header("スライド速度（m/s）")]
    [Tooltip("目標位置へ向かって移動する速度（m/s）。値を大きくすると短時間で到達します。")]
    [SerializeField, Min(0.1f)] private float slideSpeed = 2.0f;

    private Vector3 initialLocalPosition;


    //生成されたときにスイッチを探すため！
    public void SetSwitch(SwitchGimmick sw)
    {
        targetSwitch = sw;
    }


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