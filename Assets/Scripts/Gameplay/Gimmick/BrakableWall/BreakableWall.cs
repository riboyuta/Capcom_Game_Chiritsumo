using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BreakableWall : MonoBehaviour, IRespawnResettable
{
    [Header("破壊条件")]
    [Tooltip("この壁を壊せる行動です。")]
    [SerializeField]
    private List<BreakActionType> breakableBy = new()
    {
        BreakActionType.PlayerDash
    };

    [Tooltip("壊れるまでに必要な破壊力です。1なら1回で壊れます。")]
    [SerializeField] private int durability = 1;

    [Header("当たり判定")]
    [Tooltip("プレイヤーや敵を通さない通常の壁コライダーです。")]
    [SerializeField] private Collider solidCollider;

    [Tooltip("破壊判定を受け取るTriggerコライダーです。通常の壁より少し大きめにします。")]
    [SerializeField] private Collider breakTriggerCollider;

    [Header("表示")]
    [Tooltip("壊れた時に非表示にするRendererです。未設定なら子階層から自動取得します。")]
    [SerializeField] private Renderer[] targetRenderers;

    [Header("演出")]
    [Tooltip("破壊時に生成するエフェクトです。")]
    [SerializeField] private GameObject breakEffectPrefab;

    [Tooltip("破壊音を鳴らすAudioSourceです。")]
    [SerializeField] private AudioSource audioSource;

    [Tooltip("破壊時に再生するSEです。")]
    [SerializeField] private AudioClip breakSound;

    [Header("デバッグ")]
    [Tooltip("有効にすると破壊判定のログを出します。")]
    [SerializeField] private bool enableDebugLog = false;

    private int currentDurability;
    private bool isBroken;

    private Vector3 initialPosition;
    private Quaternion initialRotation;
    private Vector3 initialScale;

    private bool initialSolidColliderEnabled;
    private bool initialBreakTriggerColliderEnabled;
    private bool[] initialRendererEnabledStates;

    private bool hasCapturedInitialState;

    private readonly HashSet<int> consumedHitboxIds = new();

    private void Awake()
    {
        ResolveReferences();
        currentDurability = Mathf.Max(1, durability);

        if (breakTriggerCollider != null)
        {
            breakTriggerCollider.isTrigger = true;
        }
    }

    private void OnValidate()
    {
        durability = Mathf.Max(1, durability);

        ResolveReferences();

        if (breakTriggerCollider != null)
        {
            breakTriggerCollider.isTrigger = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        TryBreakFromCollider(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TryBreakFromCollider(other);
    }

    private void OnTriggerExit(Collider other)
    {
        if (other == null)
        {
            return;
        }

        BreakActionHitbox hitbox = other.GetComponentInParent<BreakActionHitbox>();
        if (hitbox == null)
        {
            return;
        }

        consumedHitboxIds.Remove(hitbox.GetInstanceID());
    }

    private void TryBreakFromCollider(Collider other)
    {
        if (other == null)
        {
            return;
        }

        BreakActionHitbox hitbox = other.GetComponentInParent<BreakActionHitbox>();
        if (hitbox == null)
        {
            return;
        }

        Vector3 hitPoint = other.ClosestPoint(transform.position);
        TryBreak(hitbox, hitPoint);
    }

    public void TryBreak(BreakActionHitbox hitbox, Vector3 hitPoint)
    {
        if (isBroken)
        {
            return;
        }

        if (hitbox == null)
        {
            return;
        }

        if (!breakableBy.Contains(hitbox.ActionType))
        {
            if (enableDebugLog)
            {
                Debug.Log($"BreakableWall: {hitbox.ActionType} では破壊できません。Wall={name}", this);
            }

            return;
        }

        int hitboxId = hitbox.CurrentHitKey;
        if (consumedHitboxIds.Contains(hitboxId))
        {
            return;
        }

        consumedHitboxIds.Add(hitboxId);

        currentDurability -= hitbox.Power;

        if (enableDebugLog)
        {
            Debug.Log(
                $"BreakableWall: 破壊判定を受け取りました。Wall={name}, Action={hitbox.ActionType}, Durability={currentDurability}",
                this);
        }

        if (currentDurability <= 0)
        {
            Vector3 reboundDirection = hitbox.transform.position - transform.position;
            reboundDirection.z = 0.0f;

            if (reboundDirection.sqrMagnitude <= 0.0001f)
            {
                reboundDirection = -hitbox.transform.right;
                reboundDirection.z = 0.0f;
            }

            reboundDirection.Normalize();

            Break(hitPoint);
            hitbox.NotifyWallBroken(this, hitPoint, reboundDirection);
        }
    }

    private void Break(Vector3 hitPoint)
    {
        if (isBroken)
        {
            return;
        }

        isBroken = true;

        if (solidCollider != null)
        {
            solidCollider.enabled = false;
        }

        if (breakTriggerCollider != null)
        {
            breakTriggerCollider.enabled = false;
        }

        SetRenderersEnabled(false);

        if (breakEffectPrefab != null)
        {
            Instantiate(breakEffectPrefab, hitPoint, Quaternion.identity);
        }

        if (audioSource != null && breakSound != null)
        {
            audioSource.PlayOneShot(breakSound);
        }

        if (enableDebugLog)
        {
            Debug.Log($"BreakableWall: 壁を破壊しました。Wall={name}", this);
        }
    }

    private void SetRenderersEnabled(bool enabled)
    {
        if (targetRenderers == null)
        {
            return;
        }

        for (int i = 0; i < targetRenderers.Length; i++)
        {
            if (targetRenderers[i] != null)
            {
                targetRenderers[i].enabled = enabled;
            }
        }
    }

    private void ResolveReferences()
    {
        if (solidCollider == null || breakTriggerCollider == null)
        {
            Collider[] colliders = GetComponents<Collider>();

            for (int i = 0; i < colliders.Length; i++)
            {
                Collider current = colliders[i];

                if (current == null)
                {
                    continue;
                }

                if (!current.isTrigger && solidCollider == null)
                {
                    solidCollider = current;
                    continue;
                }

                if (current.isTrigger && breakTriggerCollider == null)
                {
                    breakTriggerCollider = current;
                }
            }
        }

        if (targetRenderers == null || targetRenderers.Length == 0)
        {
            targetRenderers = GetComponentsInChildren<Renderer>(true);
        }
    }

    public void CaptureInitialState()
    {
        if (hasCapturedInitialState)
        {
            return;
        }

        ResolveReferences();

        initialPosition = transform.position;
        initialRotation = transform.rotation;
        initialScale = transform.localScale;

        initialSolidColliderEnabled = solidCollider != null && solidCollider.enabled;
        initialBreakTriggerColliderEnabled = breakTriggerCollider != null && breakTriggerCollider.enabled;

        if (targetRenderers != null)
        {
            initialRendererEnabledStates = new bool[targetRenderers.Length];

            for (int i = 0; i < targetRenderers.Length; i++)
            {
                initialRendererEnabledStates[i] = targetRenderers[i] != null && targetRenderers[i].enabled;
            }
        }
        else
        {
            initialRendererEnabledStates = null;
        }

        hasCapturedInitialState = true;
    }

    public void ResetToRespawnState()
    {
        ResolveReferences();

        if (hasCapturedInitialState)
        {
            transform.position = initialPosition;
            transform.rotation = initialRotation;
            transform.localScale = initialScale;
        }

        isBroken = false;
        currentDurability = Mathf.Max(1, durability);
        consumedHitboxIds.Clear();

        if (solidCollider != null)
        {
            solidCollider.enabled = hasCapturedInitialState
                ? initialSolidColliderEnabled
                : true;
        }

        if (breakTriggerCollider != null)
        {
            breakTriggerCollider.enabled = hasCapturedInitialState
                ? initialBreakTriggerColliderEnabled
                : true;

            breakTriggerCollider.isTrigger = true;
        }

        RestoreRendererStates();

        if (enableDebugLog)
        {
            Debug.Log($"BreakableWall: リスポーン状態へ復帰しました。Wall={name}", this);
        }
    }

    private void RestoreRendererStates()
    {
        if (targetRenderers == null)
        {
            return;
        }

        if (hasCapturedInitialState &&
            initialRendererEnabledStates != null)
        {
            int count = Mathf.Min(targetRenderers.Length, initialRendererEnabledStates.Length);

            for (int i = 0; i < count; i++)
            {
                if (targetRenderers[i] != null)
                {
                    targetRenderers[i].enabled = initialRendererEnabledStates[i];
                }
            }

            return;
        }

        SetRenderersEnabled(true);
    }
}