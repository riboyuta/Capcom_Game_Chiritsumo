using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemySimpleHand3D : MonoBehaviour
{
    private enum AttackType
    {
        Smash,
        Grab
    }

    [Header("移動")]
    [SerializeField, Min(0f)] private float moveSpeed = 2.0f;
    [SerializeField] private Vector3 moveAxis = Vector3.right;

    [Header("判定")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool disableAfterHit = false;

    [Header("起動制御")]
    [SerializeField] private bool startActive = false;
    [SerializeField] private bool hideUntilActivated = true;
    [SerializeField] private Vector3 spawnPositionOnActivate;
    [SerializeField] private bool useSpawnPositionOnActivate = false;

    [Header("攻撃: 参照")]
    [SerializeField] private Transform player;
    [SerializeField] private HandSmashAttack handSmashPrefab;
    [SerializeField] private HandGrabAttack handGrabPrefab;

    [Header("攻撃: 基本設定")]
    [SerializeField] private Vector3 handSpawnOffset = new Vector3(2.0f, 1.0f, 0.0f);
    [SerializeField, Min(0f)] private float smashAttackRangeX = 8.0f;
    [SerializeField, Min(0f)] private float grabAttackRangeX = 3.0f;
    [SerializeField, Min(0f)] private float attackCooldown = 2.5f;
    [SerializeField] private float groundY = 0.0f;

    [Header("攻撃: デバッグ")]
    [SerializeField] private bool debugTriggerWithSpace = false;

    [Header("見た目参照")]
    [SerializeField] private Transform visualRoot;
    [SerializeField] private SpriteRenderer armRenderer;
    [SerializeField] private SpriteRenderer palmRenderer;

    [Header("見た目: 全体調整")]
    [SerializeField] private Vector3 visualRootLocalOffset = Vector3.zero;
    [SerializeField] private Vector3 visualRootLocalScale = Vector3.one;

    [Header("見た目: 腕調整")]
    [SerializeField] private Vector3 armLocalOffset = Vector3.zero;
    [SerializeField] private Vector3 armLocalScale = Vector3.one;

    [Header("見た目: 手の平調整")]
    [SerializeField] private Vector3 palmLocalOffset = Vector3.zero;
    [SerializeField] private Vector3 palmLocalScale = Vector3.one;

    [Header("アニメ")]
    [SerializeField] private Sprite[] armFrames;
    [SerializeField] private Sprite[] palmFrames;
    [SerializeField, Min(0f)] private float animationFps = 8.0f;

    [Header("デバッグ")]
    [SerializeField] private bool enableDebugLog;
    [SerializeField] private bool logMissingReferences = true;

    private Rigidbody rb;
    private bool isDisabled;
    private float animationTimer;
    private float attackCooldownTimer;
    private bool isHandActive;
    private GameObject activeHandInstance;

    private bool isActivated;
    private Collider cachedCollider;
    private Renderer[] cachedRenderers;

    private AttackType lastAttackType = AttackType.Smash;
    private int sameAttackStreakCount = 0;

    private Vector3 initialPosition;
    private Quaternion initialRotation;

    private void Reset()
    {
        visualRoot = transform;
        rb = GetComponent<Rigidbody>();
        TryAutoAssignRenderers();
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        cachedCollider = GetComponent<Collider>();
        cachedRenderers = GetComponentsInChildren<Renderer>(true);

        initialPosition = transform.position;
        initialRotation = transform.rotation;

        if (visualRoot == null)
        {
            visualRoot = transform;
        }

        if (player == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag(playerTag);
            if (playerObject != null)
            {
                player = playerObject.transform;
            }
        }

        TryAutoAssignRenderers();

        if (rb == null && logMissingReferences)
        {
            Debug.LogWarning("[EnemySimpleHand3D] Rigidbody がありません。移動は行われません。", this);
        }

        isActivated = startActive;
        ApplyActivationVisualState();
    }

    private void OnValidate()
    {
        moveSpeed = Mathf.Max(0f, moveSpeed);
        animationFps = Mathf.Max(0f, animationFps);
        smashAttackRangeX = Mathf.Max(0f, smashAttackRangeX);
        grabAttackRangeX = Mathf.Max(0f, grabAttackRangeX);
        attackCooldown = Mathf.Max(0f, attackCooldown);

        if (visualRootLocalScale.x == 0f) visualRootLocalScale.x = 1f;
        if (visualRootLocalScale.y == 0f) visualRootLocalScale.y = 1f;
        if (visualRootLocalScale.z == 0f) visualRootLocalScale.z = 1f;

        if (armLocalScale.x == 0f) armLocalScale.x = 1f;
        if (armLocalScale.y == 0f) armLocalScale.y = 1f;
        if (armLocalScale.z == 0f) armLocalScale.z = 1f;

        if (palmLocalScale.x == 0f) palmLocalScale.x = 1f;
        if (palmLocalScale.y == 0f) palmLocalScale.y = 1f;
        if (palmLocalScale.z == 0f) palmLocalScale.z = 1f;

        if (visualRoot == null)
        {
            visualRoot = transform;
        }

        if (moveAxis.sqrMagnitude > 0f)
        {
            moveAxis = moveAxis.normalized;
        }

        TryAutoAssignRenderers();
    }

    private void FixedUpdate()
    {
        if (isDisabled || rb == null || !isActivated)
        {
            return;
        }

        Vector3 next = rb.position + moveAxis * (moveSpeed * Time.fixedDeltaTime);
        rb.MovePosition(next);
    }

    private void Update()
    {
        if (isDisabled)
        {
            return;
        }

        UpdateSimpleAnimation();
        ApplyVisualLayout();

        if (!isActivated)
        {
            return;
        }

        if (attackCooldownTimer > 0.0f)
        {
            attackCooldownTimer -= Time.deltaTime;
        }

        if (debugTriggerWithSpace)
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                TryStartRandomAttack();
            }
            return;
        }

        TryStartRandomAttack();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isDisabled || !isActivated)
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
            if (logMissingReferences)
            {
                Debug.LogWarning($"[EnemySimpleHand3D] {other.name} has tag '{playerTag}' but no PlayerController.", this);
            }
            return;
        }

        bool accepted = playerController.RequestDamageDeath();

        if (!accepted)
        {
            if (enableDebugLog)
            {
                Debug.Log($"[EnemySimpleHand3D] RequestDamageDeath rejected for '{other.name}'.", this);
            }
            return;
        }

        if (enableDebugLog)
        {
            Debug.Log($"[EnemySimpleHand3D] Hit player '{other.name}', RequestDamageDeath accepted=true", this);
        }

        if (disableAfterHit)
        {
            DisableSelf();
        }
    }

    public void BeginChase()
    {
        if (isActivated)
        {
            return;
        }

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
        attackCooldownTimer = 0.0f;
        ApplyActivationVisualState();

        if (enableDebugLog)
        {
            Debug.Log("[EnemySimpleHand3D] BeginChase called. Enemy activated.", this);
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

    private void TryStartRandomAttack()
    {
        if (isHandActive)
        {
            return;
        }

        if (attackCooldownTimer > 0.0f)
        {
            return;
        }

        ResolvePlayerIfNeeded();
        if (player == null)
        {
            return;
        }

        float dx = Mathf.Abs(player.position.x - transform.position.x);

        bool canUseSmash = handSmashPrefab != null && dx <= smashAttackRangeX;
        bool canUseGrab = handGrabPrefab != null && dx <= grabAttackRangeX;

        if (!canUseSmash && !canUseGrab)
        {
            return;
        }

        if (canUseSmash && !canUseGrab)
        {
            if (TryStartSmashAttack())
            {
                RecordAttackType(AttackType.Smash);
            }
            return;
        }

        if (!canUseSmash && canUseGrab)
        {
            if (TryStartGrabAttack())
            {
                RecordAttackType(AttackType.Grab);
            }
            return;
        }

        AttackType selectedAttackType = SelectNextAttackType();

        if (selectedAttackType == AttackType.Smash)
        {
            if (TryStartSmashAttack())
            {
                RecordAttackType(AttackType.Smash);
            }
        }
        else
        {
            if (TryStartGrabAttack())
            {
                RecordAttackType(AttackType.Grab);
            }
        }
    }

    private AttackType SelectNextAttackType()
    {
        if (sameAttackStreakCount >= 2)
        {
            return GetOppositeAttackType(lastAttackType);
        }

        return Random.value < 0.5f ? AttackType.Smash : AttackType.Grab;
    }

    private AttackType GetOppositeAttackType(AttackType attackType)
    {
        return attackType == AttackType.Smash ? AttackType.Grab : AttackType.Smash;
    }

    private void RecordAttackType(AttackType attackType)
    {
        if (sameAttackStreakCount == 0)
        {
            lastAttackType = attackType;
            sameAttackStreakCount = 1;
            return;
        }

        if (attackType == lastAttackType)
        {
            sameAttackStreakCount++;
        }
        else
        {
            lastAttackType = attackType;
            sameAttackStreakCount = 1;
        }
    }

    private bool TryStartSmashAttack()
    {
        if (isHandActive)
        {
            return false;
        }

        if (handSmashPrefab == null)
        {
            if (logMissingReferences)
            {
                Debug.LogWarning("[EnemySimpleHand3D] HandSmashAttack prefab が未設定です。", this);
            }
            return false;
        }

        if (player == null)
        {
            return false;
        }

        Vector3 spawnPosition = transform.position + handSpawnOffset;

        HandSmashAttack handInstance = Instantiate(
            handSmashPrefab,
            spawnPosition,
            Quaternion.identity
        );

        activeHandInstance = handInstance.gameObject;
        isHandActive = true;
        attackCooldownTimer = attackCooldown;

        handInstance.StartAttack(
            spawnPosition,
            player,
            groundY,
            OnHandAttackFinished
        );

        if (enableDebugLog)
        {
            Debug.Log("[EnemySimpleHand3D] Smash attack started.", this);
        }

        return true;
    }

    private bool TryStartGrabAttack()
    {
        if (isHandActive)
        {
            return false;
        }

        if (handGrabPrefab == null)
        {
            if (logMissingReferences)
            {
                Debug.LogWarning("[EnemySimpleHand3D] HandGrabAttack prefab が未設定です。", this);
            }
            return false;
        }

        if (player == null)
        {
            return false;
        }

        Vector3 spawnPosition = transform.position + handSpawnOffset;

        HandGrabAttack handInstance = Instantiate(
            handGrabPrefab,
            spawnPosition,
            Quaternion.identity
        );

        activeHandInstance = handInstance.gameObject;
        isHandActive = true;
        attackCooldownTimer = attackCooldown;

        handInstance.StartAttack(
            spawnPosition,
            player,
            OnHandAttackFinished
        );

        if (enableDebugLog)
        {
            Debug.Log("[EnemySimpleHand3D] Grab attack started.", this);
        }

        return true;
    }

    private void OnHandAttackFinished()
    {
        isHandActive = false;
        activeHandInstance = null;
    }

    public void ResetEncounterForRespawn()
    {
        transform.position = initialPosition;
        transform.rotation = initialRotation;

        attackCooldownTimer = 0.0f;
        isHandActive = false;
        isDisabled = false;
        animationTimer = 0.0f;
        sameAttackStreakCount = 0;
        lastAttackType = AttackType.Smash;

        if (activeHandInstance != null)
        {
            Destroy(activeHandInstance);
            activeHandInstance = null;
        }

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        isActivated = startActive;
        ApplyActivationVisualState();

        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }
    }

    public void ResetToRespawnState()
    {
        ResetEncounterForRespawn();
    }

    private void DisableSelf()
    {
        isDisabled = true;

        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.enabled = false;
        }

        gameObject.SetActive(false);
    }

    private void UpdateSimpleAnimation()
    {
        if (animationFps <= 0f)
        {
            ApplyFrame(armRenderer, armFrames, 0);
            ApplyFrame(palmRenderer, palmFrames, 0);
            return;
        }

        animationTimer += Time.deltaTime;

        int armIndex = GetLoopFrameIndex(armFrames, animationTimer, animationFps);
        int palmIndex = GetLoopFrameIndex(palmFrames, animationTimer, animationFps);

        ApplyFrame(armRenderer, armFrames, armIndex);
        ApplyFrame(palmRenderer, palmFrames, palmIndex);
    }

    private static int GetLoopFrameIndex(Sprite[] frames, float time, float fps)
    {
        if (frames == null || frames.Length == 0)
        {
            return -1;
        }

        int index = Mathf.FloorToInt(time * fps) % frames.Length;
        if (index < 0)
        {
            index += frames.Length;
        }

        return index;
    }

    private static void ApplyFrame(SpriteRenderer target, Sprite[] frames, int index)
    {
        if (target == null || frames == null || frames.Length == 0)
        {
            return;
        }

        if (index < 0 || index >= frames.Length)
        {
            return;
        }

        target.sprite = frames[index];
    }

    private void ApplyVisualLayout()
    {
        if (visualRoot != null)
        {
            visualRoot.localPosition = visualRootLocalOffset;
            visualRoot.localScale = visualRootLocalScale;
        }

        if (armRenderer != null)
        {
            armRenderer.transform.localPosition = armLocalOffset;
            armRenderer.transform.localScale = armLocalScale;
        }

        if (palmRenderer != null)
        {
            palmRenderer.transform.localPosition = palmLocalOffset;
            palmRenderer.transform.localScale = palmLocalScale;
        }
    }

    private void ResolvePlayerIfNeeded()
    {
        if (player != null)
        {
            return;
        }

        GameObject playerObject = GameObject.FindGameObjectWithTag(playerTag);
        if (playerObject != null)
        {
            player = playerObject.transform;
        }
    }

    private void TryAutoAssignRenderers()
    {
        if (visualRoot == null)
        {
            return;
        }

        if (armRenderer == null)
        {
            armRenderer = visualRoot.Find("ArmRenderer")?.GetComponent<SpriteRenderer>();
        }

        if (palmRenderer == null)
        {
            palmRenderer = visualRoot.Find("PalmRenderer")?.GetComponent<SpriteRenderer>();
        }
    }
}