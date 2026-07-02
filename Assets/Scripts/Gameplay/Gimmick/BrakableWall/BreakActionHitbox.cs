using System;
using UnityEngine;

public enum BreakActionType
{
    PlayerDash,
    SonarChargerCharge,
    ShadowChaserDash
}

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public sealed class BreakActionHitbox : MonoBehaviour
{
    [Header("破壊判定設定")]
    [Tooltip("この判定が表す破壊行動です。")]
    [SerializeField] private BreakActionType actionType = BreakActionType.PlayerDash;

    [Tooltip("壁に与える破壊力です。通常は1で問題ありません。")]
    [SerializeField] private int power = 1;

    [Header("参照")]
    [Tooltip("破壊判定に使うTriggerコライダーです。未設定時は同じGameObjectから取得します。")]
    [SerializeField] private Collider hitboxCollider;

    [Header("初期状態")]
    [Tooltip("開始時に破壊判定を無効化するかです。通常は有効にします。")]
    [SerializeField] private bool disableOnAwake = true;

    [Header("重なり検出補助")]
    [Tooltip("有効にすると、Triggerイベントに頼らず毎フレーム重なっている BreakableWall を直接検出します。")]
    [SerializeField] private bool useManualOverlapCheck = true;

    [Tooltip("手動重なり検出で対象にするLayerです。基本は壁が含まれるLayerを指定します。")]
    [SerializeField] private LayerMask manualOverlapLayerMask = ~0;

    [Tooltip("手動重なり検出の最大取得数です。")]
    [SerializeField] private int manualOverlapBufferSize = 16;

    [Tooltip("Transformで動く対象の判定漏れを減らすため、Overlap前に Physics.SyncTransforms() を呼びます。")]
    [SerializeField] private bool syncTransformsBeforeManualOverlap = true;

    private Collider[] manualOverlapBuffer;
    private int activationSerial;

    public BreakActionType ActionType => actionType;
    public int Power => Mathf.Max(1, power);
    public bool IsHitboxEnabled => hitboxCollider != null && hitboxCollider.enabled;
    public Collider HitboxCollider => hitboxCollider;
    public int CurrentHitKey => unchecked((GetInstanceID() * 397) ^ activationSerial);

    public event Action<BreakableWall, Vector3, Vector3> WallBroken;

    private void Awake()
    {
        ResolveReferences();

        if (hitboxCollider != null)
        {
            hitboxCollider.isTrigger = true;

            if (disableOnAwake)
            {
                hitboxCollider.enabled = false;
            }
        }
    }

    private void LateUpdate()
    {
        if (!useManualOverlapCheck)
        {
            return;
        }

        CheckManualOverlap();
    }

    private void OnValidate()
    {
        power = Mathf.Max(1, power);
        manualOverlapBufferSize = Mathf.Max(1, manualOverlapBufferSize);

        ResolveReferences();

        if (hitboxCollider != null)
        {
            hitboxCollider.isTrigger = true;
        }
    }

    public void SetHitboxEnabled(bool enabled)
    {
        if (hitboxCollider == null)
        {
            ResolveReferences();
        }

        if (hitboxCollider == null)
        {
            return;
        }

        if (hitboxCollider.enabled == enabled)
        {
            return;
        }

        hitboxCollider.enabled = enabled;

        if (enabled)
        {
            activationSerial++;
        }
    }

    public void NotifyWallBroken(BreakableWall wall, Vector3 hitPoint, Vector3 reboundDirection)
    {
        if (wall == null)
        {
            return;
        }

        reboundDirection.z = 0.0f;

        if (reboundDirection.sqrMagnitude <= 0.0001f)
        {
            reboundDirection = -transform.right;
            reboundDirection.z = 0.0f;
        }

        reboundDirection.Normalize();

        WallBroken?.Invoke(wall, hitPoint, reboundDirection);
    }

    private void ResolveReferences()
    {
        if (hitboxCollider == null)
        {
            hitboxCollider = GetComponent<Collider>();
        }
    }

    private void CheckManualOverlap()
    {
        if (hitboxCollider == null)
        {
            return;
        }

        if (!hitboxCollider.enabled)
        {
            return;
        }

        if (manualOverlapBuffer == null || manualOverlapBuffer.Length != manualOverlapBufferSize)
        {
            manualOverlapBuffer = new Collider[manualOverlapBufferSize];
        }

        if (syncTransformsBeforeManualOverlap)
        {
            Physics.SyncTransforms();
        }

        Bounds bounds = hitboxCollider.bounds;

        int hitCount = Physics.OverlapBoxNonAlloc(
            bounds.center,
            bounds.extents,
            manualOverlapBuffer,
            Quaternion.identity,
            manualOverlapLayerMask,
            QueryTriggerInteraction.Collide);

        for (int i = 0; i < hitCount; i++)
        {
            Collider overlap = manualOverlapBuffer[i];

            if (overlap == null)
            {
                continue;
            }

            if (overlap == hitboxCollider)
            {
                continue;
            }

            BreakableWall wall = overlap.GetComponentInParent<BreakableWall>();
            if (wall == null)
            {
                continue;
            }

            Vector3 hitPoint = hitboxCollider.ClosestPoint(wall.transform.position);
            wall.TryBreak(this, hitPoint);
        }
    }
}