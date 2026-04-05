using UnityEngine;

// 鍵によって作動する壁。指定した KeyManager のすべての鍵が集まると動く。
// 動き方は SlideGimmick（スイッチで動く壁）と同等。
[DisallowMultipleComponent]
public class KeyActivatedWall : MonoBehaviour, IRespawnResettable
{
    [Header("ターゲット鍵マネージャー")]
    [Tooltip("監視対象のKeyManagerを設定します。指定したマネージャーの鍵が全て集まると本ギミックが動作します。")]
    [SerializeField] private KeyManager targetKeyManager;

    [Header("スライド方向（ローカル）")]
    [Tooltip("オブジェクトのローカル空間でのスライド方向。")]
    [SerializeField] private Vector3 slideLocalDirection = Vector3.right;

    [Header("スライド距離（メートル）")]
    [Tooltip("鍵が集まったときに移動する距離。")]
    [SerializeField, Min(0f)] private float slideDistance = 3.0f;

    [Header("スライド速度（m/s）")]
    [Tooltip("目標位置へ向かって移動する速度。")]
    [SerializeField, Min(0.1f)] private float slideSpeed = 2.0f;

    private Vector3 initialLocalPosition;
    private float currentDistance = 0f;
    private bool hasCapturedInitialState;

    private void Awake()
    {
        initialLocalPosition = transform.localPosition;
    }

    private void Update()
    {
        if (targetKeyManager == null) return;

        // --- 移動処理 ---
        bool shouldOpen = targetKeyManager.IsCompleted;
        float targetDistance = shouldOpen ? slideDistance : 0f;

        // 今回のフレームでの移動後の距離を計算
        float nextDistance = Mathf.MoveTowards(currentDistance, targetDistance, slideSpeed * Time.deltaTime);

        // SE:スライドが開始した瞬間
        // それまで距離が0で、このフレームから動き出す場合に鳴らす
        if (currentDistance == 0f && nextDistance > 0f)
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayOverlap("SFX_gimmick_switchwall"); // 扉が開く音
            }
        }

        currentDistance = nextDistance;
        transform.localPosition = initialLocalPosition + (slideLocalDirection.normalized * currentDistance);
    }
    
    // ──────────────────────────────────────────────
    // IRespawnResettable
    // ──────────────────────────────────────────────

    public void CaptureInitialState()
    {
        if (hasCapturedInitialState) return;
        
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
        transform.localPosition = initialLocalPosition;
    }
}
