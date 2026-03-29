using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemyCore : MonoBehaviour
{
    private enum AttackType
    {
        Smash,
        Grab
    }

    [Header("移動")]
    [SerializeField, Min(0f)] private float moveSpeed = 2.0f;
    [SerializeField] private Vector3 moveAxis = Vector3.right;

    [Header("移動: 距離追従")]
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

    [Header("カメラシェイク (接近時)")]
    [Tooltip("プレイヤー近接時に発生させるカメラ振動のプロファイル")]
    [SerializeField] private Capcom_Game_Chiritsumo.Camera.CameraShake.CameraShakeProfile continuousShakeProfile;
    [Tooltip("揺れが最大(強度1.0)になるプレイヤーとの距離")]
    [SerializeField, Min(0f)] private float maxShakeDistance = 2.0f;
    [Tooltip("揺れが発生し始める(強度0.0)プレイヤーとの距離")]
    [SerializeField, Min(0f)] private float minShakeDistance = 10.0f;

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

        animationFps = Mathf.Max(0f, animationFps);
        smashAttackRangeX = Mathf.Max(0f, smashAttackRangeX);
        grabAttackRangeX = Mathf.Max(0f, grabAttackRangeX);
        attackCooldown = Mathf.Max(0f, attackCooldown);
        maxShakeDistance = Mathf.Max(0f, maxShakeDistance);
        minShakeDistance = Mathf.Max(maxShakeDistance, minShakeDistance);

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

        float currentSpeed = GetCurrentMoveSpeed();
        Vector3 next = rb.position + moveAxis * (currentSpeed * Time.fixedDeltaTime);
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

        // プレイヤー距離に応じたカメラシェイク
        UpdateCameraShake();

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

    private void UpdateCameraShake()
    {
        if (continuousShakeProfile == null || isDisabled || !isActivated)
        {
            return;
        }

        ResolvePlayerIfNeeded();
        if (player == null)
        {
            return;
        }

        float dx = Mathf.Abs(player.position.x - transform.position.x);

        if (dx > minShakeDistance)
        {
            return; // 遠い場合は揺らさない
        }

        // 距離を0(遠い)～1(近い)に変換
        float intensity = 1.0f - Mathf.InverseLerp(maxShakeDistance, minShakeDistance, dx);

        Capcom_Game_Chiritsumo.Camera.CameraShake.CameraShakeManager.Instance?.SetContinuousIntensity(intensity, continuousShakeProfile);
        
        // プレイヤーへの距離に応じたVignette（暗転と脈打ち）効果を適用
        Capcom_Game_Chiritsumo.Camera.VignetteEffects.VignetteManager.Instance?.SetContinuousIntensity(intensity);
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

    private float GetCurrentMoveSpeed()
    {
        if (!useDistanceBasedSpeed)
        {
            return isHandActive ? moveSpeed * speedMultiplierWhileAttacking : moveSpeed;
        }

        ResolvePlayerIfNeeded();
        if (player == null)
        {
            return isHandActive ? moveSpeed * speedMultiplierWhileAttacking : moveSpeed;
        }

        float dx = Mathf.Abs(player.position.x - transform.position.x);

        float baseSpeed;
        if (dx <= distanceForMinSpeed)
        {
            baseSpeed = minMoveSpeed;
        }
        else
        {
            float t = Mathf.InverseLerp(distanceForMinSpeed, distanceForMaxSpeed, dx);
            baseSpeed = Mathf.Lerp(minMoveSpeed, maxMoveSpeed, t);
        }

        if (isHandActive)
        {
            baseSpeed *= speedMultiplierWhileAttacking;
        }

        return baseSpeed;
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

    private void OnDrawGizmosSelected()
    {
        DrawDistanceSpeedGizmos();
    }

    private void DrawDistanceSpeedGizmos()
    {
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