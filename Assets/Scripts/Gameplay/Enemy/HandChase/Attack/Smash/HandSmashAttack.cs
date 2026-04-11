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
        // RigidbodyをKinematicに設定
        InitializeRigidbody();

        // ヒットボックスを初期化（初期状態では無効）
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
        // 初期化
        this.spawnPosition = spawnPosition;
        this.targetPlayer = targetPlayer;
        this.groundY = groundY;
        this.onFinished = onFinished;

        // プレイヤーの現在位置を取得
        Vector3 currentTargetPosition = targetPlayer != null ? targetPlayer.position : spawnPosition;

        // 上昇目標位置を設定
        riseTargetPosition = new Vector3(
            currentTargetPosition.x,
            currentTargetPosition.y + riseHeight,
            currentTargetPosition.z
        );

        // 叩きつけ目標位置を設定
        smashTargetPosition = new Vector3(
            currentTargetPosition.x,
            groundY,
            currentTargetPosition.z
        );

        // 初期位置を設定して上昇状態を開始
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
        // 現在の状態に応じた処理を実行
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

    // 上昇フェーズ：プレイヤーの上空へ移動
    private void TickRise()
    {
        // 上昇中はプレイヤーの移動を追跡
        UpdateTargetsWhileRising();

        Vector3 next = Vector3.MoveTowards(
            transform.position,
            riseTargetPosition,
            riseSpeed * Time.deltaTime
        );

        transform.position = next;

        // 目標位置に到達したらHold状態へ遷移
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

    // 待機フェーズ：少し上昇しながら叩きつけまで待つ
    private void TickHold()
    {
        holdLiftElapsedTime += Time.deltaTime;

        // 少しずつ上昇する（イージング付き）
        float duration = holdLiftHeight / holdLiftSpeed;
        float t = Mathf.Clamp01(holdLiftElapsedTime / duration);

        float easedT = EaseInCubic(t);

        transform.position = Vector3.Lerp(holdStartPosition, holdLiftTargetPosition, easedT);

        // 待機時間が終わったらSmash状態へ遷移
        holdTimer -= Time.deltaTime;
        if (holdTimer > 0.0f)
        {
            return;
        }

        // 当たり判定を有効化
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

    // 叩きつけフェーズ：地面に向かって高速落下
    private void TickSmash()
    {
        Vector3 next = Vector3.MoveTowards(
            transform.position,
            smashTargetPosition,
            smashSpeed * Time.deltaTime
        );

        transform.position = next;

        // 地面に到達したら終了処理
        if (Vector3.Distance(transform.position, smashTargetPosition) <= reachThreshold)
        {
            transform.position = smashTargetPosition;

            // 当たり判定を無効化
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

    // 終了フェーズ：一定時間待ってからオブジェクトを破棄
    private void TickEnd()
    {
        endTimer -= Time.deltaTime;
        // タイマーが終了したら攻撃を終了
        if (endTimer > 0.0f)
        {
            return;
        }

        FinishAttack();
    }

    // 上昇中にプレイヤーの移動を追跡して目標位置を更新
    private void UpdateTargetsWhileRising()
    {
        if (targetPlayer == null)
        {
            return;
        }

        Vector3 currentTargetPosition = targetPlayer.position;

        // 上昇目標位置をプレイヤーの現在地に合わせて更新
        riseTargetPosition = new Vector3(
            currentTargetPosition.x,
            currentTargetPosition.y + riseHeight,
            currentTargetPosition.z
        );

        // 叩きつけ目標位置も同様に更新
        smashTargetPosition = new Vector3(
            currentTargetPosition.x,
            groundY,
            currentTargetPosition.z
        );
    }

    // 3乗のイージング関数（緊張感を演出）
    private float EaseInCubic(float t)
    {
        return t * t * t;
    }

    // ヒットボックスがプレイヤーを検出したときに呼ばれる
    public void NotifyPlayerHit(GameObject playerObject)
    {
        RequestPlayerDeath(playerObject);
    }
}