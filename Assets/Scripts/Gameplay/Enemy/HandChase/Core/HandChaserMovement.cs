using UnityEngine;

/// <summary>
/// HandChaser敵の移動を制御するコンポーネント。
/// プレイヤーとの距離に応じた速度調整、攻撃中の速度減衰などを処理する。
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public sealed class HandChaserMovement : MonoBehaviour
{
    [Header("基本移動")]
    [Tooltip("基準となる移動速度です。")]
    [SerializeField, Min(0f)] private float moveSpeed = 2.0f;

    [Tooltip("移動方向です。正規化されます。")]
    [SerializeField] private Vector3 moveAxis = Vector3.right;

    [Header("距離追従")]
    [Tooltip("true のとき、プレイヤーとの距離に応じて移動速度を変化させます。")]
    [SerializeField] private bool useDistanceBasedSpeed = true;

    [Tooltip("プレイヤーに十分近いときの最低移動速度です。")]
    [SerializeField, Min(0f)] private float minMoveSpeed = 1.5f;

    [Tooltip("プレイヤーから十分離れているときの最大移動速度です。")]
    [SerializeField, Min(0f)] private float maxMoveSpeed = 4.5f;

    [Tooltip("この X 距離以上で最大移動速度になります。")]
    [SerializeField, Min(0.01f)] private float distanceForMaxSpeed = 10.0f;

    [Tooltip("この X 距離以下では最低移動速度のままにします。")]
    [SerializeField, Min(0f)] private float distanceForMinSpeed = 1.5f;

    [Tooltip("攻撃中に本体移動速度を落とす倍率です。1=通常、0=停止。")]
    [SerializeField, Range(0f, 1f)] private float speedMultiplierWhileAttacking = 0.5f;

    [Header("デバッグ表示")]
    [Tooltip("移動速度範囲をGizmoで表示します。")]
    [SerializeField] private bool showSpeedRangeGizmos = true;

    private Rigidbody rb;
    private Transform playerTransform;
    private bool isAttacking;
    private bool isActive;

    public bool IsActive
    {
        get => isActive;
        set => isActive = value;
    }

    public bool IsAttacking
    {
        get => isAttacking;
        set => isAttacking = value;
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }
    }

    private void OnValidate()
    {
        moveSpeed = Mathf.Max(0f, moveSpeed);
        minMoveSpeed = Mathf.Max(0f, minMoveSpeed);
        maxMoveSpeed = Mathf.Max(0f, maxMoveSpeed);
        distanceForMaxSpeed = Mathf.Max(0.01f, distanceForMaxSpeed);
        distanceForMinSpeed = Mathf.Max(0f, distanceForMinSpeed);
        speedMultiplierWhileAttacking = Mathf.Clamp01(speedMultiplierWhileAttacking);

        if (maxMoveSpeed < minMoveSpeed)
        {
            maxMoveSpeed = minMoveSpeed;
        }

        if (distanceForMinSpeed > distanceForMaxSpeed)
        {
            distanceForMinSpeed = distanceForMaxSpeed;
        }

        if (moveAxis.sqrMagnitude > 0f)
        {
            moveAxis = moveAxis.normalized;
        }
    }

    private void FixedUpdate()
    {
        Debug.Log($"[HandChaserMovement] FixedUpdate enabled={enabled} isActive={isActive}", this);

        if (!isActive)
        {
            Debug.Log("[HandChaserMovement] isActive false", this);
            return;
        }

        if (rb == null)
        {
            Debug.LogError("[HandChaserMovement] rb is null", this);
            return;
        }

        float currentSpeed = GetCurrentMoveSpeed();
        Vector3 before = rb.position;
        Vector3 next = rb.position + moveAxis * (currentSpeed * Time.fixedDeltaTime);

        Debug.Log($"[HandChaserMovement] before={before} next={next} axis={moveAxis} speed={currentSpeed} isKinematic={rb.isKinematic}", this);

        rb.MovePosition(next);

        Debug.Log($"[HandChaserMovement] after MovePosition rb.position={rb.position} transform.position={transform.position}", this);
    }

    public void SetPlayerTarget(Transform player)
    {
        playerTransform = player;
    }

    public float GetCurrentMoveSpeed()
    {
        float baseSpeed = CalculateBaseSpeed();

        if (isAttacking)
        {
            baseSpeed *= speedMultiplierWhileAttacking;
        }

        return baseSpeed;
    }

    private float CalculateBaseSpeed()
    {
        if (!useDistanceBasedSpeed || playerTransform == null)
        {
            return moveSpeed;
        }

        float dx = Mathf.Abs(playerTransform.position.x - transform.position.x);

        if (dx <= distanceForMinSpeed)
        {
            return minMoveSpeed;
        }

        float t = Mathf.InverseLerp(distanceForMinSpeed, distanceForMaxSpeed, dx);
        return Mathf.Lerp(minMoveSpeed, maxMoveSpeed, t);
    }

    private void OnDrawGizmosSelected()
    {
        if (!showSpeedRangeGizmos)
        {
            return;
        }

        Vector3 origin = transform.position;

        float minDist = Mathf.Max(0f, distanceForMinSpeed);
        float maxDist = Mathf.Max(minDist, distanceForMaxSpeed);

        float yOffset = 0.25f;
        Vector3 leftMin = origin + Vector3.left * minDist + Vector3.up * yOffset;
        Vector3 rightMin = origin + Vector3.right * minDist + Vector3.up * yOffset;

        Vector3 leftMax = origin + Vector3.left * maxDist + Vector3.up * yOffset;
        Vector3 rightMax = origin + Vector3.right * maxDist + Vector3.up * yOffset;

        Vector3 lineTopOffset = Vector3.up * 1.5f;
        Vector3 lineBottomOffset = Vector3.down * 1.5f;

        // 最低速度範囲
        Gizmos.color = Color.green;
        Gizmos.DrawLine(leftMin, rightMin);
        Gizmos.DrawLine(leftMin + lineBottomOffset, leftMin + lineTopOffset);
        Gizmos.DrawLine(rightMin + lineBottomOffset, rightMin + lineTopOffset);

        // 最大速度到達範囲
        Gizmos.color = Color.red;
        Gizmos.DrawLine(leftMax, rightMax);
        Gizmos.DrawLine(leftMax + lineBottomOffset, leftMax + lineTopOffset);
        Gizmos.DrawLine(rightMax + lineBottomOffset, rightMax + lineTopOffset);

        // 中間補間エリア
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(leftMin, leftMax);
        Gizmos.DrawLine(rightMin, rightMax);
    }
}
