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

    [Header("スイッチ設定: タイプ")]
    [Tooltip("スイッチの挙動タイプ。OneShot は一度押すと戻らず、Continuous は離すと元に戻ります。")]
    [SerializeField] private SwitchType switchType = SwitchType.Continuous;

    [Header("スイッチ設定: 押し込み方向（ローカル）")]
    [Tooltip("スイッチが押し込まれるローカル方向。床タイプなら (0, -1, 0) などを指定してください。")]
    [SerializeField] private Vector3 pushLocalDirection = Vector3.down;

    [Header("スイッチ設定: 最大押し込み深さ")]
    [Tooltip("スイッチが押し込まれる最大深さ（メートル）。この深さと閾値を使って押されたかどうかを判定します。")]
    [SerializeField, Min(0f)] private float pressDepth = 0.2f;

    [Header("スイッチ設定: 押し込み速度（m/s）")]
    [Tooltip("押し込まれる速度（m/s）。値が大きいほど速く押し込まれます。")]
    [SerializeField, Min(0.1f)] private float pressSpeed = 1.0f;

    [Header("スイッチ設定: 戻る速度（m/s）")]
    [Tooltip("スイッチが離れたときに元に戻る速度（m/s）。Continuous タイプ時に影響します。")]
    [SerializeField, Min(0.1f)] private float releaseSpeed = 1.0f;

    [Header("スイッチ設定: 活性化閾値（割合）")]
    [Tooltip("押し込み深さに対する有効化閾値（0〜1）。この割合以上押し込まれると IsPressed が true になります。")]
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