using System;
using UnityEngine;

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
    [SerializeField] private float holdLiftHeight = 0.4f;
    [SerializeField] private float holdLiftSpeed = 6.0f;
    [SerializeField] private float smashSpeed = 24.0f;
    [SerializeField] private float endLifeTime = 0.2f;
    [SerializeField] private float reachThreshold = 0.05f;

    [Header("References")]
    [SerializeField] private PalmHitbox palmHitbox;
    [SerializeField] private HandSmashVisualController visualController;

    private Rigidbody rigidBody;
    private AttackState state = AttackState.Idle;

    private Transform targetPlayer;
    private float groundY = 0.0f;

    private Vector3 spawnPosition;
    private Vector3 riseTargetPosition;
    private Vector3 smashTargetPosition;
    private Vector3 holdStartPosition;
    private Vector3 holdLiftTargetPosition;

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
        Transform targetPlayer,
        float groundY,
        Action onFinished
    )
    {
        this.spawnPosition = spawnPosition;
        this.targetPlayer = targetPlayer;
        this.groundY = groundY;
        this.onFinished = onFinished;

        Vector3 currentTargetPosition = targetPlayer != null ? targetPlayer.position : spawnPosition;

        riseTargetPosition = new Vector3(
            currentTargetPosition.x,
            currentTargetPosition.y + riseHeight,
            currentTargetPosition.z
        );

        smashTargetPosition = new Vector3(
            currentTargetPosition.x,
            groundY,
            currentTargetPosition.z
        );

        transform.position = this.spawnPosition;
        state = AttackState.Rise;

        if (palmHitbox != null)
        {
            palmHitbox.SetHitEnabled(false);
        }

        if (visualController != null)
        {
            visualController.PlayRise();
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
        UpdateTargetsWhileRising();

        Vector3 next = Vector3.MoveTowards(
            transform.position,
            riseTargetPosition,
            riseSpeed * Time.deltaTime
        );

        transform.position = next;

        if (Vector3.Distance(transform.position, riseTargetPosition) <= reachThreshold)
        {
            transform.position = riseTargetPosition;

            holdStartPosition = transform.position;
            holdLiftTargetPosition = holdStartPosition + Vector3.up * holdLiftHeight;

            holdTimer = holdTime;
            state = AttackState.Hold;

            if (visualController != null)
            {
                visualController.PlayHold();
            }
        }
    }

    private void TickHold()
    {
        transform.position = Vector3.MoveTowards(
            transform.position,
            holdLiftTargetPosition,
            holdLiftSpeed * Time.deltaTime
        );

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

        if (visualController != null)
        {
            visualController.PlaySmash();
        }
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

            if (visualController != null)
            {
                visualController.PlayEnd();
            }
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

    private void UpdateTargetsWhileRising()
    {
        if (targetPlayer == null)
        {
            return;
        }

        Vector3 currentTargetPosition = targetPlayer.position;

        riseTargetPosition = new Vector3(
            currentTargetPosition.x,
            currentTargetPosition.y + riseHeight,
            currentTargetPosition.z
        );

        smashTargetPosition = new Vector3(
            currentTargetPosition.x,
            groundY,
            currentTargetPosition.z
        );
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