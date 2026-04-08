using UnityEngine;

/// <summary>
/// HandChaser敵のメインコントローラー。
/// Movement, AttackController, View, ProximityEffectsを統合し、
/// 起動・リスポーン・プレイヤー接触判定を管理する。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public sealed class HandChaserEnemy : MonoBehaviour, IRespawnResettable
{
    [Header("コンポーネント参照")]
    [Tooltip("移動制御コンポーネントです。未設定時は自動取得します。")]
    [SerializeField] private HandChaserMovement movement;

    [Tooltip("攻撃制御コンポーネントです。未設定時は自動取得します。")]
    [SerializeField] private HandChaserAttackController attackController;

    [Tooltip("見た目制御コンポーネントです。未設定時は自動取得します。")]
    [SerializeField] private HandChaserView view;

    [Tooltip("接近エフェクト制御コンポーネントです。未設定時は自動取得します。")]
    [SerializeField] private ProximityEffectController proximityEffects;

    [Header("プレイヤー参照")]
    [Tooltip("追跡対象のプレイヤーです。未設定時はタグから自動取得します。")]
    [SerializeField] private Transform player;

    [Tooltip("プレイヤー検索に使用するタグ名です。")]
    [SerializeField] private string playerTag = "Player";

    [Header("起動制御")]
    [Tooltip("シーン開始時に自動で有効化するかどうかです。")]
    [SerializeField] private bool startActive = false;

    [Tooltip("有効化されるまで見た目を非表示にするかどうかです。")]
    [SerializeField] private bool hideUntilActivated = true;

    [Tooltip("有効化時に指定位置へワープするかどうかです。")]
    [SerializeField] private bool useSpawnPositionOnActivate = false;

    [Tooltip("有効化時のワープ位置です。")]
    [SerializeField] private Vector3 spawnPositionOnActivate;

    [Header("プレイヤー接触判定")]
    [Tooltip("プレイヤーと接触した時に即死させるかどうかです。")]
    [SerializeField] private bool killPlayerOnContact = true;

    [Tooltip("プレイヤーを即死させた後、敵を無効化するかどうかです。")]
    [SerializeField] private bool disableAfterKill = false;

    [Header("デバッグ")]
    [Tooltip("デバッグログを有効にします。")]
    [SerializeField] private bool enableDebugLog;

    private Rigidbody rb;
    private Collider cachedCollider;
    private Renderer[] cachedRenderers;
    private bool isActivated;
    private bool isDisabled;

    private Vector3 initialPosition;
    private Quaternion initialRotation;
    private bool hasCapturedInitialState;

    private void Reset()
    {
        TryGetComponents();
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        cachedCollider = GetComponent<Collider>();
        cachedRenderers = GetComponentsInChildren<Renderer>(true);

        initialPosition = transform.position;
        initialRotation = transform.rotation;

        TryGetComponents();
        ResolvePlayerIfNeeded();

        isActivated = startActive;
        ApplyActivationVisualState();

        // startActive が true なら、Movement と ProximityEffects も有効化
        if (startActive)
        {
            if (movement != null)
            {
                movement.IsActive = true;
            }

            if (proximityEffects != null)
            {
                proximityEffects.IsActive = true;
            }
        }
    }

    private void Update()
    {
        if (isDisabled || !isActivated)
        {
            return;
        }

        // 攻撃をトリガー
        if (attackController != null)
        {
            attackController.TryStartRandomAttack();
        }
    }

    private void LateUpdate()
    {
        if (isDisabled || !isActivated)
        {
            return;
        }

        // 移動とエフェクトの状態を同期
        if (movement != null && attackController != null)
        {
            movement.IsAttacking = attackController.IsAttacking;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isDisabled || !isActivated || !killPlayerOnContact)
        {
            return;
        }

        if (!other.CompareTag(playerTag))
        {
            return;
        }

        PlayerController playerController = other.GetComponent<PlayerController>();
        if (playerController == null)
        {
            playerController = other.GetComponentInParent<PlayerController>();
        }

        if (playerController == null)
        {
            Debug.LogWarning($"[HandChaserEnemy] {other.name} has tag '{playerTag}' but no PlayerController.", this);
            return;
        }

        bool accepted = playerController.RequestDamageDeath();

        if (enableDebugLog)
        {
            Debug.Log($"[HandChaserEnemy] Hit player '{other.name}', RequestDamageDeath accepted={accepted}", this);
        }

        if (accepted && disableAfterKill)
        {
            DisableSelf();
        }
    }

    public void BeginChase()
    {
        if (isActivated)
        {
            Debug.LogWarning("[HandChaserEnemy] Already activated!", this);
            return;
        }

        Debug.Log($"[HandChaserEnemy] BeginChase start. movement={(movement != null ? movement.name : "NULL")}", this);

        if (useSpawnPositionOnActivate)
        {
            if (rb != null)
            {
                rb.position = spawnPositionOnActivate;
            }
            else
            {
                transform.position = spawnPositionOnActivate;
            }
        }

        isActivated = true;
        ApplyActivationVisualState();

        if (movement != null)
        {
            movement.IsActive = true;
            Debug.Log($"[HandChaserEnemy] movement.IsActive={movement.IsActive}", this);

            if (enableDebugLog)
            {
                Debug.Log("[HandChaserEnemy] Movement activated!", this);
            }
        }
        else if (enableDebugLog)
        {
            Debug.LogError("[HandChaserEnemy] Movement component is NULL!", this);
        }

        if (proximityEffects != null)
        {
            proximityEffects.IsActive = true;
        }

        if (enableDebugLog)
        {
            Debug.Log("[HandChaserEnemy] BeginChase called. Enemy activated.", this);
        }
    }

    public void CaptureInitialState()
    {
        if (hasCapturedInitialState)
        {
            return;
        }

        initialPosition = transform.position;
        initialRotation = transform.rotation;
        hasCapturedInitialState = true;
    }

    public void ResetToRespawnState()
    {
        ResetEncounterForRespawn();
    }

    public void ResetEncounterForRespawn()
    {
        transform.position = initialPosition;
        transform.rotation = initialRotation;

        isDisabled = false;
        isActivated = startActive;

        if (rb != null)
        {
            // Kinematic Rigidbody を一時的に非 Kinematic にして速度をリセット
            bool wasKinematic = rb.isKinematic;
            rb.isKinematic = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = wasKinematic;
        }

        if (movement != null)
        {
            movement.IsActive = isActivated;
            movement.IsAttacking = false;
        }

        if (attackController != null)
        {
            attackController.ResetAttackState();
        }

        if (proximityEffects != null)
        {
            proximityEffects.IsActive = isActivated;
        }

        ApplyActivationVisualState();

        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }
    }

    private void ApplyActivationVisualState()
    {
        bool visible = isActivated || !hideUntilActivated;

        if (cachedCollider != null)
        {
            cachedCollider.enabled = isActivated;
        }

        if (cachedRenderers != null)
        {
            for (int i = 0; i < cachedRenderers.Length; i++)
            {
                if (cachedRenderers[i] != null)
                {
                    cachedRenderers[i].enabled = visible;
                }
            }
        }
    }

    private void DisableSelf()
    {
        isDisabled = true;

        if (cachedCollider != null)
        {
            cachedCollider.enabled = false;
        }

        if (movement != null)
        {
            movement.IsActive = false;
        }

        if (proximityEffects != null)
        {
            proximityEffects.IsActive = false;
        }

        gameObject.SetActive(false);
    }

    private void TryGetComponents()
    {
        if (movement == null)
        {
            movement = GetComponent<HandChaserMovement>();
        }

        if (attackController == null)
        {
            attackController = GetComponent<HandChaserAttackController>();
        }

        if (view == null)
        {
            view = GetComponent<HandChaserView>();
        }

        if (proximityEffects == null)
        {
            proximityEffects = GetComponent<ProximityEffectController>();
        }
    }

    private void ResolvePlayerIfNeeded()
    {
        if (player != null)
        {
            SetPlayerTarget(player);
            return;
        }

        GameObject playerObject = GameObject.FindGameObjectWithTag(playerTag);
        if (playerObject != null)
        {
            player = playerObject.transform;
            SetPlayerTarget(player);
        }
    }

    private void SetPlayerTarget(Transform target)
    {
        if (movement != null)
        {
            movement.SetPlayerTarget(target);
        }

        if (attackController != null)
        {
            attackController.SetPlayerTarget(target);
        }

        if (proximityEffects != null)
        {
            proximityEffects.SetPlayerTarget(target);
        }
    }
}
