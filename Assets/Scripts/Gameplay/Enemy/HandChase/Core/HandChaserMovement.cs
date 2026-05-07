using System;
using UnityEngine;

public enum MoveDirection
{
    Right,   // 右
    Left,    // 左
    Up,      // 上
    Down,    // 下
    Custom   // カスタム
}

[Serializable]
public struct HandChaserMovementSettings
{
    [Tooltip("移動速度です。")]
    [Min(0f)] public float moveSpeed;

    [Tooltip("移動方向を選択します。")]
    public MoveDirection moveDirection;

    [Tooltip("MoveDirection が Custom の場合に使用される移動方向です。")]
    public Vector3 customMoveAxis;

    public static HandChaserMovementSettings Default => new HandChaserMovementSettings
    {
        moveSpeed = 2.0f,
        moveDirection = MoveDirection.Right,
        customMoveAxis = Vector3.right
    };
}

[RequireComponent(typeof(Rigidbody))]
public sealed class HandChaserMovement : MonoBehaviour
{
    private Rigidbody rb;
    private bool isActive;

    // 現在の設定
    private float moveSpeed = 2.0f;
    private MoveDirection moveDirection = MoveDirection.Right;
    private Vector3 customMoveAxis = Vector3.right;

    // 初期状態のキャッシュ（リセット用）
    private float initialMoveSpeed;
    private MoveDirection initialMoveDirection;
    private Vector3 initialCustomMoveAxis;
    private bool hasCapturedInitialState;

    public bool IsActive
    {
        get => isActive;
        set => isActive = value;
    }

    public float MoveSpeed
    {
        get => moveSpeed;
        set => moveSpeed = Mathf.Max(0f, value);
    }

    public MoveDirection Direction
    {
        get => moveDirection;
        set => moveDirection = value;
    }

    public Vector3 CustomMoveAxis
    {
        get => customMoveAxis;
        set => customMoveAxis = value.sqrMagnitude > 0f ? value.normalized : Vector3.right;
    }

    // 外部から設定を適用する
    public void ApplySettings(HandChaserMovementSettings settings)
    {
        moveSpeed = Mathf.Max(0f, settings.moveSpeed);
        moveDirection = settings.moveDirection;
        customMoveAxis = settings.customMoveAxis.sqrMagnitude > 0f 
            ? settings.customMoveAxis.normalized 
            : Vector3.right;
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

        // 初期状態をキャッシュ
        CaptureInitialState();
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

    // 現在の設定を初期状態としてキャッシュ
    public void CaptureInitialState()
    {
        if (hasCapturedInitialState)
            return;

        initialMoveSpeed = moveSpeed;
        initialMoveDirection = moveDirection;
        initialCustomMoveAxis = customMoveAxis;
        hasCapturedInitialState = true;
    }

    // 初期状態にリセット
    public void ResetToInitialState()
    {
        moveSpeed = initialMoveSpeed;
        moveDirection = initialMoveDirection;
        customMoveAxis = initialCustomMoveAxis;
        isActive = false;
    }
}
