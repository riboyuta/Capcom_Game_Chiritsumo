using UnityEngine;

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
    // 敵の状態定義
    private enum SonarChargerState
    {
        Idle,        // 非アクティブ状態
        Follow,      // プレイヤーを追跡しながらソナーで探知
        Alert,       // プレイヤー発見後の溜め状態
        LockConfirm, // 突進方向確定後の短い硬直
        Charge,      // 直線突進
        Rebound,     // カメラ境界に当たった後の跳ね返り
        Stun         // 跳ね返り後の硬直
    }

    [Header("設定")]
    [Tooltip("SonarChargerEnemy の調整値です。")]
    [SerializeField] private SonarChargerSettings settings = new SonarChargerSettings();

    [Header("対象プレイヤー")]
    [Tooltip("追跡・検知・接触死の対象プレイヤーです。未設定時は Player タグから探索します。")]
    [SerializeField] private PlayerController targetPlayer;

    [Header("プレイヤーカメラ")]
    [Tooltip("突進停止に使う playerCamera です。未設定時はシーンから探索します。")]
    [SerializeField] private PlayerCameraController playerCameraController;

    // 構成コンポーネント
    [Header("構成コンポーネント")]
    [SerializeField] private SonarChargerMovement movement;
    [SerializeField] private SonarChargerSonarDetector sonarDetector;
    [SerializeField] private SonarChargerPlayerMotionDetector playerMotionDetector;
    [SerializeField] private SonarChargerView view;
    [SerializeField] private SonarChargerChargeWarningView chargeWarningView;

    // キャッシュ済み Unity コンポーネント
    private Rigidbody rb;
    private Collider bodyCollider;

    // 現在の状態
    private SonarChargerState state = SonarChargerState.Idle;
    private float stateTimer;
    private Vector3 alertTargetPosition;
    private Vector3 lockedChargeTargetPosition;

    private Vector3 chargeWarningEndPosition;
    private bool hasChargeWarningEndPosition;

    // アクティベーション状態
    private bool isActivated;
    private bool isDisabled;

    // 初期状態保持（リスポーン用）
    private bool hasCapturedInitialState;
    private Vector3 initialPosition;
    private Quaternion initialRotation;

    public bool IsActivated => isActivated;
    public bool IsCharging => state == SonarChargerState.Charge;
    public Vector3 CurrentChargeDirection => movement != null ? movement.ChargeDirection : Vector3.zero;

    private SonarChargerSettings Settings => settings ?? (settings = new SonarChargerSettings());

    // Unityライフサイクル: 初期化
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
        if (Settings.startActive)
        {
            BeginChase();
        }
    }

    // Unityライフサイクル: 更新
    private void Update()
    {
        // 無効化または非アクティブの場合は何もしない
        if (isDisabled || !isActivated)
        {
            return;
        }

        // プレイヤーとカメラの参照を解決（未設定の場合はシーンから検索）
        ResolvePlayerIfNeeded();
        ResolveCameraIfNeeded();

        // プレイヤーが見つからない場合は何もしない
        if (targetPlayer == null)
        {
            return;
        }

        float deltaTime = Time.deltaTime;

        // プレイヤーの移動状態を毎フレーム更新
        playerMotionDetector.Tick(targetPlayer, Settings);

        // 現在の状態に応じた更新処理を実行
        switch (state)
        {
            case SonarChargerState.Follow:
                TickFollow(deltaTime);
                break;

            case SonarChargerState.Alert:
                TickAlert(deltaTime);
                break;

            case SonarChargerState.LockConfirm:
                TickLockConfirm(deltaTime);
                break;

            case SonarChargerState.Charge:
                TickCharge(deltaTime);
                break;

            case SonarChargerState.Rebound:
                TickRebound(deltaTime);
                break;

            case SonarChargerState.Stun:
                TickStun(deltaTime);
                break;
        }
    }

    public void ApplySettings(SonarChargerSettings nextSettings)
    {
        if (nextSettings == null)
        {
            return;
        }

        // RoomEnemySystem 側の Settings インスタンスを直接共有せず、
        // この敵が持つ Settings に値だけコピーする。
        Settings.CopyFrom(nextSettings);

        ResolveComponents();
        ResolveReferences();

        // Idle 中なら検知器も新しい設定で初期化する。
        // Follow / Alert / Charge 中にリセットすると挙動が飛ぶので、状態が Idle の時だけ行う。
        if (Application.isPlaying && state == SonarChargerState.Idle)
        {
            ResetDetectors();
            ApplyActivationVisualState();
            HideChargeWarning();

            if (Settings.startActive && !isActivated && !isDisabled)
            {
                BeginChase();
            }
        }
    }

    // 敵を起動し、Follow状態でプレイヤーの追跡を開始する
    public void BeginChase()
    {
        // 既に起動済みの場合は何もしない
        if (isActivated)
        {
            LogDebug("Already activated.");
            return;
        }

        HideChargeWarning();

        // 状態をアクティブに設定
        isDisabled = false;
        isActivated = true;

        // Rigidbodyの速度をゼロクリア
        ClearRigidbodyVelocity();
        // 各検知器をリセット
        ResetDetectors();

        // 表示状態を適用し、Follow状態へ遷移
        ApplyActivationVisualState();
        HideChargeWarning();
        ChangeState(SonarChargerState.Follow);
        view.PlayFollow();

        LogDebug("BeginChase.");
    }

    public void BeginChase(Vector3 spawnPosition, Quaternion spawnRotation)
    {
        WarpTo(spawnPosition, spawnRotation);
        BeginChase();
    }

    public void DisableSelf()
    {
        HideChargeWarning();

        isDisabled = true;
        isActivated = false;
        ClearRigidbodyVelocity();
        ResetToIdleState();
    }

    public void CaptureInitialState()
    {
        if (hasCapturedInitialState)
        {
            return;
        }

        initialPosition = transform.position;
        initialRotation = transform.rotation;

        movement.CaptureInitialState();
        sonarDetector.ResetDetector(Settings);
        playerMotionDetector.ResetDetector(targetPlayer);
        view.CaptureInitialState();

        hasCapturedInitialState = true;
    }

    public void ResetToRespawnState()
    {
        ResetEncounterForRespawn();
    }

    // リスポーン時に敵を初期状態にリセットする
    public void ResetEncounterForRespawn()
    {
        HideChargeWarning();

        // 初期位置・回転にワープ
        WarpTo(initialPosition, initialRotation);
        // Rigidbodyの速度をクリア
        ClearRigidbodyVelocity();

        // 状態を非アクティブにリセット
        isDisabled = false;
        isActivated = false;

        // 全コンポーネントを初期状態に戻す
        ResetAllComponents();
        // Idle状態に戻す
        ResetToIdleState();

        // startActiveが有効なら、リスポーン後に再起動
        if (Settings.startActive)
        {
            BeginChase();
        }
    }

    // 状態: Follow - プレイヤーを追跡しながらソナーで探知
    private void TickFollow(float deltaTime)
    {
        // 視覚的なオフセットをリセット（Alertの揺れエフェクトを解除）
        view.ResetVisualOffset();

        // プレイヤーの位置へ向かって移動
        Vector3 followDirection = targetPlayer.transform.position - transform.position;
        followDirection.z = 0.0f;

        movement.TickFollow(
            targetPlayer.transform.position,
            Settings,
            deltaTime);

        view.ApplyDirection(followDirection);

        // ダッシュ入力による即座Alert起動をチェック
        if (TryStartAlertByDashInput())
        {
            return;
        }

        // ソナーでプレイヤーを検知したらAlertへ遷移
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

    // 状態遷移: Follow → Alert
    private void StartAlert(Vector3 detectedPosition)
    {
        alertTargetPosition = detectedPosition;
        sonarDetector.CancelPulse();
        StopMovementAndResetView();

        HideChargeWarning();

        ChangeState(SonarChargerState.Alert);
        view.PlayAlert();
        LogDebug("Player detected. Alert started.");
    }

    // 状態: Alert - 突進前の溜め状態
    private void TickAlert(float deltaTime)
    {
        stateTimer += deltaTime;
        // 移動を停止（溜め中は移動しない）
        movement.StopImmediate();

        // プレイヤーが存在するなら、目標位置を毎フレーム更新（プレイヤーを追尾）
        if (targetPlayer != null)
        {
            alertTargetPosition = targetPlayer.transform.position;
        }

        Vector3 alertDirection = alertTargetPosition - transform.position;
        alertDirection.z = 0.0f;
        view.ApplyDirection(alertDirection);

        // Alert状態の視覚的な揺れエフェクトを更新
        view.TickAlert(stateTimer, Settings);

        UpdateChargeWarning();

        // 溜め時間が経過したらLockConfirmへ遷移
        if (stateTimer >= Settings.alertTime)
        {
            StartLockConfirm();
        }
    }

    // ダッシュ入力による即座Alert起動判定
    private bool TryStartAlertByDashInput()
    {
        // プレイヤーまたはモーション検知器がない場合はfalse
        if (targetPlayer == null || playerMotionDetector == null)
        {
            return false;
        }

        // 機能が無効化されている場合、ベースラインだけ同期してfalse
        if (!Settings.enableDashInputAlertTrigger)
        {
            playerMotionDetector.SyncDashInputBaseline(targetPlayer);
            return false;
        }

        // 新しいダッシュ入力がなければfalse
        if (!playerMotionDetector.ConsumeDashInputTrigger(targetPlayer))
        {
            return false;
        }

        // ダッシュ入力検知成功、即座にAlertへ遷移
        StartAlert(targetPlayer.transform.position);
        return true;
    }

    // 状態遷移: Alert → LockConfirm
    private void StartLockConfirm()
    {
        // この瞬間の狙い位置で突進方向を確定する。
        lockedChargeTargetPosition = alertTargetPosition;

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

    // 状態: LockConfirm - 突進方向確定後の短い硬直
    private void TickLockConfirm(float deltaTime)
    {
        stateTimer += deltaTime;

        movement.StopImmediate();

        Vector3 lockedDirection = lockedChargeTargetPosition - transform.position;
        lockedDirection.z = 0.0f;
        view.ApplyDirection(lockedDirection);

        // 方向確定後はプレイヤーを追い直さず、固定された方向の予測表示を維持する。
        UpdateChargeWarningForTarget(lockedChargeTargetPosition, 1.0f);

        if (stateTimer >= Settings.lockConfirmTime)
        {
            StartCharge();
        }
    }

    // 状態遷移: LockConfirm → Charge
    private void StartCharge()
    {
        view.ResetVisualOffset();

        movement.StartCharge(
            lockedChargeTargetPosition,
            playerCameraController,
            Settings);

        view.ApplyDirection(movement.ChargeDirection);

        // Charge中に表示し続ける帯の終点をここで固定する。
        PrepareChargeWarningForCharge();

        ChangeState(SonarChargerState.Charge);
        view.PlayCharge();
        LogDebug($"Charge started. dir={movement.ChargeDirection}");
    }

    // 状態: Charge - 直線突進
    private void TickCharge(float deltaTime)
    {
        stateTimer += deltaTime;

        // 突進移動を更新、カメラ境界に到達したかをチェック
        bool reachedCameraBoundary = movement.TickCharge(Settings, deltaTime);

        // 境界に到達したらReboundへ遷移
        if (reachedCameraBoundary)
        {
            StartRebound();
            return;
        }

        UpdateChargeWarningDuringCharge();
    }

    private void UpdateChargeWarning()
    {
        UpdateChargeWarningForTarget(alertTargetPosition, GetAlertProgress01());
    }

    private void UpdateChargeWarningForTarget(Vector3 targetPosition, float progress)
    {
        if (chargeWarningView == null)
        {
            return;
        }

        if (!Settings.showAlertPredictionLine)
        {
            chargeWarningView.Hide();
            return;
        }

        Vector3 start = transform.position;

        Vector3 direction = movement.BuildChargeDirectionTo(
            targetPosition,
            Settings);

        Vector3 end = movement.BuildPredictionEndPosition(
            start,
            direction,
            playerCameraController,
            Settings);

        chargeWarningView.UpdateWarning(
            start,
            end,
            Mathf.Clamp01(progress),
            stateTimer,
            Settings);
    }

    private float GetAlertProgress01()
    {
        if (Settings.alertTime <= 0.0f)
        {
            return 1.0f;
        }

        return Mathf.Clamp01(stateTimer / Settings.alertTime);
    }

    private void PrepareChargeWarningForCharge()
    {
        hasChargeWarningEndPosition = false;

        if (chargeWarningView == null)
        {
            return;
        }

        if (!Settings.showAlertPredictionLine)
        {
            chargeWarningView.Hide();
            return;
        }

        Vector3 start = transform.position;

        Vector3 direction = movement.ChargeDirection;
        direction.z = 0.0f;

        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = movement.BuildChargeDirectionTo(
                lockedChargeTargetPosition,
                Settings);
        }

        chargeWarningEndPosition = movement.BuildPredictionEndPosition(
            start,
            direction,
            playerCameraController,
            Settings);

        hasChargeWarningEndPosition = true;

        chargeWarningView.UpdateWarning(
            start,
            chargeWarningEndPosition,
            1.0f,
            0.0f,
            Settings);
    }

    private void UpdateChargeWarningDuringCharge()
    {
        if (chargeWarningView == null)
        {
            return;
        }

        if (!Settings.showAlertPredictionLine)
        {
            chargeWarningView.Hide();
            return;
        }

        if (!hasChargeWarningEndPosition)
        {
            chargeWarningView.Hide();
            return;
        }

        Vector3 start = transform.position;
        Vector3 end = chargeWarningEndPosition;

        Vector3 remaining = end - start;
        remaining.z = 0.0f;

        // 終点付近まで来たらラインを消す。
        if (remaining.sqrMagnitude <= 0.05f * 0.05f)
        {
            chargeWarningView.Hide();
            return;
        }

        chargeWarningView.UpdateWarning(
            start,
            end,
            1.0f,
            stateTimer,
            Settings);
    }

    private void HideChargeWarning()
    {
        hasChargeWarningEndPosition = false;

        if (chargeWarningView != null)
        {
            chargeWarningView.Hide();
        }
    }

    // 状態遷移: Charge → Rebound
    private void StartRebound()
    {
        HideChargeWarning();
        movement.StartRebound(Settings);
        ChangeState(SonarChargerState.Rebound);
        view.PlayRebound();
        LogDebug("Rebound started.");
    }

    // 状態: Rebound - 跳ね返り
    private void TickRebound(float deltaTime)
    {
        // 跳ね返りアニメーションを更新、完了したかをチェック
        if (movement.TickRebound(Settings, deltaTime))
        {
            // 跳ね返り完了、移動停止してStunへ遷移
            movement.StopImmediate();
            ChangeState(SonarChargerState.Stun);
            view.PlayStun();
        }
    }

    // 状態: Stun - 跳ね返り後の硬直
    private void TickStun(float deltaTime)
    {
        stateTimer += deltaTime;
        // 硬直中は移動しない
        StopMovementAndResetView();

        // 硬直時間が経過したらFollowへ戻る
        if (stateTimer >= Settings.stunTime)
        {
            // ソナーをリセットして再探知を開始
            sonarDetector.ResetDetector(Settings);
            ChangeState(SonarChargerState.Follow);
            view.PlayFollow();

        }
    }

    // 状態遷移処理
    private void ChangeState(SonarChargerState nextState)
    {
        SonarChargerState previousState = state;
        state = nextState;
        stateTimer = 0.0f; // 状態タイマーをリセット

        // Follow状態に入った時、それ以前のダッシュ入力を無視する
        if (nextState == SonarChargerState.Follow && previousState != SonarChargerState.Follow)
        {
            playerMotionDetector.SyncDashInputBaseline(targetPlayer);
        }
    }

    // Unityライフサイクル: 当たり判定
    private void OnTriggerEnter(Collider other)
    {
        TryKillPlayerOnContact(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TryKillPlayerOnContact(other);
    }

    // プレイヤーとの接触死判定
    private void TryKillPlayerOnContact(Collider other)
    {
        // 条件チェック：アクティブ、無効化されていない、接触死有効
        if (!CanKillPlayer() || other == null)
        {
            return;
        }

        // ColliderからPlayerControllerを取得
        PlayerController player = GetPlayerFromCollider(other);
        if (player == null)
        {
            return;
        }

        // プレイヤーにダメージ死をリクエスト
        bool accepted = player.RequestDamageDeath();
        LogDebug($"Contact kill requested. accepted={accepted}");

        // キル成功かつdisableAfterKillが有効なら敵を無効化
        if (accepted && Settings.disableAfterKill)
        {
            DisableSelf();
        }
    }

    // 初期化ヘルパー
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
        {
            bodyCollider.isTrigger = true;
        }
    }

    private void ResolveReferences()
    {
        ResolvePlayerIfNeeded();
        ResolveCameraIfNeeded();
    }

    private void ResolveComponents()
    {
        movement ??= GetComponent<SonarChargerMovement>();
        sonarDetector ??= GetComponent<SonarChargerSonarDetector>();
        playerMotionDetector ??= GetComponent<SonarChargerPlayerMotionDetector>();
        view ??= GetComponent<SonarChargerView>();
        chargeWarningView ??= GetComponent<SonarChargerChargeWarningView>();
    }

    private void ResolvePlayerIfNeeded()
    {
        if (targetPlayer != null)
        {
            return;
        }

        GameObject playerObject = GameObject.FindGameObjectWithTag(Settings.playerTag);
        if (playerObject != null)
        {
            targetPlayer = playerObject.GetComponent<PlayerController>() ?? playerObject.GetComponentInParent<PlayerController>();
        }
    }

    private void ResolveCameraIfNeeded()
    {
        playerCameraController ??= FindFirstObjectByType<PlayerCameraController>();
    }

    // リセット処理ヘルパー
    private void ResetDetectors()
    {
        sonarDetector.ResetDetector(Settings);
        playerMotionDetector.ResetDetector(targetPlayer);
        view.ResetVisualOffset();
    }

    private void ResetAllComponents()
    {
        movement.ResetToInitialState();
        sonarDetector.ResetDetector(Settings);
        playerMotionDetector.ResetDetector(targetPlayer);
        view.ResetToInitialState();

        lockedChargeTargetPosition = Vector3.zero;

        if (chargeWarningView != null)
        {
            chargeWarningView.ResetView();
        }
    }

    // 状態遷移ヘルパー
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

    // 判定ヘルパー
    private bool CanKillPlayer()
    {
        return isActivated && !isDisabled && Settings.killPlayerOnContact;
    }

    private PlayerController GetPlayerFromCollider(Collider other)
    {
        return other.GetComponent<PlayerController>() ?? other.GetComponentInParent<PlayerController>();
    }

    // ユーティリティヘルパー
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

    private void WarpTo(Vector3 position, Quaternion rotation)
    {
        transform.SetPositionAndRotation(position, rotation);

        if (rb != null)
        {
            rb.position = position;
            rb.rotation = rotation;
        }
    }

    private void ClearRigidbodyVelocity()
    {
        if (rb == null)
        {
            return;
        }

        bool wasKinematic = rb.isKinematic;
        if (wasKinematic)
        {
            rb.isKinematic = false;
        }

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = wasKinematic;
    }

    private void LogDebug(string message)
    {
        if (Settings.enableDebugLog)
        {
            Debug.Log($"[SonarChargerEnemy] {message}", this);
        }
    }
}