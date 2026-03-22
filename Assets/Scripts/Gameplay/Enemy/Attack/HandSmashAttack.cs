using System;
using UnityEngine;

// 叩きつけ攻撃用の手オブジェクト。
// 本体近くから出現し、プレイヤー上空へ掲げてから真下に振り下ろす。
[RequireComponent(typeof(Rigidbody))]
public sealed class HandSmashAttack : MonoBehaviour
{
    private enum AttackState
    {
        Idle,
        Rise,
        Hold,
        Smash,
        End
    }

    [Header("Move")]
    [SerializeField] private float riseHeight = 5.0f;
    [SerializeField] private float riseSpeed = 10.0f;
    [SerializeField] private float holdTime = 0.15f;
    [SerializeField] private float smashSpeed = 24.0f;
    [SerializeField] private float endLifeTime = 0.2f;
    [SerializeField] private float reachThreshold = 0.05f;

    [Header("References")]
    [SerializeField] private PalmHitbox palmHitbox;

    private Rigidbody rigidBody;
    private AttackState state = AttackState.Idle;

    private Vector3 spawnPosition;
    private Vector3 riseTargetPosition;
    private Vector3 smashTargetPosition;

    private float holdTimer = 0.0f;
    private float endTimer = 0.0f;

    private Action onFinished;

    private void Awake()
    {
        rigidBody = GetComponent<Rigidbody>();
        rigidBody.useGravity = false;
        rigidBody.isKinematic = true;

        if (palmHitbox != null)
        {
            palmHitbox.Initialize(this);
            palmHitbox.SetHitEnabled(false);
        }
    }

    public void StartAttack(
        Vector3 spawnPosition,
        Vector3 targetPlayerPosition,
        float groundY,
        Action onFinished
    )
    {
        this.spawnPosition = spawnPosition;

        riseTargetPosition = new Vector3(
            targetPlayerPosition.x,
            targetPlayerPosition.y + riseHeight,
            targetPlayerPosition.z
        );

        smashTargetPosition = new Vector3(
            riseTargetPosition.x,
            groundY,
            riseTargetPosition.z
        );

        this.onFinished = onFinished;
        transform.position = this.spawnPosition;
        state = AttackState.Rise;

        if (palmHitbox != null)
        {
            palmHitbox.SetHitEnabled(false);
        }
    }

    private void Update()
    {
        switch (state)
        {
            case AttackState.Idle:
                break;

            case AttackState.Rise:
                TickRise();
                break;

            case AttackState.Hold:
                TickHold();
                break;

            case AttackState.Smash:
                TickSmash();
                break;

            case AttackState.End:
                TickEnd();
                break;
        }
    }

    private void TickRise()
    {
        Vector3 next = Vector3.MoveTowards(
            transform.position,
            riseTargetPosition,
            riseSpeed * Time.deltaTime
        );

        transform.position = next;

        if (Vector3.Distance(transform.position, riseTargetPosition) <= reachThreshold)
        {
            transform.position = riseTargetPosition;
            holdTimer = holdTime;
            state = AttackState.Hold;
        }
    }

    private void TickHold()
    {
        holdTimer -= Time.deltaTime;
        if (holdTimer > 0.0f)
        {
            return;
        }

        if (palmHitbox != null)
        {
            palmHitbox.SetHitEnabled(true);
        }

        state = AttackState.Smash;
    }

    private void TickSmash()
    {
        Vector3 next = Vector3.MoveTowards(
            transform.position,
            smashTargetPosition,
            smashSpeed * Time.deltaTime
        );

        transform.position = next;

        if (Vector3.Distance(transform.position, smashTargetPosition) <= reachThreshold)
        {
            transform.position = smashTargetPosition;

            if (palmHitbox != null)
            {
                palmHitbox.SetHitEnabled(false);
            }

            endTimer = endLifeTime;
            state = AttackState.End;
        }
    }

    private void TickEnd()
    {
        endTimer -= Time.deltaTime;
        if (endTimer > 0.0f)
        {
            return;
        }

        FinishAttack();
    }

    private void FinishAttack()
    {
        onFinished?.Invoke();
        Destroy(gameObject);
    }

    public void NotifyPlayerHit(GameObject playerObject)
    {
        PlayerController playerController = playerObject.GetComponent<PlayerController>();
        if (playerController != null)
        {
            playerController.RequestDamageDeath();
            return;
        }

        Debug.LogWarning("HandSmashAttack: PlayerController が見つからないため死亡要求を送れませんでした。", playerObject);
    }
}