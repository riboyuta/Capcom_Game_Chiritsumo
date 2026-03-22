using UnityEngine;

[RequireComponent(typeof(Collider))]
public sealed class EnemyCore : MonoBehaviour
{
    private enum AttackType
    {
        Smash,
        Grab
    }

    [Header("References")]
    [SerializeField] private Transform player;
    [SerializeField] private HandSmashAttack handSmashPrefab;
    [SerializeField] private HandGrabAttack handGrabPrefab;

    [Header("Move")]
    [SerializeField] private float moveSpeed = 2.0f;

    [Header("Attack")]
    [SerializeField] private Vector3 handSpawnOffset = new Vector3(2.0f, 1.0f, 0.0f);
    [SerializeField] private float smashAttackRangeX = 8.0f;
    [SerializeField] private float grabAttackRangeX = 3.0f;
    [SerializeField] private float attackCooldown = 2.5f;
    [SerializeField] private float groundY = 0.0f;

    [Header("Debug")]
    [SerializeField] private bool debugTriggerWithSpace = false;

    private float attackCooldownTimer = 0.0f;
    private bool isHandActive = false;

    private Vector3 initialPosition;
    private Quaternion initialRotation;
    private GameObject activeHandInstance;

    private AttackType lastAttackType = AttackType.Smash;
    private int sameAttackStreakCount = 0;

    private void Reset()
    {
        Collider col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    private void Awake()
    {
        initialPosition = transform.position;
        initialRotation = transform.rotation;

        if (player == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null)
            {
                player = playerObject.transform;
            }
        }
    }

    private void Update()
    {
        if (player == null)
        {
            return;
        }

        MoveTowardsPlayer();

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

    private void MoveTowardsPlayer()
    {
        Vector3 pos = transform.position;
        float dir = Mathf.Sign(player.position.x - pos.x);

        pos.x += dir * moveSpeed * Time.deltaTime;
        transform.position = pos;
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

        if (attackCooldownTimer > 0.0f)
        {
            return false;
        }

        if (handSmashPrefab == null)
        {
            Debug.LogWarning("EnemyCore: HandSmashAttack prefab が設定されていません。");
            return false;
        }

        if (player == null)
        {
            return false;
        }

        Vector3 spawnPosition = transform.position + handSpawnOffset;
        Vector3 targetPlayerPosition = player.position;

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

        return true;
    }

    private bool TryStartGrabAttack()
    {
        if (isHandActive)
        {
            return false;
        }

        if (attackCooldownTimer > 0.0f)
        {
            return false;
        }

        if (handGrabPrefab == null)
        {
            Debug.LogWarning("EnemyCore: HandGrabAttack prefab が設定されていません。");
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

        return true;
    }

    private void OnHandAttackFinished()
    {
        isHandActive = false;
        activeHandInstance = null;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
        {
            return;
        }

        KillPlayer(other.gameObject);
    }

    private void KillPlayer(GameObject playerObject)
    {
        PlayerController playerController = playerObject.GetComponent<PlayerController>();
        if (playerController != null)
        {
            playerController.RequestDamageDeath();
            return;
        }

        Debug.LogWarning("EnemyCore: PlayerController が見つからないため死亡要求を送れませんでした。", playerObject);
    }

    public void ResetToRespawnState()
    {
        transform.position = initialPosition;
        transform.rotation = initialRotation;

        attackCooldownTimer = 0.0f;
        isHandActive = false;

        if (activeHandInstance != null)
        {
            Destroy(activeHandInstance);
            activeHandInstance = null;
        }
    }
}