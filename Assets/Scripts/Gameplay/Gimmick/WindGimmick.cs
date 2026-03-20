using UnityEngine;

// プレイヤー（および Rigidbody を持つオブジェクト）が範囲内に入っている間、
// 指定した方向へ継続的に加速させる風オブジェクト（ギミック）。
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public sealed class WindGimmick : MonoBehaviour
{
    [Header("風: 方向（ローカル）")]
    [Tooltip("風の向きをローカル空間で指定します。例：(0,1,0) はオブジェクトの上方向へ風が吹きます。")]
    [SerializeField] private Vector3 localWindDirection = Vector3.up;

    [Header("風: 加速度（m/s^2）")]
    [Tooltip("風によって毎秒増加する速度（m/s^2）。値を大きくすると短時間で速度が上がります。")]
    [SerializeField, Min(0f)] private float windAcceleration = 15f;

    [Header("風: 最大速度（m/s）")]
    [Tooltip("風の方向への速度上限（m/s）。この速度を超えないように加速が制限されます。")]
    [SerializeField, Min(0f)] private float maxWindSpeed = 10f;

    [Header("風エリア離脱時の速度維持率")]
    [Tooltip("エリアから出た瞬間の風方向の速度維持率（0で即消滅、1でそのまま飛ぶ）。脱出時に飛びすぎるのを防ぎます。")]
    [SerializeField, Range(0f, 1f)] private float exitVelocityMultiplier = 0.5f;

    private void OnTriggerStay(Collider other)
    {
        TryApplyWind(other.attachedRigidbody);
    }

    private void TryApplyWind(Rigidbody targetRb)
    {
        // 物理挙動を持つオブジェクト（Rigidbody）のみ反応する
        if (targetRb == null)
        {
            return;
        }

        // ローカル方向ベクトルをワールド空間の方向に変換し、正規化する
        Vector3 windDirWorld = transform.TransformDirection(localWindDirection).normalized;

        // 万が一方向がゼロベクトルの場合は処理しない
        if (windDirWorld == Vector3.zero) return;

        // 現在の速度を取得
        Vector3 velocity = targetRb.linearVelocity;

        // 風の方向への現在の速度成分（内積）を計算
        float currentSpeedInWindDir = Vector3.Dot(velocity, windDirWorld);

        // 指定した最大速度に達していない場合のみ、加速を適用する
        if (currentSpeedInWindDir < maxWindSpeed)
        {
            // 1物理フレームで加算するべき速度
            float speedToAdd = windAcceleration * Time.fixedDeltaTime;

            // 超過しないようにクランプ後の速度成分を計算
            float newSpeedInWindDir = Mathf.Min(currentSpeedInWindDir + speedToAdd, maxWindSpeed);

            // 速度の差分をベクトルとして加算
            float speedDiff = newSpeedInWindDir - currentSpeedInWindDir;
            velocity += windDirWorld * speedDiff;

            // 更新した速度を適用
            targetRb.linearVelocity = velocity;
        }
        else if (currentSpeedInWindDir > maxWindSpeed)
        {
            // ジャンプなどで風の最大速度を上回って突入した場合、強制的に風の最大速度まで押し下げる
            // これにより、風に乗った状態で速度が上がりすぎることを防ぐ
            float speedDiff = maxWindSpeed - currentSpeedInWindDir;
            velocity += windDirWorld * speedDiff;
            targetRb.linearVelocity = velocity;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        Rigidbody targetRb = other.attachedRigidbody;
        if (targetRb == null) return;

        Vector3 windDirWorld = transform.TransformDirection(localWindDirection).normalized;
        if (windDirWorld == Vector3.zero) return;

        Vector3 velocity = targetRb.linearVelocity;
        float currentSpeedInWindDir = Vector3.Dot(velocity, windDirWorld);

        // エリアから出た際、風の方向への勢いを適度に抑える
        if (currentSpeedInWindDir > 0)
        {
            float exitSpeed = currentSpeedInWindDir * exitVelocityMultiplier;
            float speedDiff = exitSpeed - currentSpeedInWindDir;
            velocity += windDirWorld * speedDiff;
            targetRb.linearVelocity = velocity;
        }
    }
}