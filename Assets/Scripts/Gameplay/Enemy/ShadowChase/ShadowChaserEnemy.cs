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

    [Header("参照")]
    [Tooltip("追尾元になるプレイヤー履歴です。")]
    [SerializeField] private PlayerShadowRecorder recorder;

    [Tooltip("接触時に即死を要求する対象プレイヤーです。未設定時は recorder と同じ GameObject から取得を試みます。")]
    [SerializeField] private PlayerController targetPlayer;

    [Tooltip("左右反転対象の見た目 root です。未設定時は自分自身を使います。")]
    [SerializeField] private Transform visualRoot;

    [Tooltip("出現時に表示・非表示を切り替える対象 Renderer 群です。未設定時は子を含めて自動取得を試みます。")]
    [SerializeField] private Renderer[] controlledRenderers;

    [Header("追尾設定")]
    [Tooltip("何秒前のプレイヤーをなぞるかです。")]
    [SerializeField] private float delayTime = 0.4f;

    [Tooltip("履歴の前後 2 点を補間して滑らかにするかです。")]
    [SerializeField] private bool useInterpolation = true;

    [Tooltip("目標位置から大きく離れたときに即座に補正する距離です。")]
    [SerializeField] private float snapDistance = 1.5f;

    [Tooltip("通常追尾中も目標位置へ少し滑らかに寄せるかです。")]
    [SerializeField] private bool smoothFollow = true;

    [Tooltip("通常追尾中の吸い付き強さです。大きいほど素早く追従します。")]
    [SerializeField] private float followSmoothSharpness = 20.0f;

    [Tooltip("開始時に有効にするかです。true ならシーン開始時にスポーンシーケンスへ入ります。")]
    [SerializeField] private bool isActiveOnStart = false;

    [Header("スポーン設定")]
    [Tooltip("スポーンシーケンスを使うかです。false なら即座に追尾開始します。")]
    [SerializeField] private bool useSpawnSequence = true;

    [Tooltip("起動から出現演出開始までの待機時間です。")]
    [SerializeField] private float spawnDelay = 0.2f;

    [Tooltip("出現演出の長さです。")]
    [SerializeField] private float spawnDuration = 0.35f;

    [Tooltip("出現演出の開始位置オフセットです。要求されたスポーン位置からこの分ずらした位置から現れます。")]
    [SerializeField] private Vector3 spawnOffset = new Vector3(0f, -1f, 0f);

    [Tooltip("出現演出開始時のスケールです。")]
    [SerializeField] private Vector3 spawnStartScale = new Vector3(0.6f, 0.6f, 1f);

    [Tooltip("待機中は Renderer を非表示にするかです。")]
    [SerializeField] private bool hideDuringSpawnDelay = true;

    [Tooltip("出現演出完了後に追尾を開始するかです。")]
    [SerializeField] private bool startFollowAfterSpawn = true;

    [Header("CatchUp 設定")]
    [Tooltip("スポーン後、履歴レールへ滑らかに合流する処理を使うかです。")]
    [SerializeField] private bool useCatchUp = true;

    [Tooltip("CatchUp にかける最短時間です。")]
    [SerializeField] private float catchUpDuration = 0.2f;

    [Tooltip("CatchUp 中に現在位置から目標位置へ吸い付く強さです。大きいほど追従が強くなります。")]
    [SerializeField] private float catchUpFollowSharpness = 12.0f;

    [Tooltip("CatchUp 完了とみなす位置差です。これ以下まで寄ったら通常追尾へ入ります。")]
    [SerializeField] private float catchUpCompleteDistance = 0.08f;

    [Tooltip("CatchUp の進行カーブです。未設定や不正時は線形扱いになります。")]
    [SerializeField] private AnimationCurve catchUpCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("接触設定")]
    [Tooltip("この半径内にプレイヤー中心が入ったら即死扱いにします。")]
    [SerializeField] private float contactRadius = 0.4f;

    [Header("見た目設定")]
    [Tooltip("snapshot の facing に応じて左右反転するかです。")]
    [SerializeField] private bool applyFacingToVisual = true;

    [Header("デバッグ")]
    [Tooltip("参照中の目標位置を Gizmo で表示します。")]
    [SerializeField] private bool showTargetGizmo = true;

    [Tooltip("目標位置の Gizmo 色です。")]
    [SerializeField] private Color targetGizmoColor = Color.cyan;

    [Tooltip("現在の要求スポーン位置を Gizmo で表示します。")]
    [SerializeField] private bool showRequestedSpawnGizmo = true;

    [Tooltip("要求スポーン位置の Gizmo 色です。")]
    [SerializeField] private Color requestedSpawnGizmoColor = Color.yellow;

    [Tooltip("CatchUp 中の現在目標位置を Gizmo で表示します。")]
    [SerializeField] private bool showCatchUpTargetGizmo = true;

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

    // View 参照口
    public bool HasSnapshot => hasLastAppliedSnapshot;
    public PlayerShadowSnapshot CurrentSnapshot => lastAppliedSnapshot;

    private void Awake()
    {
        if (recorder == null)
        {
            recorder = FindFirstObjectByType<PlayerShadowRecorder>();
        }

        if (targetPlayer == null && recorder != null)
        {
            targetPlayer = recorder.GetComponent<PlayerController>();
        }

        if (visualRoot == null)
        {
            visualRoot = transform;
        }

        if (controlledRenderers == null || controlledRenderers.Length == 0)
        {
            controlledRenderers = GetComponentsInChildren<Renderer>(true);
        }

        defaultPosition = transform.position;
        defaultRotation = transform.rotation;

        if (visualRoot != null)
        {
            defaultVisualScale = visualRoot.localScale;
            initializedDefaultVisualScale = true;
        }

        currentRequestedSpawnPosition = defaultPosition;
        currentRequestedSpawnRotation = defaultRotation;
        hasRequestedSpawn = false;

        state = ShadowChaserState.Idle;
        SetVisible(!hideDuringSpawnDelay);
    }

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

    public void Activate(ShadowChaserSpawnRequest request)
    {
        if (state != ShadowChaserState.Idle)
        {
            return;
        }

        currentRequestedSpawnPosition = request.position;
        currentRequestedSpawnRotation = request.rotation;
        hasRequestedSpawn = true;

        MoveToRequestedSpawnImmediate();

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

    private IEnumerator CoSpawnSequence()
    {
        state = ShadowChaserState.SpawnDelay;

        Vector3 targetPosition = GetRequestedSpawnPosition();
        Quaternion targetRotation = GetRequestedSpawnRotation();

        if (hideDuringSpawnDelay)
        {
            SetVisible(false);
        }
        else
        {
            SetVisible(true);
        }

        transform.position = targetPosition;
        transform.rotation = targetRotation;

        if (spawnDelay > 0f)
        {
            yield return new WaitForSeconds(spawnDelay);
        }

        state = ShadowChaserState.Spawning;

        SetVisible(true);

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

        transform.position = endPosition;

        if (visualRoot != null)
        {
            visualRoot.localScale = endScale;
        }

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

    private void BeginCatchUpOrFollow()
    {
        if (!useCatchUp)
        {
            BeginFollow();
            return;
        }

        if (recorder == null)
        {
            BeginFollow();
            return;
        }

        if (!recorder.TryGetSnapshotAtDelay(delayTime, useInterpolation, out PlayerShadowSnapshot snapshot))
        {
            BeginFollow();
            return;
        }

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

    private void UpdateCatchUp()
    {
        if (recorder == null)
        {
            BeginFollow();
            return;
        }

        if (!recorder.TryGetSnapshotAtDelay(delayTime, useInterpolation, out PlayerShadowSnapshot snapshot))
        {
            return;
        }

        catchUpCurrentTargetPosition = snapshot.position;
        catchUpCurrentTargetRotation = snapshot.rotation;
        hasCatchUpTarget = true;

        lastAppliedSnapshot = snapshot;
        hasLastAppliedSnapshot = true;

        float duration = Mathf.Max(0.0001f, catchUpDuration);
        catchUpTimer += Time.deltaTime;

        float normalizedTime = Mathf.Clamp01(catchUpTimer / duration);

        float curveT = normalizedTime;
        if (catchUpCurve != null && catchUpCurve.length > 0)
        {
            curveT = catchUpCurve.Evaluate(normalizedTime);
        }

        float followLerpFactor = 1f - Mathf.Exp(-Mathf.Max(0.01f, catchUpFollowSharpness) * Time.deltaTime);

        Vector3 movingTargetPosition = Vector3.Lerp(
            transform.position,
            catchUpCurrentTargetPosition,
            followLerpFactor);

        Quaternion movingTargetRotation = Quaternion.Slerp(
            transform.rotation,
            catchUpCurrentTargetRotation,
            followLerpFactor);

        transform.position = Vector3.Lerp(
            catchUpStartPosition,
            movingTargetPosition,
            curveT);

        transform.rotation = Quaternion.Slerp(
            catchUpStartRotation,
            movingTargetRotation,
            curveT);

        ApplyFacingVisual(snapshot.facing);

        float remainDistance = Vector3.Distance(transform.position, catchUpCurrentTargetPosition);

        if (normalizedTime >= 1f && remainDistance <= Mathf.Max(0.001f, catchUpCompleteDistance))
        {
            BeginFollow();
        }
    }

    private void UpdateFollow()
    {
        if (recorder == null)
        {
            return;
        }

        if (!recorder.TryGetSnapshotAtDelay(delayTime, useInterpolation, out PlayerShadowSnapshot snapshot))
        {
            return;
        }

        ApplySnapshot(snapshot);

        lastAppliedSnapshot = snapshot;
        hasLastAppliedSnapshot = true;
    }

    private void ApplySnapshot(PlayerShadowSnapshot snapshot)
    {
        float distance = Vector3.Distance(transform.position, snapshot.position);

        if (distance >= snapDistance)
        {
            transform.position = snapshot.position;
        }
        else if (smoothFollow)
        {
            float lerpFactor = 1f - Mathf.Exp(-Mathf.Max(0.01f, followSmoothSharpness) * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, snapshot.position, lerpFactor);
        }
        else
        {
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

    private void CheckKillContact()
    {
        if (targetPlayer == null)
        {
            return;
        }

        Vector3 playerPosition = targetPlayer.transform.position;
        float distance = Vector3.Distance(transform.position, playerPosition);

        if (distance > contactRadius)
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