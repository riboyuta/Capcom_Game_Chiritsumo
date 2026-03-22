using UnityEngine;

// 左から迫ってくる敵本体。
// 本体接触による死亡判定と、手攻撃の発動を担当する。
[RequireComponent(typeof(Collider))]
public sealed class EnemyCore : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform player;
    [SerializeField] private HandSmashAttack handSmashPrefab;

    [Header("Move")]
    [SerializeField] private float moveSpeed = 2.0f;

    [Header("Attack")]
    [SerializeField] private Vector3 handSpawnOffset = new Vector3(2.0f, 1.0f, 0.0f);
    [SerializeField] private float attackRangeX = 8.0f;
    [SerializeField] private float attackCooldown = 2.5f;
    [SerializeField] private float groundY = 0.0f;

    [Header("Debug")]
    [SerializeField] private bool debugTriggerWithSpace = false;

    private float attackCooldownTimer = 0.0f;
    private bool isHandActive = false;

    private void Reset()
    {
        Collider col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    private void Awake()
    {
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
                TryStartSmashAttack();
            }
            return;
        }

        float dx = Mathf.Abs(player.position.x - transform.position.x);
        if (dx <= attackRangeX)
        {
            TryStartSmashAttack();
        }
    }

    private void MoveTowardsPlayer()
    {
        Vector3 pos = transform.position;
        float dir = Mathf.Sign(player.position.x - pos.x);

        pos.x += dir * moveSpeed * Time.deltaTime;
        transform.position = pos;
    }

    private void TryStartSmashAttack()
    {
        if (isHandActive)
        {
            return;
        }

        if (attackCooldownTimer > 0.0f)
        {
            return;
        }

        if (handSmashPrefab == null)
        {
            Debug.LogWarning("EnemyCore: HandSmashAttack prefab が設定されていません。");
            return;
        }

        if (player == null)
        {
            return;
        }

        Vector3 spawnPosition = transform.position + handSpawnOffset;
        Vector3 targetPlayerPosition = player.position;

        HandSmashAttack handInstance = Instantiate(
            handSmashPrefab,
            spawnPosition,
            Quaternion.identity
        );

        isHandActive = true;
        attackCooldownTimer = attackCooldown;

        handInstance.StartAttack(
            spawnPosition,
            targetPlayerPosition,
            groundY,
            OnHandAttackFinished
        );
    }

    private void OnHandAttackFinished()
    {
        isHandActive = false;
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
}