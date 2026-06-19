using UnityEngine;

// ソナーでプレイヤーを探知し、確定方向へ高速突進する敵の制御クラス。
// Follow → Alert → LockConfirm → Charge → Rebound → Stun のサイクルを繰り返す。
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(SonarChargerMovement))]
[RequireComponent(typeof(SonarChargerSonarDetector))]
[RequireComponent(typeof(SonarChargerPlayerMotionDetector))]
[RequireComponent(typeof(SonarChargerView))]
[RequireComponent(typeof(SonarChargerChargeWarningView))]
public sealed class SonarChargerEnemy : MonoBehaviour, IRespawnResettable
{
    // =========================================================
    // 内部ステート
    // =========================================================

    private enum SonarChargerState
    {
        Idle,        // 非アクティブ
        Follow,      // プレイヤーを追跡しながらソナーで探知
        Alert,       // プレイヤー検知後の溜め演出
        LockConfirm, // 突進方向確定後の短い重み
        Charge,      // 高速突進
        Rebound,     // カメラ端に当たった後の跳ね返り
        Stun,        // 跳ね返り後の硬直
    }

    // =========================================================
    // インスペクター設定
    // =========================================================

    [Header("設定")]
    [Tooltip("SonarChargerEnemy の設定値です。")]
    [SerializeField] private SonarChargerSettings settings = new SonarChargerSettings();

    [Header("対象プレイヤー")]
    [Tooltip("追跡・探知・接触時の対象プレイヤーです。未設定時は Player タグから探します。")]
    [SerializeField] private PlayerController targetPlayer;

    [Header("プレイヤーカメラ")]
    [Tooltip("突進停止に使う playerCamera です。未設定時はシーンから探します。")]
    [SerializeField] private PlayerCameraController playerCameraController;

    [Header("表示コンポーネント")]
    [SerializeField] private SonarChargerMovement movement;
    [SerializeField] private SonarChargerSonarDetector sonarDetector;
    [SerializeField] private SonarChargerPlayerMotionDetector playerMotionDetector;
    [SerializeField] private SonarChargerView view;
    [SerializeField] private SonarChargerChargeWarningView chargeWarningView;

    // =========================================================
    // ランタイム状態
    // =========================================================

    // キャッシュ済み Unity コンポーネント
    private Rigidbody rb;
    private Collider bodyCollider;

    // 現在の状態
    private SonarChargerState state = SonarChargerState.Idle;
    private float stateTimer;
    private Vector3 alertTargetPosition;
    private Vector3 lockedChargeTargetPosition;

    // 突進警告ラインの終端座標（Charge 開始時に確定する）
    private Vector3 chargeWarningEndPosition;
    private bool hasChargeWarningEndPosition;

    // アクティベーション管理
    private bool isActivated;
    private bool isDisabled;

    // 画面外から画面内へ復帰移動しているか
    private bool isRecoveringToView;

    // 通常追跡中に画面外へ留まっている時間
    private float offscreenTimer;

    // プレイヤーが最後に移動していた有効な方向
    private Vector3 lastPlayerTravelDirection;

    // プレイヤー移動方向の平滑化速度
    private const float PlayerTravelDirectionSmoothing = 10.0f;

    // プレイヤーが移動中と判断する最低速度
    private const float MinimumPlayerTravelSpeed = 0.1f;

    // 復帰目標をカメラ中央へ寄せる割合
    private const float RecoveryCameraCenterBias = 0.25f;

    // 復帰目標を完了判定境界より内側へ配置するための追加余白
    private const float RecoveryTargetExtraInset = 0.1f;

    // 初期状態保持（リスポーン用）
    private bool hasCapturedInitialState;
    private Vector3 initialPosition;
    private Quaternion initialRotation;

    // =========================================================
    // 公開プロパティ
    // =========================================================

    public bool IsActivated => isActivated;
    public bool IsCharging => state == SonarChargerState.Charge;
    public Vector3 CurrentChargeDirection => movement != null ? movement.ChargeDirection : Vector3.zero;

    // settings が null の場合は新規インスタンスを返す保険プロパティ
    private SonarChargerSettings Settings => settings ?? (settings = new SonarChargerSettings());

    // =========================================================
    // Unity ライフサイクル
    // =========================================================

    // インスペクターからコンポーネント参照をリセットする
    private void Reset()
    {
        movement = GetComponent<SonarChargerMovement>();
        sonarDetector = GetComponent<SonarChargerSonarDetector>();
        playerMotionDetector = GetComponent<SonarChargerPlayerMotionDetector>();
        view = GetComponent<SonarChargerView>();
        chargeWarningView = GetComponent<SonarChargerChargeWarningView>();
    }

    private void Awake()
    {
        InitializeComponents();
        InitializeRigidbody();
        InitializeCollider();
        ResolveReferences();

        movement.Initialize(rb);
        view.Initialize(transform);

        if (chargeWarningView != null)
        {
            chargeWarningView.Initialize();
            chargeWarningView.Hide();
        }

        CaptureInitialState();
        ResetToIdleState();
    }

    private void Start()
    {
        // startActive が有効なら即座に追跡を開始する
        if (Settings.startActive)
            BeginChase();
    }

    private void Update()
    {
        // 無効化またはビアクティブな場合は何もしない
        if (isDisabled || !isActivated)
            return;

        // プレイヤーとカメラの参照を補完する（未設定の場合はシーンから検索）
        ResolvePlayerIfNeeded();
        ResolveCameraIfNeeded();

        if (targetPlayer == null)
            return;

        float deltaTime = Time.deltaTime;

        // ソナーの発信タイマーは、
        // Follow以外の攻撃状態や画面内復帰中でも進める
        sonarDetector.TickInterval(
            Settings,
            deltaTime);

        // プレイヤーの移動状態を毎フレーム更新する
        playerMotionDetector.Tick(
            targetPlayer,
            Settings);

        // 現在の状態に応じた更新処理を実行する
        switch (state)
        {
            case SonarChargerState.Follow: TickFollow(deltaTime); break;
            case SonarChargerState.Alert: TickAlert(deltaTime); break;
            case SonarChargerState.LockConfirm: TickLockConfirm(deltaTime); break;
            case SonarChargerState.Charge: TickCharge(deltaTime); break;
            case SonarChargerState.Rebound: TickRebound(deltaTime); break;
            case SonarChargerState.Stun: TickStun(deltaTime); break;
        }
    }

    // =========================================================
    // 公開 API
    // =========================================================

    // RoomEnemySystem から渡された設定値を一括コピーして各コンポーネントに反映する
    public void ApplySettings(SonarChargerSettings nextSettings)
    {
        if (nextSettings == null)
            return;

        // インスタンスを差し替えるのではなく値をコピーする
        Settings.CopyFrom(nextSettings);

        ResolveComponents();
        ResolveReferences();

        // Follow / Alert / Charge 中にリセットすると挙動が変わるため、Idle 時だけ再初期化する
        if (Application.isPlaying && state == SonarChargerState.Idle)
        {
            ResetDetectors();
            ApplyActivationVisualState();
            HideChargeWarning();

            if (Settings.startActive && !isActivated && !isDisabled)
                BeginChase();
        }
    }

    // 敵を起動し、Follow 状態でプレイヤーの追跡を開始する
    public void BeginChase()
    {
        // 既に起動済みの場合は無視する
        if (isActivated)
        {
            LogDebug("Already activated.");
            return;
        }

        HideChargeWarning();

        isDisabled = false;
        isActivated = true;

        ClearRigidbodyVelocity();
        ResetDetectors();

        ApplyActivationVisualState();
        HideChargeWarning();
        ChangeState(SonarChargerState.Follow);
        view.PlayFollow();

        LogDebug("BeginChase.");
    }

    // 指定スポーン位置へワープしてから BeginChase を呼ぶ
    public void BeginChase(Vector3 spawnPosition, Quaternion spawnRotation)
    {
        WarpTo(spawnPosition, spawnRotation);
        BeginChase();
    }

    // 敵を無効化し、Idle 状態へ戻す
    public void DisableSelf()
    {
        HideChargeWarning();

        isDisabled = true;
        isActivated = false;
        ClearRigidbodyVelocity();
        ResetToIdleState();
    }

    // 現在の状態をリスポーン基準として記録する（2 回目以降は無視）
    public void CaptureInitialState()
    {
        if (hasCapturedInitialState)
            return;

        initialPosition = transform.position;
        initialRotation = transform.rotation;

        movement.CaptureInitialState();
        sonarDetector.ResetDetector(Settings);
        playerMotionDetector.ResetDetector(targetPlayer);
        view.CaptureInitialState();

        hasCapturedInitialState = true;
    }

    // IRespawnResettable 実装
    public void ResetToRespawnState()
    {
        ResetEncounterForRespawn();
    }

    // リスポーン時に敵を初期状態にリセットする
    public void ResetEncounterForRespawn()
    {
        HideChargeWarning();

        // 初期位置・回転にワープする
        WarpTo(initialPosition, initialRotation);
        ClearRigidbodyVelocity();

        isDisabled = false;
        isActivated = false;

        ResetAllComponents();
        ResetToIdleState();

        // startActive が有効ならリスポーン後に再起動する
        if (Settings.startActive)
            BeginChase();
    }

    // =========================================================
    // 状態別 Tick
    // =========================================================

    // Follow：プレイヤーを追跡しながらソナーで探知する
    private void TickFollow(float deltaTime)
    {
        view.ResetVisualOffset();

        UpdatePlayerTravelDirection(deltaTime);
        UpdateOffscreenTimer(deltaTime);

        Vector3 playerPosition =
            targetPlayer.transform.position;

        Vector3 toPlayer =
            playerPosition -
            transform.position;

        toPlayer.z = 0.0f;

        float distanceToPlayer =
            toPlayer.magnitude;

        if (TryTickViewRecovery(
            distanceToPlayer,
            deltaTime))
        {
            return;
        }

        TickNormalFollow(
            playerPosition,
            toPlayer,
            distanceToPlayer,
            deltaTime);
    }

    // 必要な場合に画面外復帰を更新する
    private bool TryTickViewRecovery(
        float distanceToPlayer,
        float deltaTime)
    {
        if (!ShouldRecoverToCurrentView(
            distanceToPlayer))
        {
            EndViewRecovery();
            return false;
        }

        BeginViewRecovery();

        Vector3 recoveryTarget =
            CalculateRecoveryTargetPosition();

        Vector3 recoveryDirection =
            recoveryTarget -
            transform.position;

        recoveryDirection.z = 0.0f;

        movement.TickFollow(
            recoveryTarget,
            Settings.recoverySpeed,
            deltaTime);

        view.ApplyDirection(
            recoveryDirection);

        // 移動後の位置で復帰完了を再判定する。
        // 次フレームの境界判定だけに任せず、復帰状態を確実に終了させる。
        if (IsInsideCurrentView(
            Settings.recoveryCameraPadding))
        {
            EndViewRecovery();
        }

        // このフレームは復帰処理として終了し、
        // 通常追跡とソナーは次フレームから再開する。
        return true;
    }

    // 通常時のプレイヤー追跡と攻撃判定を更新する
    private void TickNormalFollow(
        Vector3 playerPosition,
        Vector3 toPlayer,
        float distanceToPlayer,
        float deltaTime)
    {
        float followSpeed =
            CalculateFollowSpeed(
                distanceToPlayer);

        movement.TickFollow(
            playerPosition,
            followSpeed,
            deltaTime);

        view.ApplyDirection(
            toPlayer);

        // 画面外にいる間は新しい攻撃を開始しない
        if (!IsInsideCurrentView(0.0f))
        {
            sonarDetector.CancelPulseForViewRecovery(Settings);

            playerMotionDetector.SyncDashInputBaseline(targetPlayer);

            return;
        }

        // ダッシュ入力による即時Alert
        if (TryStartAlertByDashInput())
        {
            return;
        }

        // ソナーによるAlert
        if (sonarDetector.TickSonar(
            targetPlayer,
            playerMotionDetector,
            Settings,
            deltaTime,
            out Vector3 detectedPosition))
        {
            StartAlert(detectedPosition);
        }
    }

    // プレイヤーとの距離に応じた追跡速度を計算する
    private float CalculateFollowSpeed(float distanceToPlayer)
    {
        float normalSpeed =
            Mathf.Max(0.0f, Settings.followSpeed);

        float maxSpeed =
            Mathf.Max(
                normalSpeed,
                Settings.catchUpMaxSpeed);

        float startDistance =
            Mathf.Max(
                0.0f,
                Settings.catchUpStartDistance);

        float maxDistance =
            Mathf.Max(
                startDistance,
                Settings.catchUpMaxDistance);

        // 加速開始距離以内なら通常速度
        if (distanceToPlayer <= startDistance)
        {
            return normalSpeed;
        }

        // 開始距離と最大距離が同じ場合は即座に最大速度へ切り替える
        if (maxDistance <= startDistance)
        {
            return maxSpeed;
        }

        float distanceRate =
            Mathf.InverseLerp(
                startDistance,
                maxDistance,
                distanceToPlayer);

        return Mathf.Lerp(
            normalSpeed,
            maxSpeed,
            distanceRate);
    }

    // 状態遷移：Follow → Alert
    private void StartAlert(Vector3 detectedPosition)
    {
        alertTargetPosition = detectedPosition;
        sonarDetector.CancelPulse();
        StopMovementAndResetView();

        HideChargeWarning();

        if (chargeWarningView != null)
            chargeWarningView.SetTracking();

        ChangeState(SonarChargerState.Alert);
        view.PlayAlert();
        LogDebug("Player detected. Alert started.");
    }

    // Alert：プレイヤーを低速追跡しながら突進を溜める
    private void TickAlert(float deltaTime)
    {
        stateTimer += deltaTime;

        // Alert中はプレイヤーの現在位置を追跡対象にする
        if (targetPlayer != null)
        {
            alertTargetPosition =
                targetPlayer.transform.position;
        }

        Vector3 toTarget =
            alertTargetPosition -
            transform.position;

        toTarget.z = 0.0f;

        float distanceToTarget =
            toTarget.magnitude;

        float minimumDistance =
            Mathf.Max(
                0.0f,
                Settings.alertMinimumDistance);

        float alertFollowSpeed =
            Mathf.Max(
                0.0f,
                Settings.followSpeed) *
            Mathf.Clamp01(
                Settings.alertFollowSpeedMultiplier);

        // 最低距離より遠い場合だけ、Alert中も低速で追跡する
        if (distanceToTarget > minimumDistance &&
            alertFollowSpeed > 0.0f)
        {
            movement.TickFollow(
                alertTargetPosition,
                alertFollowSpeed,
                deltaTime);
        }
        else
        {
            movement.StopImmediate();
        }

        // 移動後の位置を基準に向きを更新する
        Vector3 alertDirection =
            alertTargetPosition -
            transform.position;

        alertDirection.z = 0.0f;

        view.ApplyDirection(
            alertDirection);

        view.TickAlert(
            stateTimer,
            Settings);

        UpdateChargeWarning();

        // 溜め完了時点のプレイヤー位置で方向を確定する
        if (stateTimer >= Settings.alertTime)
        {
            StartLockConfirm();
        }
    }

    // ダッシュ入力を検知して即時 Alert を起動する
    private bool TryStartAlertByDashInput()
    {
        if (targetPlayer == null || playerMotionDetector == null)
            return false;

        // 機能が無効化されている場合はベースラインを最新化してから false を返す
        if (!Settings.enableDashInputAlertTrigger)
        {
            playerMotionDetector.SyncDashInputBaseline(targetPlayer);
            return false;
        }

        if (!playerMotionDetector.ConsumeDashInputTrigger(targetPlayer))
            return false;

        // ダッシュ入力を検知した：即座に Alert へ遷移する
        StartAlert(targetPlayer.transform.position);
        return true;
    }

    // 状態遷移：Alert → LockConfirm
    private void StartLockConfirm()
    {
        // この瞬間の目標位置で突進方向を確定する
        lockedChargeTargetPosition = alertTargetPosition;

        if (chargeWarningView != null)
            chargeWarningView.SetLocked();

        movement.StopImmediate();
        view.ResetVisualOffset();

        Vector3 lockedDirection = lockedChargeTargetPosition - transform.position;
        lockedDirection.z = 0.0f;
        view.ApplyDirection(lockedDirection);

        UpdateChargeWarningForTarget(lockedChargeTargetPosition, 1.0f);

        ChangeState(SonarChargerState.LockConfirm);
        view.PlayAlert();
        LogDebug("Charge direction locked.");
    }

    // LockConfirm：突進方向確定後の短い重み
    private void TickLockConfirm(float deltaTime)
    {
        stateTimer += deltaTime;
        movement.StopImmediate();

        Vector3 lockedDirection = lockedChargeTargetPosition - transform.position;
        lockedDirection.z = 0.0f;
        view.ApplyDirection(lockedDirection);

        // プレイヤーを追わず、確定方向の予測表示を維持する
        UpdateChargeWarningForTarget(lockedChargeTargetPosition, 1.0f);

        if (stateTimer >= Settings.lockConfirmTime)
            StartCharge();
    }

    // 状態遷移：LockConfirm → Charge
    private void StartCharge()
    {
        view.ResetVisualOffset();

        movement.StartCharge(
            lockedChargeTargetPosition,
            playerCameraController,
            Settings);

        if (chargeWarningView != null)
            chargeWarningView.SetCharging();

        view.ApplyDirection(movement.ChargeDirection);

        // Charge 開始時に警告ラインの終端を確定して固定する
        PrepareChargeWarningForCharge();

        ChangeState(SonarChargerState.Charge);
        view.PlayCharge();
        LogDebug($"Charge started. dir={movement.ChargeDirection}");
    }

    // Charge：高速突進
    private void TickCharge(float deltaTime)
    {
        stateTimer += deltaTime;

        // 突進移動を更新し、カメラ端に到達したかチェックする
        bool reachedCameraBoundary = movement.TickCharge(Settings, deltaTime);

        if (reachedCameraBoundary)
        {
            StartRebound();
            return;
        }

        UpdateChargeWarningDuringCharge();
    }

    // 状態遷移：Charge → Rebound
    private void StartRebound()
    {
        HideChargeWarning();
        movement.StartRebound(Settings);
        ChangeState(SonarChargerState.Rebound);
        view.PlayRebound();
        LogDebug("Rebound started.");
    }

    // Rebound：跳ね返り
    private void TickRebound(float deltaTime)
    {
        // 跳ね返りアニメーションを更新し、完了したら Stun へ遷移する
        if (movement.TickRebound(Settings, deltaTime))
        {
            movement.StopImmediate();
            ChangeState(SonarChargerState.Stun);
            view.PlayStun();
        }
    }

    // Stun：跳ね返り後の硬直
    private void TickStun(float deltaTime)
    {
        stateTimer += deltaTime;
        StopMovementAndResetView();

        if (stateTimer < Settings.stunTime)
        {
            return;
        }

        bool isOutsideCurrentView = !IsInsideCurrentView(0.0f);

        if (isOutsideCurrentView)
        {
            BeginViewRecovery();
        }

        ChangeState(
            SonarChargerState.Follow);

        view.PlayFollow();
    }

    // =========================================================
    // 突進警告ライン更新
    // =========================================================

    // Alert 中：alertTargetPosition に向けた警告ラインを更新する
    private void UpdateChargeWarning()
    {
        UpdateChargeWarningForTarget(alertTargetPosition, GetAlertProgress01());
    }

    // 指定ターゲット位置・進捗で警告ラインを更新する
    private void UpdateChargeWarningForTarget(Vector3 targetPosition, float progress)
    {
        if (chargeWarningView == null)
            return;

        if (!Settings.showAlertPredictionLine)
        {
            chargeWarningView.Hide();
            return;
        }

        Vector3 start = transform.position;
        Vector3 direction = movement.BuildChargeDirectionTo(targetPosition, Settings);
        Vector3 end = movement.BuildPredictionEndPosition(start, direction, playerCameraController, Settings);

        chargeWarningView.UpdateWarning(start, end, Mathf.Clamp01(progress), stateTimer, Settings);
    }

    // Alert 進捗を 0?1 で返す
    private float GetAlertProgress01()
    {
        if (Settings.alertTime <= 0.0f)
            return 1.0f;

        return Mathf.Clamp01(stateTimer / Settings.alertTime);
    }

    // Charge 開始時に警告ラインの終端を確定して hasChargeWarningEndPosition を立てる
    private void PrepareChargeWarningForCharge()
    {
        hasChargeWarningEndPosition = false;

        if (chargeWarningView == null)
            return;

        if (!Settings.showAlertPredictionLine)
        {
            chargeWarningView.Hide();
            return;
        }

        Vector3 start = transform.position;
        Vector3 direction = movement.ChargeDirection;
        direction.z = 0.0f;

        // ChargeDirection が未確定の場合は目標位置から算出する
        if (direction.sqrMagnitude <= 0.0001f)
            direction = movement.BuildChargeDirectionTo(lockedChargeTargetPosition, Settings);

        chargeWarningEndPosition = movement.BuildPredictionEndPosition(start, direction, playerCameraController, Settings);
        hasChargeWarningEndPosition = true;

        chargeWarningView.UpdateWarning(start, chargeWarningEndPosition, 1.0f, 0.0f, Settings);
    }

    // Charge中：確定済みの終端座標を基に警告帯を更新する
    private void UpdateChargeWarningDuringCharge()
    {
        if (chargeWarningView == null)
        {
            return;
        }

        if (!Settings.showAlertPredictionLine ||
            !hasChargeWarningEndPosition)
        {
            chargeWarningView.Hide();
            return;
        }

        Vector3 start = transform.position;
        Vector3 end = chargeWarningEndPosition;

        Vector3 remaining = end - start;
        remaining.z = 0.0f;

        const float EndThreshold = 0.05f;

        bool reachedEnd =
            remaining.sqrMagnitude
            <= EndThreshold * EndThreshold;

        // remainingが突進方向と逆向きになった場合、
        // 敵が帯の終点を通り過ぎたと判断する。
        bool passedEnd =
            Vector3.Dot(
                remaining,
                movement.ChargeDirection)
            <= 0.0f;

        if (reachedEnd || passedEnd)
        {
            HideChargeWarning();
            return;
        }

        // 始点を現在の敵位置へ更新することで、
        // 敵が通過した部分の帯が短くなっていく。
        chargeWarningView.UpdateWarning(
            start,
            end,
            1.0f,
            stateTimer,
            Settings);
    }

    // 警告ラインを非表示にして終端フラグをリセットする
    private void HideChargeWarning()
    {
        hasChargeWarningEndPosition = false;

        if (chargeWarningView != null)
            chargeWarningView.Hide();
    }

    // =========================================================
    // 状態遷移ヘルパー
    // =========================================================

    private void ChangeState(SonarChargerState nextState)
    {
        SonarChargerState previousState = state;
        state = nextState;
        stateTimer = 0.0f;

        // Follow に入る際は前フレームのダッシュ入力ベースラインをリセットする
        if (nextState == SonarChargerState.Follow && previousState != SonarChargerState.Follow)
            playerMotionDetector.SyncDashInputBaseline(targetPlayer);
    }

    // =========================================================
    // 接触判定
    // =========================================================

    private void OnTriggerEnter(Collider other)
    {
        TryKillPlayerOnContact(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TryKillPlayerOnContact(other);
    }

    // プレイヤーとの接触でダメージ死を要求する
    private void TryKillPlayerOnContact(Collider other)
    {
        if (!CanKillPlayer() || other == null)
            return;

        PlayerController player = GetPlayerFromCollider(other);
        if (player == null)
            return;

        bool accepted = player.RequestDamageDeath();
        LogDebug($"Contact kill requested. accepted={accepted}");

        // 受理されて disableAfterKill が有効なら敵を無効化する
        if (accepted && Settings.disableAfterKill)
            DisableSelf();
    }

    // =========================================================
    // 初期化ヘルパー
    // =========================================================

    private void InitializeComponents()
    {
        rb = GetComponent<Rigidbody>();
        bodyCollider = GetComponent<Collider>();
        ResolveComponents();
    }

    private void InitializeRigidbody()
    {
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }
    }

    private void InitializeCollider()
    {
        if (bodyCollider != null)
            bodyCollider.isTrigger = true;
    }

    private void ResolveReferences()
    {
        ResolvePlayerIfNeeded();
        ResolveCameraIfNeeded();
    }

    // null のコンポーネント参照を GetComponent で補完する
    private void ResolveComponents()
    {
        movement ??= GetComponent<SonarChargerMovement>();
        sonarDetector ??= GetComponent<SonarChargerSonarDetector>();
        playerMotionDetector ??= GetComponent<SonarChargerPlayerMotionDetector>();
        view ??= GetComponent<SonarChargerView>();
        chargeWarningView ??= GetComponent<SonarChargerChargeWarningView>();
    }

    // targetPlayer が未設定の場合はタグ検索で補完する
    private void ResolvePlayerIfNeeded()
    {
        if (targetPlayer != null)
            return;

        GameObject playerObject = GameObject.FindGameObjectWithTag(Settings.playerTag);
        if (playerObject != null)
            targetPlayer = playerObject.GetComponent<PlayerController>()
                        ?? playerObject.GetComponentInParent<PlayerController>();
    }

    private void ResolveCameraIfNeeded()
    {
        playerCameraController ??= FindFirstObjectByType<PlayerCameraController>();
    }

    // =========================================================
    // リセットヘルパー
    // =========================================================

    // ソナー・モーション検知・視覚オフセットをリセットする
    private void ResetDetectors()
    {
        sonarDetector.ResetDetector(Settings);
        playerMotionDetector.ResetDetector(targetPlayer);
        view.ResetVisualOffset();

        isRecoveringToView = false;
        offscreenTimer = 0.0f;
        lastPlayerTravelDirection = Vector3.zero;

        EnsurePlayerTravelDirection();
    }

    // 全コンポーネントを初期状態に戻す
    private void ResetAllComponents()
    {
        movement.ResetToInitialState();
        sonarDetector.ResetDetector(Settings);
        playerMotionDetector.ResetDetector(targetPlayer);
        view.ResetToInitialState();

        lockedChargeTargetPosition = Vector3.zero;

        isRecoveringToView = false;
        offscreenTimer = 0.0f;
        lastPlayerTravelDirection = Vector3.zero;

        if (chargeWarningView != null)
        {
            chargeWarningView.ResetView();
        }
    }

    // Idle 状態へ遷移し、アニメーションと表示状態をリセットする
    private void ResetToIdleState()
    {
        ChangeState(SonarChargerState.Idle);
        view.PlayIdle();
        ApplyActivationVisualState();
        HideChargeWarning();
    }

    private void StopMovementAndResetView()
    {
        movement.StopImmediate();
        view.ResetVisualOffset();
    }

    // =========================================================
    // ユーティリティ
    // =========================================================

    // プレイヤーへの接触ダメージが有効かを返す
    private bool CanKillPlayer()
    {
        return isActivated &&
               !isDisabled &&
               !isRecoveringToView &&
               Settings.killPlayerOnContact;
    }

    private PlayerController GetPlayerFromCollider(Collider other)
    {
        return other.GetComponent<PlayerController>()
            ?? other.GetComponentInParent<PlayerController>();
    }

    // isActivated に応じて表示・Collider 状態を更新する
    private void ApplyActivationVisualState()
    {
        bool visible = isActivated || !Settings.hideUntilActivated;
        view.SetVisible(visible);

        if (bodyCollider != null)
        {
            bodyCollider.enabled = !isDisabled;
            bodyCollider.isTrigger = true;
        }
    }

    // Transform と Rigidbody を指定位置へ即時移動する
    private void WarpTo(Vector3 position, Quaternion rotation)
    {
        transform.SetPositionAndRotation(position, rotation);

        if (rb != null)
        {
            rb.position = position;
            rb.rotation = rotation;
        }
    }

    // Rigidbody の速度・角速度をゼロにクリアする（Kinematic 状態を保持する）
    private void ClearRigidbodyVelocity()
    {
        if (rb == null)
            return;

        bool wasKinematic = rb.isKinematic;
        if (wasKinematic)
            rb.isKinematic = false;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = wasKinematic;
    }

    // 現在カメラに映っている範囲を取得する
    private bool TryGetCurrentViewBounds(out Bounds viewBounds)
    {
        viewBounds = default;

        if (playerCameraController == null)
        {
            return false;
        }

        viewBounds = playerCameraController.GetCurrentViewBounds();

        // カメラ未取得時はサイズ0のBoundsが返るため無効扱いにする
        return viewBounds.size.x > 0.0001f &&
               viewBounds.size.y > 0.0001f;
    }

    // 現在位置とプレイヤー進行方向から、安全な画面内復帰位置を計算する
    private Vector3 CalculateRecoveryTargetPosition()
    {
        Vector3 currentPosition = GetCurrentWorldPosition();

        if (!TryGetCurrentViewBounds(out Bounds viewBounds))
        {
            Vector3 fallback =
                targetPlayer != null
                    ? targetPlayer.transform.position
                    : transform.position;

            fallback.z = currentPosition.z;
            return fallback;
        }

        float completionPadding =
            Mathf.Max(
                0.0f,
                Settings.recoveryCameraPadding);

        // 復帰完了境界と同じ位置を目標にせず、少し深い位置へ戻す
        float targetPadding =
            completionPadding +
            RecoveryTargetExtraInset;

        float minX =
            viewBounds.min.x +
            targetPadding;

        float maxX =
            viewBounds.max.x -
            targetPadding;

        float minY =
            viewBounds.min.y +
            targetPadding;

        float maxY =
            viewBounds.max.y -
            targetPadding;

        if (minX > maxX)
        {
            minX = viewBounds.center.x;
            maxX = viewBounds.center.x;
        }

        if (minY > maxY)
        {
            minY = viewBounds.center.y;
            maxY = viewBounds.center.y;
        }

        // 現在位置から最も近い画面内位置
        Vector3 nearestEntryPosition = currentPosition;

        nearestEntryPosition.x =
            Mathf.Clamp(
                nearestEntryPosition.x,
                minX,
                maxX);

        nearestEntryPosition.y =
            Mathf.Clamp(
                nearestEntryPosition.y,
                minY,
                maxY);

        nearestEntryPosition.z =
            currentPosition.z;

        if (targetPlayer == null)
        {
            return nearestEntryPosition;
        }

        EnsurePlayerTravelDirection();

        Vector3 travelDirection =
            lastPlayerTravelDirection;

        travelDirection.z = 0.0f;

        if (travelDirection.sqrMagnitude <= 0.0001f)
        {
            return nearestEntryPosition;
        }

        travelDirection.Normalize();

        Vector3 playerPosition =
            targetPlayer.transform.position;

        Vector3 playerToEnemy = currentPosition - playerPosition;

        playerToEnemy.z = 0.0f;

        bool isClearlyBehindPlayer = false;

        if (playerToEnemy.sqrMagnitude > 0.0001f)
        {
            float directionDot =
                Vector3.Dot(
                    playerToEnemy.normalized,
                    travelDirection);

            // プレイヤー進行方向に対して明確に後方にいる場合だけ、
            // プレイヤー後方の位置を復帰目標にする
            isClearlyBehindPlayer =
                directionDot <= -0.25f;
        }

        // 敵が前方や側面にいる場合は、
        // プレイヤーを横切らず最寄りの画面端へ戻す
        if (!isClearlyBehindPlayer)
        {
            return nearestEntryPosition;
        }

        float behindDistance =
            Mathf.Max(
                0.0f,
                Settings.recoveryBehindDistance);

        Vector3 behindTarget =
            playerPosition -
            travelDirection *
            behindDistance;

        behindTarget.z =
            currentPosition.z;

        Vector3 cameraCenter =
            viewBounds.center;

        cameraCenter.z =
            currentPosition.z;

        Vector3 recoveryTarget =
            Vector3.Lerp(
                behindTarget,
                cameraCenter,
                RecoveryCameraCenterBias);

        recoveryTarget.x =
            Mathf.Clamp(
                recoveryTarget.x,
                minX,
                maxX);

        recoveryTarget.y =
            Mathf.Clamp(
                recoveryTarget.y,
                minY,
                maxY);

        recoveryTarget.z =
            currentPosition.z;

        return recoveryTarget;
    }

    // 画面外復帰を開始する
    private void BeginViewRecovery()
    {
        if (isRecoveringToView)
        {
            return;
        }

        isRecoveringToView = true;

        // 復帰中に既存のソナーが残らないよう停止する
        if (sonarDetector != null)
        {
            sonarDetector.CancelPulseForViewRecovery(Settings);
        }

        // 復帰前のダッシュ入力を復帰終了後に拾わないよう同期する
        if (playerMotionDetector != null)
        {
            playerMotionDetector.SyncDashInputBaseline(
                targetPlayer);
        }

        HideChargeWarning();

        LogDebug("View recovery started.");
    }

    // 画面外復帰を終了する
    private void EndViewRecovery()
    {
        if (!isRecoveringToView)
        {
            return;
        }

        isRecoveringToView = false;
        offscreenTimer = 0.0f;

        // BeginViewRecoveryでCancelPulse済み。
        // ここでResetDetectorすると初回遅延へ戻るため、リセットしない。

        if (playerMotionDetector != null)
        {
            playerMotionDetector.SyncDashInputBaseline(targetPlayer);
        }

        LogDebug("View recovery completed.");
    }

    // SonarChargerが現在の画面範囲内にいるかを返す
    private bool IsInsideCurrentView(float padding)
    {
        // カメラ範囲を取得できない場合は、
        // 不要な画面外復帰を発生させないため画面内扱いにする
        if (!TryGetCurrentViewBounds(out Bounds viewBounds))
        {
            return true;
        }

        padding = Mathf.Max(0.0f, padding);

        float minX = viewBounds.min.x + padding;
        float maxX = viewBounds.max.x - padding;
        float minY = viewBounds.min.y + padding;
        float maxY = viewBounds.max.y - padding;

        // 余白が画面サイズの半分を超えた場合は中心へ縮退させる
        if (minX > maxX)
        {
            minX = viewBounds.center.x;
            maxX = viewBounds.center.x;
        }

        if (minY > maxY)
        {
            minY = viewBounds.center.y;
            maxY = viewBounds.center.y;
        }

        Vector3 currentPosition = GetCurrentWorldPosition();

        return currentPosition.x >= minX &&
               currentPosition.x <= maxX &&
               currentPosition.y >= minY &&
               currentPosition.y <= maxY;
    }

    // 画面外からの復帰移動を継続するかを返す
    private bool ShouldRecoverToCurrentView(float distanceToPlayer)
    {
        // 復帰開始後は、指定された余白まで画面内へ入るまで継続する
        if (isRecoveringToView)
        {
            float completionPadding =
                Mathf.Max(
                    0.0f,
                    Settings.recoveryCameraPadding);

            return !IsInsideCurrentView(
                completionPadding);
        }

        // 画面内にいる場合は復帰しない
        if (IsInsideCurrentView(0.0f))
        {
            return false;
        }

        float recoveryDistance =
            Mathf.Max(
                0.0f,
                Settings.recoveryStartDistance);

        // 大きく離れた場合は時間を待たずに復帰する
        if (distanceToPlayer >= recoveryDistance)
        {
            return true;
        }

        float recoveryDelay =
            Mathf.Max(
                0.0f,
                Settings.offscreenRecoveryDelay);

        // 距離が近くても、画面外に留まり続けた場合は復帰する
        return offscreenTimer >= recoveryDelay;
    }

    // プレイヤーの実速度から移動方向を更新する
    private void UpdatePlayerTravelDirection(float deltaTime)
    {
        if (targetPlayer == null)
        {
            return;
        }

        Vector3 playerVelocity =
            targetPlayer.CurrentVelocity;

        playerVelocity.z = 0.0f;

        float minimumSpeed =
            Mathf.Max(
                0.0f,
                MinimumPlayerTravelSpeed);

        // 停止中や極低速時は、直前の有効な方向を維持する
        if (playerVelocity.sqrMagnitude <
            minimumSpeed * minimumSpeed)
        {
            EnsurePlayerTravelDirection();
            return;
        }

        Vector3 currentDirection =
            playerVelocity.normalized;

        // 初回は補間せず、現在の方向をそのまま採用する
        if (lastPlayerTravelDirection.sqrMagnitude <= 0.0001f)
        {
            lastPlayerTravelDirection =
                currentDirection;

            return;
        }

        float safeDeltaTime =
            Mathf.Max(0.0f, deltaTime);

        float blendRate =
            1.0f -
            Mathf.Exp(
                -PlayerTravelDirectionSmoothing *
                safeDeltaTime);

        lastPlayerTravelDirection =
            Vector3.Lerp(
                lastPlayerTravelDirection,
                currentDirection,
                blendRate);

        if (lastPlayerTravelDirection.sqrMagnitude > 0.0001f)
        {
            lastPlayerTravelDirection.Normalize();
        }
    }

    // プレイヤー移動方向が未確定の場合に安全な初期方向を設定する
    private void EnsurePlayerTravelDirection()
    {
        if (lastPlayerTravelDirection.sqrMagnitude > 0.0001f)
        {
            return;
        }

        if (targetPlayer != null)
        {
            Vector3 enemyToPlayer =
                targetPlayer.transform.position -
                transform.position;

            enemyToPlayer.z = 0.0f;

            // 敵からプレイヤーへの方向を暫定的な進行方向として使う
            if (enemyToPlayer.sqrMagnitude > 0.0001f)
            {
                lastPlayerTravelDirection =
                    enemyToPlayer.normalized;

                return;
            }
        }

        // 方向を一切取得できない場合の最終フォールバック
        lastPlayerTravelDirection =
            Vector3.right;
    }

    // 通常追跡中に画面外へ留まっている時間を更新する
    private void UpdateOffscreenTimer(float deltaTime)
    {
        // 復帰中は画面外滞在時間を計測しない
        if (isRecoveringToView)
        {
            return;
        }

        // 画面内へ戻った時点でリセットする
        if (IsInsideCurrentView(0.0f))
        {
            offscreenTimer = 0.0f;
            return;
        }

        offscreenTimer +=
            Mathf.Max(
                0.0f,
                deltaTime);
    }

    // RigidbodyとTransformで位置参照が食い違わないよう、現在位置の取得元を統一する
    private Vector3 GetCurrentWorldPosition()
    {
        return rb != null
            ? rb.position
            : transform.position;
    }

    private void LogDebug(string message)
    {
        if (Settings.enableDebugLog)
            Debug.Log($"[SonarChargerEnemy] {message}", this);
    }
}