using System;
using UnityEngine;

public sealed class HandSmashAttack : HandAttackBase
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
    [Tooltip("上昇時の目標高さ（プレイヤーの位置からの高さ）")]
    [SerializeField] private float riseHeight = 5.0f;
    [Tooltip("上昇時の移動速度")]
    [SerializeField] private float riseSpeed = 10.0f;
    [Tooltip("Hold状態の待機時間")]
    [SerializeField] private float holdTime = 0.15f;
    [Tooltip("Hold状態での上昇高さ")]
    [SerializeField] private float holdLiftHeight = 0.4f;
    [Tooltip("Hold状態での上昇速度")]
    [SerializeField] private float holdLiftSpeed = 6.0f;
    [Tooltip("叩きつけ時の落下速度")]
    [SerializeField] private float smashSpeed = 24.0f;
    [Tooltip("攻撃終了後の生存時間")]
    [SerializeField] private float endLifeTime = 0.2f;
    [Tooltip("目標位置への到達判定距離")]
    [SerializeField] private float reachThreshold = 0.05f;

    [Header("References")]
    [Tooltip("叩きつけ攻撃の当たり判定")]
    [SerializeField] private SmashHitBox smashHitBox;
    [Tooltip("手のビジュアル表示コンポーネント")]
    [SerializeField] private HandSmashView view;

    [Header("Camera Shake")]
    [Tooltip("地面に到達した時に発生させるカメラ振動のプロファイル")]
    [SerializeField] private Capcom_Game_Chiritsumo.Camera.CameraShake.CameraShakeProfile smashShakeProfile;

    private AttackState state = AttackState.Idle;
    private float groundY;

    private Vector3 spawnPosition;
    private Vector3 riseTargetPosition;
    private Vector3 smashTargetPosition;
    private Vector3 holdStartPosition;
    private Vector3 holdLiftTargetPosition;

    private float holdTimer = 0.0f;
    private float holdLiftElapsedTime = 0.0f;
    private float endTimer = 0.0f;

    private void Awake()
    {
        InitializeRigidbody();

        if (smashHitBox != null)
        {
            smashHitBox.Initialize(this);
            smashHitBox.SetHitEnabled(false);
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

        if (smashHitBox != null)
        {
            smashHitBox.SetHitEnabled(false);
        }

        if (view != null)
        {
            view.PlayRise();
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
            holdLiftElapsedTime = 0.0f;
            state = AttackState.Hold;

            if (view != null)
            {
                view.PlayHold();
            }
        }
    }

    private void TickHold()
    {
        holdLiftElapsedTime += Time.deltaTime;

        float duration = holdLiftHeight / holdLiftSpeed;
        float t = Mathf.Clamp01(holdLiftElapsedTime / duration);

        float easedT = EaseInCubic(t);

        transform.position = Vector3.Lerp(holdStartPosition, holdLiftTargetPosition, easedT);

        holdTimer -= Time.deltaTime;
        if (holdTimer > 0.0f)
        {
            return;
        }

        if (smashHitBox != null)
        {
            smashHitBox.SetHitEnabled(true);
        }

        state = AttackState.Smash;

        if (view != null)
        {
            view.PlaySmash();
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

            if (smashHitBox != null)
            {
                smashHitBox.SetHitEnabled(false);
            }

            // カメラシェイク処理を追加 (プロファイルが設定されていれば揺らす)
            if (smashShakeProfile != null)
            {
                Capcom_Game_Chiritsumo.Camera.CameraShake.CameraShakeManager.Instance?.ExecuteImpulseShake(smashShakeProfile);
            }

            endTimer = endLifeTime;
            state = AttackState.End;

            if (view != null)
            {
                view.PlayEnd();
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

    private float EaseInCubic(float t)
    {
        return t * t * t;
    }

    public void NotifyPlayerHit(GameObject playerObject)
    {
        RequestPlayerDeath(playerObject);
    }
}