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
    [Tooltip("移動速度です。（変速機能 OFF 時に使用）")]
    [Min(0f)] public float moveSpeed;

    [Tooltip("移動方向を選択します。")]
    public MoveDirection moveDirection;

    [Tooltip("MoveDirection が Custom の場合に使用される移動方向です。")]
    public Vector3 customMoveAxis;

    public static HandChaserMovementSettings Default => new HandChaserMovementSettings
    {
        moveSpeed = 9.0f,
        moveDirection = MoveDirection.Right,
        customMoveAxis = Vector3.right
    };
}

[RequireComponent(typeof(Rigidbody))]
public sealed class HandChaserMovement : MonoBehaviour
{
    private Rigidbody rb;
    private bool isActive;

    // 変速 OFF 時に使う設定
    private float moveSpeed = 9.0f;
    private MoveDirection moveDirection = MoveDirection.Right;
    private Vector3 customMoveAxis = Vector3.right;

    // 初期状態のキャッシュ（リセット用）
    private float initialMoveSpeed;
    private MoveDirection initialMoveDirection;
    private Vector3 initialCustomMoveAxis;
    private bool hasCapturedInitialState;

    // ──────────────────────────────────────────
    // 変速機能
    // ──────────────────────────────────────────

    [Header("変速機能")]
    [Tooltip("プレイヤーとの距離に応じて速度を変化させるかどうかです。false にすると等速になります。")]
    [SerializeField] private bool enableAdaptiveSpeed = true;

    [Header("各ゾーンの速度 (m/s)")]
    [Tooltip("近すぎる時の速度です（壁がプレイヤーに接近しすぎた場合）。")]
    [SerializeField] private float nearSpeed = 7f;

    [Tooltip("理想距離にいる時の速度です。")]
    [SerializeField] private float idealSpeed = 9f;

    [Tooltip("遠すぎる時の速度です（壁がプレイヤーから大きく離れた場合）。")]
    [SerializeField] private float farSpeed = 12f;

    [Header("距離ゾーン境界 (m)")]
    [Tooltip(
        "この距離より後ろにいる → 「近すぎ」扱い。nearSpeed を適用。\n" +
        "（オフセット = 移動軸方向のプレイヤーと壁の距離）")]
    [SerializeField] private float nearThreshold = 4f;

    [Tooltip("理想距離の近側の境界です。nearThreshold ～ idealMinDistance の間は nearSpeed と idealSpeed を補間します。")]
    [SerializeField] private float idealMinDistance = 6f;

    [Tooltip("理想距離の遠側の境界です。idealMinDistance ～ idealMaxDistance の間は idealSpeed を維持します。")]
    [SerializeField] private float idealMaxDistance = 8f;

    [Tooltip("この距離より遠い → 「遠すぎ」扱い。farSpeed を適用。\nidealMaxDistance ～ farThreshold の間は idealSpeed と farSpeed を補間します。")]
    [SerializeField] private float farThreshold = 12f;

    [Header("速度変化スムーズ")]
    [Tooltip("速度変化を SmoothDamp で滑らかにするスムーズ時間です。小さいほど速度変化が鋭くなります。")]
    [SerializeField] private float speedSmoothTime = 0.15f;

    // 変速ロジック用の内部変数
    private Transform playerTarget;
    private float currentSpeed;
    private float speedVelocity;
    private bool currentSpeedInitialized;

    // ──────────────────────────────────────────
    // プロパティ
    // ──────────────────────────────────────────

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

    // 現在の実効速度（デバッグ・外部参照用）
    public float CurrentSpeed => currentSpeed;

    // ──────────────────────────────────────────
    // 外部 API
    // ──────────────────────────────────────────

    // 外部から設定を適用する（Room.cs から呼ばれる）
    public void ApplySettings(HandChaserMovementSettings settings)
    {
        ApplyMovementSettings(settings, true);
    }

    public void ApplySettings(
        HandChaserMovementSettings movementSettings,
        HandChaserAdaptiveSpeedSettings adaptiveSpeedSettings)
    {
        ApplyMovementSettings(movementSettings, false);
        ApplyAdaptiveSpeedSettings(adaptiveSpeedSettings, false);

        // RoomEnemySystem から適用された値を、この敵の初期値として扱う。
        StoreCurrentSettingsAsInitial();
    }

    public void ApplyMovementSettings(HandChaserMovementSettings settings, bool updateInitialState)
    {
        moveSpeed = Mathf.Max(0f, settings.moveSpeed);
        moveDirection = settings.moveDirection;
        customMoveAxis = settings.customMoveAxis.sqrMagnitude > 0f
            ? settings.customMoveAxis.normalized
            : Vector3.right;

        ResetSpeedSmoothing();

        if (updateInitialState)
        {
            StoreCurrentSettingsAsInitial();
        }
    }

    public void ApplyAdaptiveSpeedSettings(HandChaserAdaptiveSpeedSettings settings, bool updateInitialState)
    {
        enableAdaptiveSpeed = settings.enableAdaptiveSpeed;

        nearSpeed = Mathf.Max(0f, settings.nearSpeed);
        idealSpeed = Mathf.Max(0f, settings.idealSpeed);
        farSpeed = Mathf.Max(0f, settings.farSpeed);

        nearThreshold = Mathf.Max(0f, settings.nearThreshold);
        idealMinDistance = Mathf.Max(nearThreshold, settings.idealMinDistance);
        idealMaxDistance = Mathf.Max(idealMinDistance, settings.idealMaxDistance);
        farThreshold = Mathf.Max(idealMaxDistance, settings.farThreshold);

        speedSmoothTime = Mathf.Max(0.001f, settings.speedSmoothTime);

        ResetSpeedSmoothing();

        if (updateInitialState)
        {
            StoreCurrentSettingsAsInitial();
        }
    }

    // プレイヤーの Transform を設定する（HandChaserEnemy から呼ばれる）
    public void SetPlayerTarget(Transform player)
    {
        playerTarget = player;
    }

    // ──────────────────────────────────────────
    // Unity ライフサイクル
    // ──────────────────────────────────────────

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity  = false;
        }

        CaptureInitialState();
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

        float effectiveSpeed = ComputeEffectiveSpeed();

        Vector3 moveAxis = GetMoveAxis();
        Vector3 before   = rb.position;
        Vector3 next     = rb.position + moveAxis * (effectiveSpeed * Time.fixedDeltaTime);

        Debug.Log($"[HandChaserMovement] before={before} next={next} axis={moveAxis} speed={effectiveSpeed:F2} isKinematic={rb.isKinematic}", this);

        rb.MovePosition(next);

        //Debug.Log($"[HandChaserMovement] after MovePosition rb.position={rb.position} transform.position={transform.position}", this);
    }

    // ──────────────────────────────────────────
    // 変速計算
    // ──────────────────────────────────────────

    // プレイヤーとの距離に応じて実効速度を計算する。
    // 変速 OFF またはプレイヤー未設定の場合は moveSpeed を返す。
    private float ComputeEffectiveSpeed()
    {
        float targetSpeed = GetTargetSpeed();

        // 初回は即座に合わせる（SmoothDamp の立ち上がりガタつき防止）
        if (!currentSpeedInitialized)
        {
            currentSpeed          = targetSpeed;
            speedVelocity         = 0f;
            currentSpeedInitialized = true;
            return currentSpeed;
        }

        float smoothTime = speedSmoothTime > 0f ? speedSmoothTime : 0.001f;
        currentSpeed = Mathf.SmoothDamp(currentSpeed, targetSpeed, ref speedVelocity, smoothTime);
        return currentSpeed;
    }

    // オフセット距離からゾーン判定して目標速度を返す。
    private float GetTargetSpeed()
    {
        if (!enableAdaptiveSpeed || playerTarget == null)
        {
            return moveSpeed;
        }

        // 移動軸方向のオフセット（正 = 壁がプレイヤーの後ろ、負 = 行き過ぎ）
        Vector3 moveAxis = GetMoveAxis();
        float offset = Vector3.Dot(playerTarget.position - rb.position, moveAxis);

        // ゾーン判定
        if (offset <= nearThreshold)
        {
            // 近すぎ（または壁が先行している）→ 最低速
            return nearSpeed;
        }

        if (offset <= idealMinDistance)
        {
            // nearThreshold ～ idealMinDistance: 近すぎ速度 → 理想速度 へ線形補間
            float t = Mathf.InverseLerp(nearThreshold, idealMinDistance, offset);
            return Mathf.Lerp(nearSpeed, idealSpeed, t);
        }

        if (offset <= idealMaxDistance)
        {
            // 理想距離範囲内 → 理想速度を維持
            return idealSpeed;
        }

        if (offset <= farThreshold)
        {
            // idealMaxDistance ～ farThreshold: 理想速度 → 最大速度 へ線形補間
            float t = Mathf.InverseLerp(idealMaxDistance, farThreshold, offset);
            return Mathf.Lerp(idealSpeed, farSpeed, t);
        }

        // 遠すぎ → 最大速度
        return farSpeed;
    }

    // ──────────────────────────────────────────
    // 内部ユーティリティ
    // ──────────────────────────────────────────

    private Vector3 GetMoveAxis()
    {
        switch (moveDirection)
        {
            case MoveDirection.Right:  return Vector3.right;
            case MoveDirection.Left:   return Vector3.left;
            case MoveDirection.Up:     return Vector3.up;
            case MoveDirection.Down:   return Vector3.down;
            case MoveDirection.Custom: return customMoveAxis.normalized;
            default:                   return Vector3.right;
        }
    }

    // ──────────────────────────────────────────
    // 初期状態キャプチャ / リセット
    // ──────────────────────────────────────────

    public void CaptureInitialState()
    {
        if (hasCapturedInitialState)
        {
            return;
        }

        StoreCurrentSettingsAsInitial();
        hasCapturedInitialState = true;
    }

    public void ResetToInitialState()
    {
        moveSpeed      = initialMoveSpeed;
        moveDirection  = initialMoveDirection;
        customMoveAxis = initialCustomMoveAxis;
        isActive       = false;

        // 変速の内部状態もリセット
        currentSpeed            = 0f;
        speedVelocity           = 0f;
        currentSpeedInitialized = false;
    }

    private void StoreCurrentSettingsAsInitial()
    {
        initialMoveSpeed = moveSpeed;
        initialMoveDirection = moveDirection;
        initialCustomMoveAxis = customMoveAxis;
    }

    private void ResetSpeedSmoothing()
    {
        currentSpeed = 0f;
        speedVelocity = 0f;
        currentSpeedInitialized = false;
    }
}
