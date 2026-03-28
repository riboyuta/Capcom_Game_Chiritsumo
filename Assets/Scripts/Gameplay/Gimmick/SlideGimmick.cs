using UnityEngine;

// 指定したスイッチが押されている間、指定方向にスライドして開くギミック。
[DisallowMultipleComponent]
public class SlideGimmick : MonoBehaviour, IRespawnResettable
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
    private float currentDistance = 0f;
    private bool isBlocked = false;
    private Collider myCollider;
    private bool hasCapturedInitialState;

    //生成されたときにスイッチを探すため！
    public void SetSwitch(SwitchGimmick sw)
    {
        targetSwitch = sw;
    }

    private void Awake()
    {
        initialLocalPosition = transform.localPosition;
        // 自身または子オブジェクトからコライダーを取得します
        myCollider = GetComponentInChildren<Collider>();
    }

    private void Update()
    {
        if (targetSwitch == null) return;

        // --- ストッパーの検知 (Physics.OverlapBox) ---
        // OnTriggerStayはRigidbody等の条件が厳しいため、自前で重なり判定を行います
        isBlocked = false;
        if (myCollider != null)
        {
            // コライダーのバウンディングボックスを使って重なっているものを全て取得
            Collider[] overlaps = Physics.OverlapBox(myCollider.bounds.center, myCollider.bounds.extents, myCollider.transform.rotation);
            foreach (var overlap in overlaps)
            {
                if (overlap.GetComponentInParent<SlideStopper>() != null)
                {
                    isBlocked = true;
                    break;
                }
            }
        }

        // --- 移動処理 ---
        bool shouldOpen = targetSwitch.IsPressed;
        float targetDistance = shouldOpen ? slideDistance : 0f;

        // 今回のフレームでの移動後の距離を計算
        float nextDistance = Mathf.MoveTowards(currentDistance, targetDistance, slideSpeed * Time.deltaTime);

        // 前進(開く)しようとしていて、かつストッパーにブロックされている場合は進まない
        // ※戻る(閉じる)場合はブロックされていても戻れるようにする
        if (shouldOpen && isBlocked && nextDistance > currentDistance)
        {
            nextDistance = currentDistance;
        }

        // SE:スライドが開始した瞬間
        // それまで距離が0で、このフレームから動き出す場合に鳴らす
        if (currentDistance == 0f && nextDistance > 0f)
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayOverlap("SFX_gimmick_switchwall");
            }
        }

        currentDistance = nextDistance;
        transform.localPosition = initialLocalPosition + (slideLocalDirection.normalized * currentDistance);
    }
    public void CaptureInitialState()
    {
        // 初期位置のみを一度だけ保存し、ランタイム状態は変更しません。
        if (hasCapturedInitialState)
        {
            return;
        }

        initialLocalPosition = transform.localPosition;
        hasCapturedInitialState = true;
    }

    public void ResetToRespawnState()
    {
        // 死亡リセット時は即時で閉じた状態へ戻します。
        if (!hasCapturedInitialState)
        {
            CaptureInitialState();
        }

        currentDistance = 0f;
        isBlocked = false;
        transform.localPosition = initialLocalPosition;
    }
}