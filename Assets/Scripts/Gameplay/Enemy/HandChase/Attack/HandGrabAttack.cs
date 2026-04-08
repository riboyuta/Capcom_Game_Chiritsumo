using System;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public sealed class HandGrabAttack : MonoBehaviour
{
    private enum AttackState
    {
        Idle,
        ApproachNear,
        TrackBeforeGrab,
        GrabStart,
        HoldPlayer,
        MissPause,
        End
    }

    [Header("Move")]
    [Tooltip("プレイヤーの近くへ接近する際の移動速度")]
    [SerializeField] private float approachNearSpeed = 12.0f;
    [Tooltip("掴み攻撃時の突進速度")]
    [SerializeField] private float grabMoveSpeed = 18.0f;
    [Tooltip("プレイヤーの近くに位置取りする際の距離")]
    [SerializeField] private float nearDistance = 1.25f;
    [Tooltip("接近位置のプレイヤーからの高さオフセット")]
    [SerializeField] private float nearHeightOffset = 0.75f;
    [Tooltip("目標位置への到達判定距離")]
    [SerializeField] private float reachThreshold = 0.08f;

    [Header("Timing")]
    [Tooltip("掴み攻撃前に待機する時間")]
    [SerializeField] private float preGrabTrackDuration = 0.5f;
    [Tooltip("待機中にプレイヤー位置を更新する時間")]
    [SerializeField] private float trackUpdateDuration = 0.2f;
    [Tooltip("プレイヤーを掴んでいる時間")]
    [SerializeField] private float holdDuration = 0.35f;
    [Tooltip("掴み失敗時の待機時間")]
    [SerializeField] private float missPauseDuration = 0.2f;
    [Tooltip("攻撃終了後の生存時間")]
    [SerializeField] private float endLifeTime = 0.1f;

    [Header("References")]
    [Tooltip("掴み判定用のヒットボックス")]
    [SerializeField] private GrabHitbox grabHitbox;
    [Tooltip("プレイヤーを掴む位置のアンカー")]
    [SerializeField] private Transform grabAnchor;
    [Tooltip("手のビジュアル表示コンポーネント")]
    [SerializeField] private HandGrabView view;

    private Rigidbody rigidBody;
    private AttackState state = AttackState.Idle;

    private Transform targetPlayer;
    private Vector3 latestPlayerPosition;
    private Vector3 approachNearTargetPosition;
    private Vector3 finalGrabTargetPosition;

    private float trackTimer = 0.0f;
    private float trackedElapsedTime = 0.0f;
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
    }

    public void StartAttack(
        Vector3 spawnPosition,
        Transform targetPlayer,
        Action onFinished
    )
    {
        transform.position = spawnPosition;

        this.targetPlayer = targetPlayer;
        this.onFinished = onFinished;

        latestPlayerPosition = targetPlayer != null ? targetPlayer.position : GetAnchorWorldPosition();
        approachNearTargetPosition = CalculateApproachNearTarget(GetAnchorWorldPosition(), latestPlayerPosition);
        finalGrabTargetPosition = latestPlayerPosition;

        hasGrabbedPlayer = false;
        grabbedPlayerController = null;
        grabbedPlayerRigidbody = null;

        trackTimer = 0.0f;
        trackedElapsedTime = 0.0f;
        holdTimer = 0.0f;
        missPauseTimer = 0.0f;
        endTimer = 0.0f;

        state = AttackState.ApproachNear;

        if (view != null)
        {
            view.SetDefaultSorting();
            view.PlayApproachNear();
        }
    }

    private void Update()
    {
        switch (state)
        {
            case AttackState.Idle:
                break;

            case AttackState.ApproachNear:
                TickApproachNear();
                break;

            case AttackState.TrackBeforeGrab:
                TickTrackBeforeGrab();
                break;

            case AttackState.GrabStart:
                TickGrabStart();
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

    private void TickApproachNear()
    {
        UpdateTrackedPlayerPosition();

        Vector3 currentAnchorPosition = GetAnchorWorldPosition();
        approachNearTargetPosition = CalculateApproachNearTarget(currentAnchorPosition, latestPlayerPosition);

        Vector3 rootTargetPosition = GetRootPositionForAnchorTarget(approachNearTargetPosition);

        Vector3 next = Vector3.MoveTowards(
            transform.position,
            rootTargetPosition,
            approachNearSpeed * Time.deltaTime
        );

        transform.position = next;

        if (Vector3.Distance(GetAnchorWorldPosition(), approachNearTargetPosition) <= reachThreshold)
        {
            trackTimer = preGrabTrackDuration;
            trackedElapsedTime = 0.0f;
            state = AttackState.TrackBeforeGrab;

            if (view != null)
            {
                view.PlayTrackBeforeGrab();
            }
        }
    }

    private void TickTrackBeforeGrab()
    {
        trackedElapsedTime += Time.deltaTime;

        if (trackedElapsedTime <= trackUpdateDuration)
        {
            UpdateTrackedPlayerPosition();
            finalGrabTargetPosition = latestPlayerPosition;
        }

        trackTimer -= Time.deltaTime;
        if (trackTimer > 0.0f)
        {
            return;
        }

        state = AttackState.GrabStart;

        if (view != null)
        {
            view.PlayGrabStart();
        }
    }

    private void TickGrabStart()
    {
        Vector3 rootTargetPosition = GetRootPositionForAnchorTarget(finalGrabTargetPosition);

        Vector3 next = Vector3.MoveTowards(
            transform.position,
            rootTargetPosition,
            grabMoveSpeed * Time.deltaTime
        );

        transform.position = next;

        if (Vector3.Distance(GetAnchorWorldPosition(), finalGrabTargetPosition) <= reachThreshold)
        {
            transform.position = rootTargetPosition;

            GameObject playerObject = grabHitbox != null ? grabHitbox.FindPlayerInGrabArea() : null;
            if (playerObject != null)
            {
                TryStartGrab(playerObject);
            }

            if (hasGrabbedPlayer)
            {
                holdTimer = holdDuration;
                state = AttackState.HoldPlayer;

                if (view != null)
                {
                    view.SetGrabbedSorting();
                    view.PlayHoldPlayer();
                }
            }
            else
            {
                missPauseTimer = missPauseDuration;
                state = AttackState.MissPause;

                if (view != null)
                {
                    view.PlayMissPause();
                }
            }
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

        if (view != null)
        {
            view.SetDefaultSorting();
        }

        endTimer = endLifeTime;
        state = AttackState.End;

        if (view != null)
        {
            view.PlayEnd();
        }
    }

    private void TickMissPause()
    {
        missPauseTimer -= Time.deltaTime;
        if (missPauseTimer > 0.0f)
        {
            return;
        }

        if (view != null)
        {
            view.SetDefaultSorting();
        }

        endTimer = endLifeTime;
        state = AttackState.End;

        if (view != null)
        {
            view.PlayEnd();
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

    private void UpdateTrackedPlayerPosition()
    {
        if (targetPlayer == null)
        {
            return;
        }

        latestPlayerPosition = targetPlayer.position;
    }

    private Vector3 CalculateApproachNearTarget(Vector3 fromPosition, Vector3 playerPosition)
    {
        Vector3 toPlayer = playerPosition - fromPosition;
        float distance = toPlayer.magnitude;

        if (distance <= 0.0001f)
        {
            return playerPosition + Vector3.up * nearHeightOffset;
        }

        Vector3 direction = toPlayer / distance;
        float offsetDistance = Mathf.Min(nearDistance, distance);

        Vector3 baseTarget = playerPosition - direction * offsetDistance;
        baseTarget += Vector3.up * nearHeightOffset;

        return baseTarget;
    }

    private Vector3 GetAnchorWorldPosition()
    {
        if (grabAnchor != null)
        {
            return grabAnchor.position;
        }

        return transform.position;
    }

    private Vector3 GetRootPositionForAnchorTarget(Vector3 anchorTargetPosition)
    {
        if (grabAnchor == null)
        {
            return anchorTargetPosition;
        }

        Vector3 anchorOffset = grabAnchor.position - transform.position;
        return anchorTargetPosition - anchorOffset;
    }

    private void TryStartGrab(GameObject playerObject)
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