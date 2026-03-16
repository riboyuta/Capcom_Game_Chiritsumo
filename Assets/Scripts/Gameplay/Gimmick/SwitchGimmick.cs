using UnityEngine;

// プレイヤーやオブジェクトが乗る、または押し込むことで起動するスイッチギミック。
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class SwitchGimmick : MonoBehaviour
{
    public enum SwitchType
    {
        OneShot,    // 一度押し込まれたらそのまま戻らない
        Continuous  // 離れると元に戻る
    }

    [Header("Switch Settings")]
    [SerializeField] private SwitchType switchType = SwitchType.Continuous;
    
    // スイッチが押し込まれるローカル方向 (床なら (0, -1, 0) など)
    [SerializeField] private Vector3 pushLocalDirection = Vector3.down;
    
    // 押し込まれる最大深さ
    [SerializeField, Min(0f)] private float pressDepth = 0.2f;
    
    // 押し込まれる速度 (m/s)
    [SerializeField, Min(0.1f)] private float pressSpeed = 1.0f;
    
    // 元に戻る速度 (m/s)
    [SerializeField, Min(0.1f)] private float releaseSpeed = 1.0f;

    // スイッチが完全に押し込まれているか判定する閾値 (割合)
    [SerializeField, Range(0f, 1f)] private float activateThreshold = 0.9f;

    private Vector3 initialLocalPosition;
    private float currentPressDistance = 0f;
    private bool isPushedThisFrame = false;

    // 外部からスイッチがオンになっているか確認するためのプロパティ
    public bool IsPressed { get; private set; }

    private void Awake()
    {
        initialLocalPosition = transform.localPosition;
    }

    private void OnTriggerStay(Collider other)
    {
        TryPush(other.attachedRigidbody);
    }

    private void OnCollisionStay(Collision collision)
    {
        TryPush(collision.rigidbody);
    }

    private void TryPush(Rigidbody targetRb)
    {
        // 物理挙動を持つオブジェクトのみ反応する
        if (targetRb == null) return;
        
        isPushedThisFrame = true;
    }

    private void FixedUpdate()
    {
        // FixedUpdate内でRigidbodyの衝突を処理することが多いため
        // 状態更新もFixedUpdateで行うかUpdateで行うか分かれますが、
        // isPushedThisFrame のリセットタイミングを考慮し、
        // Update と FixedUpdate の両方で処理できるようにします。
        
        // ※ここではUpdateでTransform更新を行っています
    }

    private void Update()
    {
        // 押し込まれている場合 or OneShotで既に完全に押し込まれている場合
        if (isPushedThisFrame || (switchType == SwitchType.OneShot && IsPressed))
        {
            currentPressDistance += pressSpeed * Time.deltaTime;
        }
        else
        {
            // 押されていない場合は元に戻る (Continuousのみ)
            if (switchType == SwitchType.Continuous)
            {
                currentPressDistance -= releaseSpeed * Time.deltaTime;
            }
        }

        // 深さをクランプ
        currentPressDistance = Mathf.Clamp(currentPressDistance, 0f, pressDepth);

        // 状態を更新
        IsPressed = (currentPressDistance >= pressDepth * activateThreshold);

        // Transformへ反映
        transform.localPosition = initialLocalPosition + (pushLocalDirection.normalized * currentPressDistance);

        // フラグをリセット (次フレームの OnTriggerStay 等で再度設定される想定)
        isPushedThisFrame = false;
    }
}
