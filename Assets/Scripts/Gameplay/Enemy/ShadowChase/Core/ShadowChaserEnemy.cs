using System.Collections;
using UnityEngine;

// PlayerShadowRecorder の履歴を遅延再生してプレイヤーを追う敵。
// 追尾開始前に、トリガーから渡された出現位置へ移動し、待機・出現演出・CatchUp を挟める。
// 通常追尾も平滑化し、LateUpdate で見た目更新してカクつきを減らしている。
// StageResetSystem からは IRespawnResettable 経由で初期状態へ戻される。
[DisallowMultipleComponent]
public sealed class ShadowChaserEnemy : MonoBehaviour, IRespawnResettable
{
    private enum ShadowChaserState
    {
        Idle,
        SpawnDelay,
        Spawning,
        CatchUp,
        Following
    }

    [Header("プレイヤー履歴レコーダー")]
    [Tooltip("追尾元になるプレイヤー履歴です。")]
    [SerializeField] private PlayerShadowRecorder recorder;

    [Header("対象プレイヤー")]
    [Tooltip("接触時に即死を要求する対象プレイヤーです。未設定時は recorder と同じ GameObject から取得を試みます。")]
    [SerializeField] private PlayerController targetPlayer;

    [Header("ビジュアルルート")]
    [Tooltip("左右反転対象の見た目 root です。未設定時は自分自身を使います。")]
    [SerializeField] private Transform visualRoot;

    [Header("制御対象レンダラー")]
    [Tooltip("出現時に表示・非表示を切り替える対象 Renderer 群です。未設定時は子を含めて自動取得を試みます。")]
    [SerializeField] private Renderer[] controlledRenderers;

    [Header("遅延時間")]
    [Tooltip("何秒前のプレイヤーをなぞるかです。")]
    [SerializeField] private float delayTime = 0.4f;

    [Header("補間使用")]
    [Tooltip("履歴の前後 2 点を補間して滑らかにするかです。")]
    [SerializeField] private bool useInterpolation = true;

    [Header("スナップ距離")]
    [Tooltip("目標位置から大きく離れたときに即座に補正する距離です。")]
    [SerializeField] private float snapDistance = 0.3f;

    [Header("滑らか追尾")]
    [Tooltip("通常追尾中も目標位置へ少し滑らかに寄せるかです。")]
    [SerializeField] private bool smoothFollow = true;

    [Header("追尾平滑化強度")]
    [Tooltip("通常追尾中の吸い付き強さです。大きいほど素早く追従します。")]
    [SerializeField] private float followSmoothSharpness = 40.0f;

    [Header("開始時有効化")]
    [Tooltip("開始時に有効にするかです。true ならシーン開始時にスポーンシーケンスへ入ります。")]
    [SerializeField] private bool isActiveOnStart = false;

    [Header("スポーンシーケンス使用")]
    [Tooltip("スポーンシーケンスを使うかです。false なら即座に追尾開始します。")]
    [SerializeField] private bool useSpawnSequence = true;

    [Header("スポーン待機時間")]
    [Tooltip("起動から出現演出開始までの待機時間です。")]
    [SerializeField] private float spawnDelay = 0.2f;

    [Header("スポーン演出時間")]
    [Tooltip("出現演出の長さです。")]
    [SerializeField] private float spawnDuration = 0.35f;

    [Header("スポーン位置オフセット")]
    [Tooltip("出現演出の開始位置オフセットです。要求されたスポーン位置からこの分ずらした位置から現れます。")]
    [SerializeField] private Vector3 spawnOffset = new Vector3(0f, -1f, 0f);

    [Header("スポーン開始スケール")]
    [Tooltip("出現演出開始時のスケールです。")]
    [SerializeField] private Vector3 spawnStartScale = new Vector3(0.6f, 0.6f, 1f);

    [Header("待機中非表示")]
    [Tooltip("待機中は Renderer を非表示にするかです。")]
    [SerializeField] private bool hideDuringSpawnDelay = true;

    [Header("スポーン後追尾開始")]
    [Tooltip("出現演出完了後に追尾を開始するかです。")]
    [SerializeField] private bool startFollowAfterSpawn = true;

    [Header("レール合流使用")]
    [Tooltip("スポーン後、履歴レールへ滑らかに合流する処理を使うかです。")]
    [SerializeField] private bool useCatchUp = true;

    [Header("合流時間")]
    [Tooltip("CatchUp にかける最短時間です。")]
    [SerializeField] private float catchUpDuration = 0.2f;

    [Header("合流追尾強度")]
    [Tooltip("CatchUp 中に現在位置から目標位置へ吸い付く強さです。大きいほど追従が強くなります。")]
    [SerializeField] private float catchUpFollowSharpness = 12.0f;

    [Header("合流完了距離")]
    [Tooltip("CatchUp 完了とみなす位置差です。これ以下まで寄ったら通常追尾へ入ります。")]
    [SerializeField] private float catchUpCompleteDistance = 0.08f;

    [Header("合流カーブ")]
    [Tooltip("CatchUp の進行カーブです。未設定や不正時は線形扱いになります。")]
    [SerializeField] private AnimationCurve catchUpCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("接触判定半径")]
    [Tooltip("この半径内にプレイヤー中心が入ったら即死扱いにします。")]
    [SerializeField] private float contactRadius = 0.4f;

    [Header("向き反転適用")]
    [Tooltip("snapshot の facing に応じて左右反転するかです。")]
    [SerializeField] private bool applyFacingToVisual = true;

    [Header("目標位置表示")]
    [Tooltip("参照中の目標位置を Gizmo で表示します。")]
    [SerializeField] private bool showTargetGizmo = true;

    [Header("目標位置色")]
    [Tooltip("目標位置の Gizmo 色です。")]
    [SerializeField] private Color targetGizmoColor = Color.cyan;

    [Header("スポーン位置表示")]
    [Tooltip("現在の要求スポーン位置を Gizmo で表示します。")]
    [SerializeField] private bool showRequestedSpawnGizmo = true;

    [Header("スポーン位置色")]
    [Tooltip("要求スポーン位置の Gizmo 色です。")]
    [SerializeField] private Color requestedSpawnGizmoColor = Color.yellow;

    [Header("合流目標表示")]
    [Tooltip("CatchUp 中の現在目標位置を Gizmo で表示します。")]
    [SerializeField] private bool showCatchUpTargetGizmo = true;

    [Header("合流目標色")]
    [Tooltip("CatchUp 目標位置の Gizmo 色です。")]
    [SerializeField] private Color catchUpTargetGizmoColor = Color.green;

    private ShadowChaserState state = ShadowChaserState.Idle;
    private Coroutine spawnSequenceCoroutine;

    private PlayerShadowSnapshot lastAppliedSnapshot;
    private bool hasLastAppliedSnapshot;

    private Vector3 defaultPosition;
    private Quaternion defaultRotation;
    private Vector3 defaultVisualScale;
    private bool initializedDefaultVisualScale;

    private float catchUpTimer;
    private Vector3 catchUpStartPosition;
    private Quaternion catchUpStartRotation;
    private bool hasCatchUpTarget;
    private Vector3 catchUpCurrentTargetPosition;
    private Quaternion catchUpCurrentTargetRotation;

    // 現在の起動要求で使うスポーン情報
    private Vector3 currentRequestedSpawnPosition;
    private Quaternion currentRequestedSpawnRotation;
    private bool hasRequestedSpawn;

    // Respawn 用に保存する初期状態
    private bool hasCapturedInitialState;
    private Vector3 initialPosition;
    private Quaternion initialRotation;
    private Vector3 initialVisualScale;
    private bool initialVisibility;
    private bool initialWasActiveOnStart;

    // インスペクター設定の初期値キャッシュ
    private float initialDelayTime;
    private bool initialUseInterpolation;
    private float initialFollowSmoothSharpness;
    private float initialSpawnDelay;
    private float initialSpawnDuration;

    // View 参照口
    public bool HasSnapshot => hasLastAppliedSnapshot;
    public PlayerShadowSnapshot CurrentSnapshot => lastAppliedSnapshot;

    // 初期化処理。
    // 必要な参照を取得し、デフォルトの位置・回転・スケールを保存する。
    private void Awake()
    {
        // Recorder が未設定ならシーンから探す
        if (recorder == null)
        {
            recorder = FindFirstObjectByType<PlayerShadowRecorder>();
        }

        // targetPlayer が未設定なら recorder から取得
        if (targetPlayer == null && recorder != null)
        {
            targetPlayer = recorder.GetComponent<PlayerController>();
        }

        // visualRoot が未設定なら自身を使用
        if (visualRoot == null)
        {
            visualRoot = transform;
        }

        // controlledRenderers が未設定なら子を含めて自動取得
        if (controlledRenderers == null || controlledRenderers.Length == 0)
        {
            controlledRenderers = GetComponentsInChildren<Renderer>(true);
        }

        // デフォルトの位置・回転・スケールを保存
        defaultPosition = transform.position;
        defaultRotation = transform.rotation;

        if (visualRoot != null)
        {
            defaultVisualScale = visualRoot.localScale;
            initializedDefaultVisualScale = true;
        }

        // スポーン要求情報の初期化
        currentRequestedSpawnPosition = defaultPosition;
        currentRequestedSpawnRotation = defaultRotation;
        hasRequestedSpawn = false;

        // 初期状態を Idle に設定
        state = ShadowChaserState.Idle;
        SetVisible(!hideDuringSpawnDelay);

        // 初期状態をキャッシュ
        CaptureInitialState();
    }

    // isActiveOnStart が true なら、シーン開始時に自動的に起動する。
    private void Start()
    {
        if (isActiveOnStart)
        {
            Activate(new ShadowChaserSpawnRequest(defaultPosition, defaultRotation));
        }
        else
        {
            state = ShadowChaserState.Idle;
        }
    }

    // LateUpdate で見た目を更新することで、カクつきを減らす。
    // 状態に応じて CatchUp または Following の更新処理を実行する。
    private void LateUpdate()
    {
        switch (state)
        {
            case ShadowChaserState.CatchUp:
                UpdateCatchUp();
                CheckKillContact();
                break;

            case ShadowChaserState.Following:
                UpdateFollow();
                CheckKillContact();
                break;
        }
    }

    public void CaptureInitialState()
    {
        if (hasCapturedInitialState)
        {
            return;
        }

        initialPosition = transform.position;
        initialRotation = transform.rotation;
        initialVisibility = AreAnyRenderersVisible();
        initialWasActiveOnStart = isActiveOnStart;

        if (visualRoot != null)
        {
            initialVisualScale = visualRoot.localScale;
        }
        else
        {
            initialVisualScale = Vector3.one;
        }

        // インスペクター設定値の初期値をキャッシュ
        initialDelayTime = delayTime;
        initialUseInterpolation = useInterpolation;
        initialFollowSmoothSharpness = followSmoothSharpness;
        initialSpawnDelay = spawnDelay;
        initialSpawnDuration = spawnDuration;

        hasCapturedInitialState = true;
    }

    public void ResetToRespawnState()
    {
        if (spawnSequenceCoroutine != null)
        {
            StopCoroutine(spawnSequenceCoroutine);
            spawnSequenceCoroutine = null;
        }

        state = ShadowChaserState.Idle;
        hasLastAppliedSnapshot = false;
        hasCatchUpTarget = false;
        catchUpTimer = 0f;

        currentRequestedSpawnPosition = defaultPosition;
        currentRequestedSpawnRotation = defaultRotation;
        hasRequestedSpawn = false;

        if (hasCapturedInitialState)
        {
            transform.position = initialPosition;
            transform.rotation = initialRotation;

            if (visualRoot != null)
            {
                visualRoot.localScale = initialVisualScale;
            }

            // パラメータを初期値に復元
            delayTime = initialDelayTime;
            useInterpolation = initialUseInterpolation;
            followSmoothSharpness = initialFollowSmoothSharpness;
            spawnDelay = initialSpawnDelay;
            spawnDuration = initialSpawnDuration;

            SetVisible(initialVisibility);

            if (initialWasActiveOnStart)
            {
                Activate(new ShadowChaserSpawnRequest(initialPosition, initialRotation));
            }
        }
        else
        {
            transform.position = defaultPosition;
            transform.rotation = defaultRotation;

            if (visualRoot != null && initializedDefaultVisualScale)
            {
                visualRoot.localScale = defaultVisualScale;
            }

            if (hideDuringSpawnDelay)
            {
                SetVisible(false);
            }
            else
            {
                SetVisible(true);
            }
        }
    }

    // 外部から呼ばれて敵を起動する。
    // トリガーから渡された出現位置へ移動し、スポーンシーケンスを開始する。
    public void Activate(ShadowChaserSpawnRequest request)
    {
        // 既に Idle 以外の状態なら何もしない
        if (state != ShadowChaserState.Idle)
        {
            return;
        }

        // スポーン要求情報を保存
        currentRequestedSpawnPosition = request.position;
        currentRequestedSpawnRotation = request.rotation;
        hasRequestedSpawn = true;

        // 要求されたスポーン位置へ即座に移動
        MoveToRequestedSpawnImmediate();

        // スポーンシーケンスを使わない場合は即座に追尾開始
        if (!useSpawnSequence)
        {
            SetVisible(true);

            if (visualRoot != null && initializedDefaultVisualScale)
            {
                visualRoot.localScale = defaultVisualScale;
            }

            if (startFollowAfterSpawn)
            {
                BeginCatchUpOrFollow();
            }

            return;
        }

        // スポーンシーケンスを開始
        if (spawnSequenceCoroutine != null)
        {
            StopCoroutine(spawnSequenceCoroutine);
        }

        spawnSequenceCoroutine = StartCoroutine(CoSpawnSequence());
    }

    public void Deactivate()
    {
        if (spawnSequenceCoroutine != null)
        {
            StopCoroutine(spawnSequenceCoroutine);
            spawnSequenceCoroutine = null;
        }

        state = ShadowChaserState.Idle;
        hasLastAppliedSnapshot = false;
        hasCatchUpTarget = false;
        catchUpTimer = 0f;

        if (hideDuringSpawnDelay)
        {
            SetVisible(false);
        }
    }

    public bool IsActive()
    {
        return state != ShadowChaserState.Idle;
    }

    // スポーンシーケンスのコルーチン。
    // 待機時間を置き、出現演出（オフセット位置から目的地への移動 + スケール変化）を行う。
    private IEnumerator CoSpawnSequence()
    {
        state = ShadowChaserState.SpawnDelay;

        Vector3 targetPosition = GetRequestedSpawnPosition();
        Quaternion targetRotation = GetRequestedSpawnRotation();

        // 待機中は非表示にするかどうか
        if (hideDuringSpawnDelay)
        {
            SetVisible(false);
        }
        else
        {
            SetVisible(true);
        }

        // スポーン位置へ移動
        transform.position = targetPosition;
        transform.rotation = targetRotation;

        // 待機時間
        if (spawnDelay > 0f)
        {
            yield return new WaitForSeconds(spawnDelay);
        }

        // 出現演出開始
        state = ShadowChaserState.Spawning;

        SetVisible(true);

        // 出現演出の開始位置と終了位置
        Vector3 startPosition = targetPosition + spawnOffset;
        Vector3 endPosition = targetPosition;

        Vector3 startScale = spawnStartScale;
        Vector3 endScale = initializedDefaultVisualScale ? defaultVisualScale : Vector3.one;

        transform.position = startPosition;
        transform.rotation = targetRotation;

        if (visualRoot != null)
        {
            visualRoot.localScale = startScale;
        }

        // 出現演出：位置とスケールを補間
        float duration = Mathf.Max(0.0001f, spawnDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            transform.position = Vector3.Lerp(startPosition, endPosition, t);

            if (visualRoot != null)
            {
                visualRoot.localScale = Vector3.Lerp(startScale, endScale, t);
            }

            yield return null;
        }

        // 最終位置とスケールを確定
        transform.position = endPosition;

        if (visualRoot != null)
        {
            visualRoot.localScale = endScale;
        }

        // 出現後に追尾を開始するかどうか
        if (startFollowAfterSpawn)
        {
            BeginCatchUpOrFollow();
        }
        else
        {
            state = ShadowChaserState.Idle;
        }

        spawnSequenceCoroutine = null;
    }

    // CatchUp または通常追尾を開始する。
    // CatchUp が有効なら、スポーン位置から履歴レールへの滑らかな合流を行う。
    private void BeginCatchUpOrFollow()
    {
        // CatchUp を使わない場合は即座に通常追尾へ
        if (!useCatchUp)
        {
            BeginFollow();
            return;
        }

        // Recorder がない場合は通常追尾へ
        if (recorder == null)
        {
            BeginFollow();
            return;
        }

        // 履歴から snapshot を取得できない場合は通常追尾へ
        if (!recorder.TryGetSnapshotAtDelay(delayTime, useInterpolation, out PlayerShadowSnapshot snapshot))
        {
            BeginFollow();
            return;
        }

        // CatchUp 開始：現在位置から目標位置へ滑らかに移行
        catchUpTimer = 0f;
        catchUpStartPosition = transform.position;
        catchUpStartRotation = transform.rotation;

        catchUpCurrentTargetPosition = snapshot.position;
        catchUpCurrentTargetRotation = snapshot.rotation;
        hasCatchUpTarget = true;

        lastAppliedSnapshot = snapshot;
        hasLastAppliedSnapshot = true;

        state = ShadowChaserState.CatchUp;
    }

    private void BeginFollow()
    {
        state = ShadowChaserState.Following;
    }

    // CatchUp 中の更新処理。
    // スポーン位置から履歴レールへ滑らかに合流する。
    // 目標位置に十分近づいたら通常追尾へ移行する。
    private void UpdateCatchUp()
    {
        if (recorder == null)
        {
            BeginFollow();
            return;
        }

        // 最新の snapshot を取得
        if (!recorder.TryGetSnapshotAtDelay(delayTime, useInterpolation, out PlayerShadowSnapshot snapshot))
        {
            return;
        }

        // 目標位置を更新
        catchUpCurrentTargetPosition = snapshot.position;
        catchUpCurrentTargetRotation = snapshot.rotation;
        hasCatchUpTarget = true;

        lastAppliedSnapshot = snapshot;
        hasLastAppliedSnapshot = true;

        float duration = Mathf.Max(0.0001f, catchUpDuration);
        catchUpTimer += Time.deltaTime;

        // 正規化された時間 (0.0 ～ 1.0)
        float normalizedTime = Mathf.Clamp01(catchUpTimer / duration);

        // カーブを適用
        float curveT = normalizedTime;
        if (catchUpCurve != null && catchUpCurve.length > 0)
        {
            curveT = catchUpCurve.Evaluate(normalizedTime);
        }

        // 目標位置への吸い付き計算
        float followLerpFactor = 1f - Mathf.Exp(-Mathf.Max(0.01f, catchUpFollowSharpness) * Time.deltaTime);

        // 現在位置から目標位置への移動目標を計算
        Vector3 movingTargetPosition = Vector3.Lerp(
            transform.position,
            catchUpCurrentTargetPosition,
            followLerpFactor);

        Quaternion movingTargetRotation = Quaternion.Slerp(
            transform.rotation,
            catchUpCurrentTargetRotation,
            followLerpFactor);

        // 開始位置から移動目標へ、カーブに応じて補間
        transform.position = Vector3.Lerp(
            catchUpStartPosition,
            movingTargetPosition,
            curveT);

        transform.rotation = Quaternion.Slerp(
            catchUpStartRotation,
            movingTargetRotation,
            curveT);

        ApplyFacingVisual(snapshot.facing);

        // 目標位置との距離をチェック（パフォーマンス最適化: sqrMagnitude を使用）
        float sqrRemainDistance = (transform.position - catchUpCurrentTargetPosition).sqrMagnitude;
        float sqrCompleteDistance = catchUpCompleteDistance * catchUpCompleteDistance;

        // 時間が経過し、十分近づいたら通常追尾へ移行
        if (normalizedTime >= 1f && sqrRemainDistance <= Mathf.Max(0.000001f, sqrCompleteDistance))
        {
            BeginFollow();
        }
    }

    // 通常追尾中の更新処理。
    // delayTime 秒前のプレイヤー履歴を取得し、その位置をなぞる。
    private void UpdateFollow()
    {
        if (recorder == null)
        {
            return;
        }

        // delayTime 秒前の snapshot を取得
        if (!recorder.TryGetSnapshotAtDelay(delayTime, useInterpolation, out PlayerShadowSnapshot snapshot))
        {
            return;
        }

        // snapshot を適用して位置を更新
        ApplySnapshot(snapshot);

        lastAppliedSnapshot = snapshot;
        hasLastAppliedSnapshot = true;
    }

    // snapshot を適用して自身の位置・回転・向きを更新する。
    // snapDistance 以上離れている場合は即座に移動、そうでなければ滑らかに追尾する。
    private void ApplySnapshot(PlayerShadowSnapshot snapshot)
    {
        // パフォーマンス最適化: 平方根計算を避けるため sqrMagnitude を使用
        float sqrDistance = (transform.position - snapshot.position).sqrMagnitude;
        float sqrSnapDistance = snapDistance * snapDistance;

        // snapDistance 以上離れている場合は即座に移動
        if (sqrDistance >= sqrSnapDistance)
        {
            transform.position = snapshot.position;
        }
        else if (smoothFollow)
        {
            // 滑らかに追尾
            float lerpFactor = 1f - Mathf.Exp(-Mathf.Max(0.01f, followSmoothSharpness) * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, snapshot.position, lerpFactor);
        }
        else
        {
            // 滑らかな追尾をしない場合は即座に移動
            transform.position = snapshot.position;
        }

        transform.rotation = snapshot.rotation;
        ApplyFacingVisual(snapshot.facing);
    }

    private void ApplyFacingVisual(int facing)
    {
        if (!applyFacingToVisual || visualRoot == null || !initializedDefaultVisualScale)
        {
            return;
        }

        Vector3 scale = defaultVisualScale;
        scale.x = Mathf.Abs(defaultVisualScale.x) * (facing < 0 ? -1f : 1f);
        visualRoot.localScale = scale;
    }

    // プレイヤーとの接触判定。
    // contactRadius 内にプレイヤーがいたら即死を要求する。
    private void CheckKillContact()
    {
        if (targetPlayer == null)
        {
            return;
        }

        // プレイヤーとの距離を計算（パフォーマンス最適化: sqrMagnitude を使用）
        Vector3 playerPosition = targetPlayer.transform.position;
        float sqrDistance = (transform.position - playerPosition).sqrMagnitude;
        float sqrContactRadius = contactRadius * contactRadius;

        // contactRadius 内にいれば即死を要求
        if (sqrDistance > sqrContactRadius)
        {
            return;
        }

        targetPlayer.RequestHazardDeath();
    }

    private void MoveToRequestedSpawnImmediate()
    {
        transform.position = GetRequestedSpawnPosition();
        transform.rotation = GetRequestedSpawnRotation();

        if (visualRoot != null && initializedDefaultVisualScale)
        {
            visualRoot.localScale = defaultVisualScale;
        }
    }

    private Vector3 GetRequestedSpawnPosition()
    {
        if (hasRequestedSpawn)
        {
            return currentRequestedSpawnPosition;
        }

        return defaultPosition;
    }

    private Quaternion GetRequestedSpawnRotation()
    {
        if (hasRequestedSpawn)
        {
            return currentRequestedSpawnRotation;
        }

        return defaultRotation;
    }

    private void SetVisible(bool visible)
    {
        if (controlledRenderers == null)
        {
            return;
        }

        int count = controlledRenderers.Length;
        for (int i = 0; i < count; ++i)
        {
            Renderer current = controlledRenderers[i];
            if (current == null)
            {
                continue;
            }

            current.enabled = visible;
        }
    }

    private bool AreAnyRenderersVisible()
    {
        if (controlledRenderers == null || controlledRenderers.Length == 0)
        {
            return true;
        }

        for (int i = 0; i < controlledRenderers.Length; ++i)
        {
            Renderer current = controlledRenderers[i];
            if (current != null && current.enabled)
            {
                return true;
            }
        }

        return false;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, contactRadius);

        if (showTargetGizmo && hasLastAppliedSnapshot)
        {
            Gizmos.color = targetGizmoColor;
            Gizmos.DrawSphere(lastAppliedSnapshot.position, 0.08f);
        }

        if (showRequestedSpawnGizmo)
        {
            Vector3 spawnPosition = hasRequestedSpawn ? currentRequestedSpawnPosition : transform.position;
            Gizmos.color = requestedSpawnGizmoColor;
            Gizmos.DrawWireSphere(spawnPosition, 0.15f);
            Gizmos.DrawLine(spawnPosition, spawnPosition + spawnOffset);
        }

        if (showCatchUpTargetGizmo && state == ShadowChaserState.CatchUp && hasCatchUpTarget)
        {
            Gizmos.color = catchUpTargetGizmoColor;
            Gizmos.DrawSphere(catchUpCurrentTargetPosition, 0.1f);
            Gizmos.DrawLine(transform.position, catchUpCurrentTargetPosition);
        }
    }
}