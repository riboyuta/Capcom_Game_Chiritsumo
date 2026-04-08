using System;
using UnityEngine;

/// <summary>
/// HandChaser敵の攻撃制御を管理するコンポーネント。
/// 攻撃タイプの選択、クールダウン管理、攻撃インスタンスの生成を行う。
/// </summary>
public sealed class HandChaserAttackController : MonoBehaviour
{
    private enum AttackType
    {
        Smash,
        Grab
    }

    [Header("攻撃Prefab")]
    [Tooltip("叩きつけ攻撃のPrefabです。")]
    [SerializeField] private HandSmashAttack handSmashPrefab;

    [Tooltip("掴み攻撃のPrefabです。")]
    [SerializeField] private HandGrabAttack handGrabPrefab;

    [Header("攻撃設定")]
    [Tooltip("攻撃の出現位置オフセットです。")]
    [SerializeField] private Vector3 handSpawnOffset = new Vector3(2.0f, 1.0f, 0.0f);

    [Tooltip("叩きつけ攻撃が使える最大X距離です。")]
    [SerializeField, Min(0f)] private float smashAttackRangeX = 8.0f;

    [Tooltip("掴み攻撃が使える最大X距離です。")]
    [SerializeField, Min(0f)] private float grabAttackRangeX = 3.0f;

    [Tooltip("攻撃後のクールダウン時間です。")]
    [SerializeField, Min(0f)] private float attackCooldown = 2.5f;

    [Tooltip("地面のY座標です。Smash攻撃の着地点に使用されます。")]
    [SerializeField] private float groundY = 0.0f;

    [Header("デバッグ")]
    [Tooltip("スペースキーで攻撃をトリガーできるようにします。")]
    [SerializeField] private bool debugTriggerWithSpace = false;

    [Tooltip("デバッグログを有効にします。")]
    [SerializeField] private bool enableDebugLog;

    private Transform playerTransform;
    private float attackCooldownTimer;
    private bool isHandActive;
    private GameObject activeHandInstance;

    private AttackType lastAttackType = AttackType.Smash;
    private int sameAttackStreakCount = 0;

    public bool IsAttacking => isHandActive;

    private void OnValidate()
    {
        smashAttackRangeX = Mathf.Max(0f, smashAttackRangeX);
        grabAttackRangeX = Mathf.Max(0f, grabAttackRangeX);
        attackCooldown = Mathf.Max(0f, attackCooldown);
    }

    private void Update()
    {
        if (attackCooldownTimer > 0.0f)
        {
            attackCooldownTimer -= Time.deltaTime;
        }

        if (debugTriggerWithSpace && Input.GetKeyDown(KeyCode.Space))
        {
            TryStartRandomAttack();
        }
    }

    public void SetPlayerTarget(Transform player)
    {
        playerTransform = player;
    }

    public void TryStartRandomAttack()
    {
        if (isHandActive || attackCooldownTimer > 0.0f || playerTransform == null)
        {
            return;
        }

        float dx = Mathf.Abs(playerTransform.position.x - transform.position.x);

        bool canUseSmash = handSmashPrefab != null && dx <= smashAttackRangeX;
        bool canUseGrab = handGrabPrefab != null && dx <= grabAttackRangeX;

        if (!canUseSmash && !canUseGrab)
        {
            return;
        }

        AttackType selectedType;

        if (canUseSmash && !canUseGrab)
        {
            selectedType = AttackType.Smash;
        }
        else if (!canUseSmash && canUseGrab)
        {
            selectedType = AttackType.Grab;
        }
        else
        {
            selectedType = SelectNextAttackType();
        }

        bool success = selectedType == AttackType.Smash
            ? TryStartSmashAttack()
            : TryStartGrabAttack();

        if (success)
        {
            RecordAttackType(selectedType);
        }
    }

    private AttackType SelectNextAttackType()
    {
        // 同じ攻撃が2回連続したら、次は別の攻撃を使う
        if (sameAttackStreakCount >= 2)
        {
            return GetOppositeAttackType(lastAttackType);
        }

        return UnityEngine.Random.value < 0.5f ? AttackType.Smash : AttackType.Grab;
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
        if (handSmashPrefab == null)
        {
            Debug.LogWarning("[HandChaserAttackController] HandSmashAttack prefab が未設定です。", this);
            return false;
        }

        Vector3 spawnPosition = transform.position + handSpawnOffset;

        HandSmashAttack handInstance = Instantiate(handSmashPrefab, spawnPosition, Quaternion.identity);

        activeHandInstance = handInstance.gameObject;
        isHandActive = true;
        attackCooldownTimer = attackCooldown;

        handInstance.StartAttack(spawnPosition, playerTransform, groundY, OnHandAttackFinished);

        if (enableDebugLog)
        {
            Debug.Log("[HandChaserAttackController] Smash attack started.", this);
        }

        return true;
    }

    private bool TryStartGrabAttack()
    {
        if (handGrabPrefab == null)
        {
            Debug.LogWarning("[HandChaserAttackController] HandGrabAttack prefab が未設定です。", this);
            return false;
        }

        Vector3 spawnPosition = transform.position + handSpawnOffset;

        HandGrabAttack handInstance = Instantiate(handGrabPrefab, spawnPosition, Quaternion.identity);

        activeHandInstance = handInstance.gameObject;
        isHandActive = true;
        attackCooldownTimer = attackCooldown;

        handInstance.StartAttack(spawnPosition, playerTransform, OnHandAttackFinished);

        if (enableDebugLog)
        {
            Debug.Log("[HandChaserAttackController] Grab attack started.", this);
        }

        return true;
    }

    private void OnHandAttackFinished()
    {
        isHandActive = false;
        activeHandInstance = null;
    }

    public void CancelActiveAttack()
    {
        if (activeHandInstance != null)
        {
            Destroy(activeHandInstance);
            activeHandInstance = null;
        }

        isHandActive = false;
    }

    public void ResetAttackState()
    {
        CancelActiveAttack();
        attackCooldownTimer = 0.0f;
        sameAttackStreakCount = 0;
        lastAttackType = AttackType.Smash;
    }
}
