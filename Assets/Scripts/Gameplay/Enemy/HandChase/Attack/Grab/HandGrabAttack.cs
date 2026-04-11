using System;
using UnityEngine;

public sealed class HandGrabAttack : HandAttackBase
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

    private AttackState state = AttackState.Idle;
    private Vector3 latestPlayerPosition;
    private Vector3 approachNearTargetPosition;
    private Vector3 finalGrabTargetPosition;

    private float trackTimer = 0.0f;
    private float trackedElapsedTime = 0.0f;
    private float holdTimer = 0.0f;
    private float missPauseTimer = 0.0f;
    private float endTimer = 0.0f;

    private PlayerController grabbedPlayerController;
    private Rigidbody grabbedPlayerRigidbody;
    private bool hasGrabbedPlayer = false;

    private void Awake()
    {
        // RigidbodyをKinematicに設定
        InitializeRigidbody();
    }

    public void StartAttack(
        Vector3 spawnPosition,
        Transform targetPlayer,
        Action onFinished
    )
    {
        // 初期位置を設定
        transform.position = spawnPosition;

        this.targetPlayer = targetPlayer;
        this.onFinished = onFinished;

        // プレイヤー位置を記録
        latestPlayerPosition = targetPlayer != null ? targetPlayer.position : GetAnchorWorldPosition();
        approachNearTargetPosition = CalculateApproachNearTarget(GetAnchorWorldPosition(), latestPlayerPosition);
        finalGrabTargetPosition = latestPlayerPosition;

        // 状態をリセット
        hasGrabbedPlayer = false;
        grabbedPlayerController = null;
        grabbedPlayerRigidbody = null;

        trackTimer = 0.0f;
        trackedElapsedTime = 0.0f;
        holdTimer = 0.0f;
        missPauseTimer = 0.0f;
        endTimer = 0.0f;

        // 接近状態を開始
        state = AttackState.ApproachNear;

        if (view != null)
        {
            view.SetDefaultSorting();
            view.PlayApproachNear();
        }
    }

    private void Update()
    {
        // 現在の状態に応じた処理を実行
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
        // 掌み中はプレイヤーの位置を手に固定
        if (state == AttackState.HoldPlayer)
        {
            UpdateGrabbedPlayerPosition();
        }
    }

    // 接近フェーズ：プレイヤーの近くに移動
    private void TickApproachNear()
    {
        // プレイヤー位置を追跡
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

        // 目標位置に到達したら追跡フェーズへ遷移
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

    // 追跡フェーズ：掌み前にプレイヤーを追い続ける
    private void TickTrackBeforeGrab()
    {
        trackedElapsedTime += Time.deltaTime;

        // 一定時間はプレイヤー位置を更新
        if (trackedElapsedTime <= trackUpdateDuration)
        {
            UpdateTrackedPlayerPosition();
            finalGrabTargetPosition = latestPlayerPosition;
        }

        // 待機時間が終わったら掌み開始
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

    // 掌みフェーズ：目標地点に突進してプレイヤーを掌む
    private void TickGrabStart()
    {
        Vector3 rootTargetPosition = GetRootPositionForAnchorTarget(finalGrabTargetPosition);

        Vector3 next = Vector3.MoveTowards(
            transform.position,
            rootTargetPosition,
            grabMoveSpeed * Time.deltaTime
        );

        transform.position = next;

        // 目標地点に到達したら当たり判定
        if (Vector3.Distance(GetAnchorWorldPosition(), finalGrabTargetPosition) <= reachThreshold)
        {
            transform.position = rootTargetPosition;

            // ヒットボックス内にプレイヤーがいるか確認
            GameObject playerObject = grabHitbox != null ? grabHitbox.FindPlayerInGrabArea() : null;
            if (playerObject != null)
            {
                TryStartGrab(playerObject);
            }

            // 掌み成功か失敗かで次の状態を分岐
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

    // 掌み保持フェーズ：プレイヤーを一定時間掌んでから殺す
    private void TickHoldPlayer()
    {
        holdTimer -= Time.deltaTime;

        // プレイヤーの位置を更新
        UpdateGrabbedPlayerPosition();

        // まだ保持時間が残っていれば続ける
        if (holdTimer > 0.0f)
        {
            return;
        }

        // 時間が終了したらプレイヤーを解放して殺す
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

    // 掌み失敗フェーズ：少し間を置いてから終了
    private void TickMissPause()
    {
        missPauseTimer -= Time.deltaTime;
        // まだ待機時間が残っていれば続ける
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

    // 終了フェーズ：一定時間待ってからオブジェクトを破棄
    private void TickEnd()
    {
        endTimer -= Time.deltaTime;
        if (endTimer > 0.0f)
        {
            return;
        }

        FinishAttack();
    }

    // プレイヤーの現在位置を記録
    private void UpdateTrackedPlayerPosition()
    {
        if (targetPlayer == null)
        {
            return;
        }

        latestPlayerPosition = targetPlayer.position;
    }

    // プレイヤーの近くの接近目標位置を計算
    private Vector3 CalculateApproachNearTarget(Vector3 fromPosition, Vector3 playerPosition)
    {
        Vector3 toPlayer = playerPosition - fromPosition;
        float distance = toPlayer.magnitude;

        // 距離がほぼ0ならプレイヤーの上に配置
        if (distance <= 0.0001f)
        {
            return playerPosition + Vector3.up * nearHeightOffset;
        }

        // プレイヤーから一定距離離れた位置を計算
        Vector3 direction = toPlayer / distance;
        float offsetDistance = Mathf.Min(nearDistance, distance);

        Vector3 baseTarget = playerPosition - direction * offsetDistance;
        baseTarget += Vector3.up * nearHeightOffset;  // 高さオフセットを追加

        return baseTarget;
    }

    // 掌みアンカーのワールド位置を取得
    private Vector3 GetAnchorWorldPosition()
    {
        if (grabAnchor != null)
        {
            return grabAnchor.position;
        }

        // アンカーがない場合はルートの位置を使用
        return transform.position;
    }

    // アンカーが目標位置に来るためのルート位置を逆算
    private Vector3 GetRootPositionForAnchorTarget(Vector3 anchorTargetPosition)
    {
        // アンカーがない場合はそのまま返す
        if (grabAnchor == null)
        {
            return anchorTargetPosition;
        }

        // アンカーのオフセットを考慮してルート位置を計算
        Vector3 anchorOffset = grabAnchor.position - transform.position;
        return anchorTargetPosition - anchorOffset;
    }

    // プレイヤーを掌む処理を開始
    private void TryStartGrab(GameObject playerObject)
    {
        // 既に掌んでいる場合は何もしない
        if (hasGrabbedPlayer)
        {
            return;
        }

        // PlayerControllerを取得
        PlayerController playerController = playerObject.GetComponent<PlayerController>();
        if (playerController == null)
        {
            Debug.LogWarning("HandGrabAttack: PlayerController が見つからないため掴みを開始できませんでした。", playerObject);
            return;
        }

        // Rigidbodyを取得（位置固定に必要）
        Rigidbody playerRb = playerObject.GetComponent<Rigidbody>();
        if (playerRb == null)
        {
            Debug.LogWarning("HandGrabAttack: Player の Rigidbody が見つからないため掴み位置へ固定できませんでした。", playerObject);
            return;
        }

        // 掌み状態を記録
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
}