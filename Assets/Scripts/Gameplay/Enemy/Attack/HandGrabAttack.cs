using System;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public sealed class HandGrabAttack : MonoBehaviour
{
    private enum AttackState
    {
        Idle,
        Approach,
        HoldPlayer,
        MissPause,
        End
    }

    [Header("Move")]
    [SerializeField] private float approachSpeed = 14.0f;
    [SerializeField] private float reachThreshold = 0.15f;

    [Header("Timing")]
    [SerializeField] private float holdDuration = 0.35f;
    [SerializeField] private float missPauseDuration = 0.2f;
    [SerializeField] private float endLifeTime = 0.1f;

    [Header("References")]
    [SerializeField] private GrabHitbox grabHitbox;
    [SerializeField] private Transform grabAnchor;

    private Rigidbody rigidBody;
    private AttackState state = AttackState.Idle;

    private Vector3 approachTargetPosition;
    private float holdTimer = 0.0f;
    private float missPauseTimer = 0.0f;
    private float endTimer = 0.0f;

    private Action onFinished;

    private PlayerController grabbedPlayerController;
    private Rigidbody grabbedPlayerRigidbody;
    private bool hasGrabbedPlayer = false;

    private void Awake()
    {
        rigidBody = GetComponent<Rigidbody>();
        rigidBody.useGravity = false;
        rigidBody.isKinematic = true;

        if (grabHitbox != null)
        {
            grabHitbox.Initialize(this);
            grabHitbox.SetHitEnabled(false);
        }
    }

    public void StartAttack(
        Vector3 spawnPosition,
        Vector3 targetPlayerPosition,
        Action onFinished
    )
    {
        transform.position = spawnPosition;
        approachTargetPosition = targetPlayerPosition;
        this.onFinished = onFinished;

        hasGrabbedPlayer = false;
        grabbedPlayerController = null;
        grabbedPlayerRigidbody = null;

        if (grabHitbox != null)
        {
            grabHitbox.SetHitEnabled(true);
        }

        state = AttackState.Approach;
    }

    private void Update()
    {
        switch (state)
        {
            case AttackState.Idle:
                break;

            case AttackState.Approach:
                TickApproach();
                break;

            case AttackState.HoldPlayer:
                TickHoldPlayer();
                break;

            case AttackState.MissPause:
                TickMissPause();
                break;

            case AttackState.End:
                TickEnd();
                break;
        }
    }

    private void LateUpdate()
    {
        if (state == AttackState.HoldPlayer)
        {
            UpdateGrabbedPlayerPosition();
        }
    }

    private void TickApproach()
    {
        Vector3 next = Vector3.MoveTowards(
            transform.position,
            approachTargetPosition,
            approachSpeed * Time.deltaTime
        );

        transform.position = next;

        //Vector3 moveDirection = approachTargetPosition - transform.position;
        //if (moveDirection.sqrMagnitude > 0.0001f)
        //{
        //    transform.rotation = Quaternion.LookRotation(moveDirection.normalized, Vector3.up);
        //}

        if (hasGrabbedPlayer)
        {
            if (grabHitbox != null)
            {
                grabHitbox.SetHitEnabled(false);
            }

            holdTimer = holdDuration;
            state = AttackState.HoldPlayer;
            return;
        }

        if (Vector3.Distance(transform.position, approachTargetPosition) <= reachThreshold)
        {
            transform.position = approachTargetPosition;

            if (grabHitbox != null)
            {
                grabHitbox.SetHitEnabled(false);
            }

            missPauseTimer = missPauseDuration;
            state = AttackState.MissPause;
        }
    }

    private void TickHoldPlayer()
    {
        holdTimer -= Time.deltaTime;

        UpdateGrabbedPlayerPosition();

        if (holdTimer > 0.0f)
        {
            return;
        }

        ReleaseGrabbedPlayerAndKill();
        endTimer = endLifeTime;
        state = AttackState.End;
    }

    private void TickMissPause()
    {
        missPauseTimer -= Time.deltaTime;
        if (missPauseTimer > 0.0f)
        {
            return;
        }

        endTimer = endLifeTime;
        state = AttackState.End;
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

    public void NotifyGrabHit(GameObject playerObject)
    {
        if (hasGrabbedPlayer)
        {
            return;
        }

        PlayerController playerController = playerObject.GetComponent<PlayerController>();
        if (playerController == null)
        {
            Debug.LogWarning("HandGrabAttack: PlayerController が見つからないため掴みを開始できませんでした。", playerObject);
            return;
        }

        Rigidbody playerRb = playerObject.GetComponent<Rigidbody>();
        if (playerRb == null)
        {
            Debug.LogWarning("HandGrabAttack: Player の Rigidbody が見つからないため掴み位置へ固定できませんでした。", playerObject);
            return;
        }

        grabbedPlayerController = playerController;
        grabbedPlayerRigidbody = playerRb;
        hasGrabbedPlayer = true;
    }

    private void UpdateGrabbedPlayerPosition()
    {
        if (!hasGrabbedPlayer)
        {
            return;
        }

        if (grabbedPlayerRigidbody == null || grabAnchor == null)
        {
            return;
        }

        grabbedPlayerRigidbody.linearVelocity = Vector3.zero;
        grabbedPlayerRigidbody.angularVelocity = Vector3.zero;
        grabbedPlayerRigidbody.position = grabAnchor.position;
    }

    private void ReleaseGrabbedPlayerAndKill()
    {
        if (!hasGrabbedPlayer)
        {
            return;
        }

        if (grabbedPlayerRigidbody != null)
        {
            grabbedPlayerRigidbody.linearVelocity = Vector3.zero;
            grabbedPlayerRigidbody.angularVelocity = Vector3.zero;
        }

        PlayerController playerController = grabbedPlayerController;

        grabbedPlayerController = null;
        grabbedPlayerRigidbody = null;
        hasGrabbedPlayer = false;

        if (playerController != null)
        {
            playerController.RequestDamageDeath();
        }
    }

    private void FinishAttack()
    {
        onFinished?.Invoke();
        Destroy(gameObject);
    }
}