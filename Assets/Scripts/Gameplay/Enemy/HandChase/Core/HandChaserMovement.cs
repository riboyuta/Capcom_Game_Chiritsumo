using UnityEngine;

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
        // Rigidbodyを取得してKinematicに設定
        rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;  // MovePositionで移動するためKinematic
            rb.useGravity = false;  // 重力は使わない
        }
    }

    private void OnValidate()
    {
        // 各パラメータを有効な範囲に制限
        moveSpeed = Mathf.Max(0f, moveSpeed);
        minMoveSpeed = Mathf.Max(0f, minMoveSpeed);
        maxMoveSpeed = Mathf.Max(0f, maxMoveSpeed);
        distanceForMaxSpeed = Mathf.Max(0.01f, distanceForMaxSpeed);
        distanceForMinSpeed = Mathf.Max(0f, distanceForMinSpeed);
        speedMultiplierWhileAttacking = Mathf.Clamp01(speedMultiplierWhileAttacking);

        // 最大速度が最小速度より小さい場合は修正
        if (maxMoveSpeed < minMoveSpeed)
        {
            maxMoveSpeed = minMoveSpeed;
        }

        // 最小距離が最大距離より大きい場合は修正
        if (distanceForMinSpeed > distanceForMaxSpeed)
        {
            distanceForMinSpeed = distanceForMaxSpeed;
        }

        // 移動軸を正規化
        if (moveAxis.sqrMagnitude > 0f)
        {
            moveAxis = moveAxis.normalized;
        }
    }

    private void FixedUpdate()
    {
        Debug.Log($"[HandChaserMovement] FixedUpdate enabled={enabled} isActive={isActive}", this);

        // 非アクティブなら移動しない
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

        // 現在の移動速度を計算
        float currentSpeed = GetCurrentMoveSpeed();
        Vector3 before = rb.position;
        Vector3 next = rb.position + moveAxis * (currentSpeed * Time.fixedDeltaTime);

        Debug.Log($"[HandChaserMovement] before={before} next={next} axis={moveAxis} speed={currentSpeed} isKinematic={rb.isKinematic}", this);

        // Rigidbodyを移動
        rb.MovePosition(next);

        Debug.Log($"[HandChaserMovement] after MovePosition rb.position={rb.position} transform.position={transform.position}", this);
    }

    public void SetPlayerTarget(Transform player)
    {
        // プレイヤーの参照を保持（距離ベースの速度調整に使用）
        playerTransform = player;
    }

    // 現在の移動速度を取得（攻撃中の減速も考慮）
    public float GetCurrentMoveSpeed()
    {
        float baseSpeed = CalculateBaseSpeed();

        // 攻撃中は移動速度を下げる
        if (isAttacking)
        {
            baseSpeed *= speedMultiplierWhileAttacking;
        }

        return baseSpeed;
    }

    // プレイヤーとの距離に応じた基本移動速度を計算
    private float CalculateBaseSpeed()
    {
        // 距離ベースの速度調整が無効、またはプレイヤーがいない場合は固定速度
        if (!useDistanceBasedSpeed || playerTransform == null)
        {
            return moveSpeed;
        }

        // X軸での距離を計算
        float dx = Mathf.Abs(playerTransform.position.x - transform.position.x);

        // 最小距離以下なら最低速度
        if (dx <= distanceForMinSpeed)
        {
            return minMoveSpeed;
        }

        // 距離に応じて速度を補間
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
