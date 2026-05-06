using UnityEngine;

public enum MoveDirection
{
    Right,   // 右
    Left,    // 左
    Up,      // 上
    Down,    // 下
    Custom   // カスタム
}

[RequireComponent(typeof(Rigidbody))]
public sealed class HandChaserMovement : MonoBehaviour
{
    [Header("移動速度")]
    [Tooltip("移動速度です。")]
    [SerializeField, Min(0f)] private float moveSpeed = 2.0f;

    [Header("移動方向")]
    [Tooltip("移動方向を選択します。")]
    [SerializeField] private MoveDirection moveDirection = MoveDirection.Right;

    [Header("カスタム移動方向")]
    [Tooltip("MoveDirection が Custom の場合に使用される移動方向です。")]
    [SerializeField] private Vector3 customMoveAxis = Vector3.right;

    private Rigidbody rb;
    private bool isActive;

    public bool IsActive
    {
        get => isActive;
        set => isActive = value;
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

        // カスタム移動軸を正規化
        if (customMoveAxis.sqrMagnitude > 0f)
        {
            customMoveAxis = customMoveAxis.normalized;
        }
    }

    private Vector3 GetMoveAxis()
    {
        switch (moveDirection)
        {
            case MoveDirection.Right:
                return Vector3.right;
            case MoveDirection.Left:
                return Vector3.left;
            case MoveDirection.Up:
                return Vector3.up;
            case MoveDirection.Down:
                return Vector3.down;
            case MoveDirection.Custom:
                return customMoveAxis.normalized;
            default:
                return Vector3.right;
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

        // 固定速度で移動
        Vector3 moveAxis = GetMoveAxis();
        Vector3 before = rb.position;
        Vector3 next = rb.position + moveAxis * (moveSpeed * Time.fixedDeltaTime);

        Debug.Log($"[HandChaserMovement] before={before} next={next} axis={moveAxis} speed={moveSpeed} isKinematic={rb.isKinematic}", this);

        // Rigidbodyを移動
        rb.MovePosition(next);

        Debug.Log($"[HandChaserMovement] after MovePosition rb.position={rb.position} transform.position={transform.position}", this);
    }

    public void SetPlayerTarget(Transform player)
    {
        // プレイヤーの参照は不要になったが、互換性のため空実装を残す
    }
}
