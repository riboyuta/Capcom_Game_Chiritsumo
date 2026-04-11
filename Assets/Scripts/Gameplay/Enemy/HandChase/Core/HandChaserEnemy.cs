using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public sealed class HandChaserEnemy : MonoBehaviour, IRespawnResettable
{
    [Header("移動制御")]
    [Tooltip("移動制御コンポーネントです。未設定時は自動取得します。")]
    [SerializeField] private HandChaserMovement movement;

    [Header("見た目制御")]
    [Tooltip("見た目制御コンポーネントです。未設定時は自動取得します。")]
    [SerializeField] private HandChaserView view;

    [Header("接近エフェクト")]
    [Tooltip("接近エフェクト制御コンポーネントです。未設定時は自動取得します。")]
    [SerializeField] private ProximityEffectController proximityEffects;

    [Header("プレイヤー")]
    [Tooltip("追跡対象のプレイヤーです。未設定時はタグから自動取得します。")]
    [SerializeField] private Transform player;

    [Header("プレイヤータグ")]
    [Tooltip("プレイヤー検索に使用するタグ名です。")]
    [SerializeField] private string playerTag = "Player";

    [Header("開始時有効化")]
    [Tooltip("シーン開始時に自動で有効化するかどうかです。")]
    [SerializeField] private bool startActive = false;

    [Header("起動前非表示")]
    [Tooltip("有効化されるまで見た目を非表示にするかどうかです。")]
    [SerializeField] private bool hideUntilActivated = true;

    [Header("起動時ワープ使用")]
    [Tooltip("有効化時に指定位置へワープするかどうかです。")]
    [SerializeField] private bool useSpawnPositionOnActivate = false;

    [Header("起動時ワープ位置")]
    [Tooltip("有効化時のワープ位置です。")]
    [SerializeField] private Vector3 spawnPositionOnActivate;

    [Header("接触時即死")]
    [Tooltip("プレイヤーと接触した時に即死させるかどうかです。")]
    [SerializeField] private bool killPlayerOnContact = true;

    [Header("即死後無効化")]
    [Tooltip("プレイヤーを即死させた後、敵を無効化するかどうかです。")]
    [SerializeField] private bool disableAfterKill = false;

    [Header("デバッグログ")]
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
        // Rigidbodyの初期設定
        rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        // 各コンポーネントをキャッシュ
        cachedCollider = GetComponent<Collider>();
        cachedRenderers = GetComponentsInChildren<Renderer>(true);

        // 初期位置を保存
        initialPosition = transform.position;
        initialRotation = transform.rotation;

        // 必要なコンポーネントを取得
        TryGetComponents();
        ResolvePlayerIfNeeded();

        // 起動状態を設定
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

    private void OnTriggerEnter(Collider other)
    {
        // 無効状態、または起動していない場合はスキップ
        if (isDisabled || !isActivated || !killPlayerOnContact)
        {
            return;
        }

        // プレイヤー以外は無視
        if (!other.CompareTag(playerTag))
        {
            return;
        }

        // PlayerControllerを取得
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

        // プレイヤーに死亡ダメージを与える
        bool accepted = playerController.RequestDamageDeath();

        if (enableDebugLog)
        {
            Debug.Log($"[HandChaserEnemy] Hit player '{other.name}', RequestDamageDeath accepted={accepted}", this);
        }

        // プレイヤーを殺した後、敵を無効化する設定なら無効化
        if (accepted && disableAfterKill)
        {
            DisableSelf();
        }
    }

    public void BeginChase()
    {
        // 既に起動済みならスキップ
        if (isActivated)
        {
            Debug.LogWarning("[HandChaserEnemy] Already activated!", this);
            return;
        }

        Debug.Log($"[HandChaserEnemy] BeginChase start. movement={(movement != null ? movement.name : "NULL")}", this);

        // スポーン位置を使用する設定ならワープ
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

        // 敵を有効化
        isActivated = true;
        ApplyActivationVisualState();

        // 移動を有効化
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

        // 接近エフェクトを有効化
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
        // 既にキャプチャ済みなら何もしない
        if (hasCapturedInitialState)
        {
            return;
        }

        // 現在の位置と回転を初期状態として保存
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
        // 位置と回転を初期状態に戻す
        transform.position = initialPosition;
        transform.rotation = initialRotation;

        isDisabled = false;
        isActivated = startActive;

        // Rigidbodyの速度をリセット
        if (rb != null)
        {
            // Kinematic Rigidbody を一時的に非 Kinematic にして速度をリセット
            bool wasKinematic = rb.isKinematic;
            rb.isKinematic = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = wasKinematic;
        }

        // 各コンポーネントをリセット
        if (movement != null)
        {
            movement.IsActive = isActivated;
        }

        if (proximityEffects != null)
        {
            proximityEffects.IsActive = isActivated;
        }

        // 表示状態を更新
        ApplyActivationVisualState();

        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }
    }

    private void ApplyActivationVisualState()
    {
        // 起動済み、または非表示設定が無効なら表示
        bool visible = isActivated || !hideUntilActivated;

        // Colliderは起動済みの時だけ有効
        if (cachedCollider != null)
        {
            cachedCollider.enabled = isActivated;
        }

        // 全てのRendererの表示状態を設定
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

        if (proximityEffects != null)
        {
            proximityEffects.SetPlayerTarget(target);
        }
    }
}
