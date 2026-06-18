using System.Collections;
using UnityEngine;

// PlayerShadowRecorder の履歴を遅延再生する敵。
// delayTime 秒前のスナップショットをそのまま適用し、プレイヤーの「影」として追跡する。
// 見た目制御は ShadowChaserModelView 側に任せる。
[DisallowMultipleComponent]
public sealed class ShadowChaserEnemy : MonoBehaviour, IRespawnResettable
{
    // =========================================================
    // 内部ステート
    // =========================================================

    private enum ShadowChaserState
    {
        Idle,       // 非活性
        Appearing,  // 出現演出中
        Following,  // 追跡中
    }

    // =========================================================
    // インスペクター設定
    // =========================================================

    [Header("プレイヤー履歴レコーダー")]
    [Tooltip("追跡元になるプレイヤー履歴です。")]
    [SerializeField] private PlayerShadowRecorder recorder;

    [Header("対象プレイヤー")]
    [Tooltip("接触時に即死を要求する対象プレイヤーです。未設定時は recorder と同じ GameObject から取得を試みます。")]
    [SerializeField] private PlayerController targetPlayer;

    [Header("履歴再生")]
    [Tooltip("何秒前のプレイヤー状態を再生するかです。")]
    [SerializeField] private float delayTime = 0.4f;

    [Tooltip("履歴の前後 2 点を補間して再生するかです。")]
    [SerializeField] private bool useInterpolation = true;

    [Header("開始時有効化")]
    [Tooltip("true ならシーン開始時に出現演出へ入ります。")]
    [SerializeField] private bool isActiveOnStart = false;

    [Header("出現演出")]
    [Tooltip("出現演出の長さです。0 なら即座に追跡へ移行します。")]
    [SerializeField] private float appearDuration = 0.3f;

    [Tooltip("出現演出の開始位置オフセットです。")]
    [SerializeField] private Vector3 appearOffset = new Vector3(0f, -1f, 0f);

    [Tooltip("出現位置を固定スポーン位置ではなく、遅延履歴上の位置に合わせるかです。")]
    [SerializeField] private bool appearOnDelayedTrail = true;

    [Header("接触判定半径")]
    [Tooltip("この半径内にプレイヤー中心が入ったら即死扱いにします。")]
    [SerializeField] private float contactRadius = 0.4f;

    [Header("Gizmo 表示")]
    [Tooltip("現在参照中の履歴位置を Gizmo で表示します。")]
    [SerializeField] private bool showTargetGizmo = true;
    [Tooltip("履歴位置の Gizmo 色です。")]
    [SerializeField] private Color targetGizmoColor = Color.cyan;

    [Tooltip("起動時に渡されたスポーン位置を Gizmo で表示します。")]
    [SerializeField] private bool showSpawnGizmo = true;
    [Tooltip("スポーン位置の Gizmo 色です。")]
    [SerializeField] private Color spawnGizmoColor = Color.yellow;

    // =========================================================
    // ランタイム状態
    // =========================================================

    private ShadowChaserState state = ShadowChaserState.Idle;
    private Coroutine appearCoroutine;
    private float appearNormalizedTime = 1f;    // 出現演出の進捗（0〜1）、ShadowChaserModelView から参照される

    // 最後に適用したスナップショット（外部参照・Gizmo 用）
    private PlayerShadowSnapshot lastAppliedSnapshot;
    private bool hasLastAppliedSnapshot;

    // Awake 時点の Transform（デフォルト基準値）
    private Vector3    defaultPosition;
    private Quaternion defaultRotation;

    // 現在のスポーン情報（Gizmo 表示用）
    private Vector3    currentSpawnPosition;
    private Quaternion currentSpawnRotation;
    private bool       hasSpawnPosition;

    // リスポーン用 Transform スナップショット
    private bool       hasCapturedInitialState;
    private Vector3    initialPosition;
    private Quaternion initialRotation;

    // リスポーン用 設定値スナップショット
    private float   initialDelayTime;
    private bool    initialUseInterpolation;
    private bool    initialWasActiveOnStart;
    private float   initialAppearDuration;
    private Vector3 initialAppearOffset;
    private bool    initialAppearOnDelayedTrail;
    private float   initialContactRadius;
    private bool    initialShowTargetGizmo;
    private Color   initialTargetGizmoColor;
    private bool    initialShowSpawnGizmo;
    private Color   initialSpawnGizmoColor;

    // =========================================================
    // 公開プロパティ
    // =========================================================

    public bool HasSnapshot          => hasLastAppliedSnapshot;
    public bool IsAppearing          => state == ShadowChaserState.Appearing;
    public bool IsFollowing          => state == ShadowChaserState.Following;
    public float AppearNormalizedTime => appearNormalizedTime;
    public PlayerShadowSnapshot CurrentSnapshot => lastAppliedSnapshot;

    // =========================================================
    // Unity ライフサイクル
    // =========================================================

    private void Awake()
    {
        // 未設定の参照をシーンから自動解決する
        if (recorder == null)
            recorder = FindFirstObjectByType<PlayerShadowRecorder>();

        if (targetPlayer == null && recorder != null)
            targetPlayer = recorder.GetComponent<PlayerController>();

        // デフォルト Transform を記憶（リセット基準値）
        defaultPosition = transform.position;
        defaultRotation = transform.rotation;

        currentSpawnPosition = defaultPosition;
        currentSpawnRotation = defaultRotation;
        hasSpawnPosition     = false;

        state = ShadowChaserState.Idle;

        CaptureInitialState();
    }

    private void OnValidate()
    {
        delayTime      = Mathf.Max(0f, delayTime);
        appearDuration = Mathf.Max(0f, appearDuration);
        contactRadius  = Mathf.Max(0f, contactRadius);
    }

    private void Start()
    {
        // isActiveOnStart が有効なら即座に出現演出を開始する
        if (isActiveOnStart)
            Activate(defaultPosition, defaultRotation);
    }

    private void LateUpdate()
    {
        if (state != ShadowChaserState.Following)
            return;

        UpdateFollow();
        CheckKillContact();
    }

    // =========================================================
    // 公開 API
    // =========================================================

    // ScriptableObject の設定値を一括適用する
    public void ApplySettings(ShadowChaserSettings settings)
    {
        if (settings == null)
            return;

        delayTime            = Mathf.Max(0f, settings.delayTime);
        useInterpolation     = settings.useInterpolation;
        isActiveOnStart      = settings.isActiveOnStart;

        appearDuration       = Mathf.Max(0f, settings.appearDuration);
        appearOffset         = settings.appearOffset;
        appearOnDelayedTrail = settings.appearOnDelayedTrail;

        contactRadius        = Mathf.Max(0f, settings.contactRadius);

        showTargetGizmo      = settings.showTargetGizmo;
        targetGizmoColor     = settings.targetGizmoColor;
        showSpawnGizmo       = settings.showSpawnGizmo;
        spawnGizmoColor      = settings.spawnGizmoColor;

        StoreCurrentSettingsAsInitial();
    }

    // 現在の状態をリスポーン基準として記録する（2 回目以降は無視）
    public void CaptureInitialState()
    {
        if (hasCapturedInitialState)
            return;

        initialPosition = transform.position;
        initialRotation = transform.rotation;

        StoreCurrentSettingsAsInitial();
        hasCapturedInitialState = true;
    }

    // リスポーン時に初期状態へ戻す（IRespawnResettable 実装）
    public void ResetToRespawnState()
    {
        StopAppearCoroutine();

        state                  = ShadowChaserState.Idle;
        hasLastAppliedSnapshot = false;
        hasSpawnPosition       = false;
        appearNormalizedTime   = 0f;

        if (hasCapturedInitialState)
        {
            transform.position = initialPosition;
            transform.rotation = initialRotation;

            RestoreInitialSettings();

            // isActiveOnStart が有効だった場合は再起動する
            if (initialWasActiveOnStart)
                Activate(initialPosition, initialRotation);
        }
        else
        {
            // 初期状態未記録時はデフォルト値でフォールバック
            transform.position = defaultPosition;
            transform.rotation = defaultRotation;
        }
    }

    // 指定スポーン位置で出現演出を開始し、追跡を有効化する
    public void Activate(Vector3 spawnPosition, Quaternion spawnRotation)
    {
        // すでに活性状態なら無視する
        if (state != ShadowChaserState.Idle)
            return;

        currentSpawnPosition = spawnPosition;
        currentSpawnRotation = spawnRotation;
        hasSpawnPosition     = true;

        if (appearDuration <= 0f)
        {
            // 演出なし：即座に追跡開始
            BeginFollowImmediate(spawnPosition, spawnRotation);
            return;
        }

        StopAppearCoroutine();
        appearCoroutine = StartCoroutine(CoAppear(spawnPosition, spawnRotation));
    }

    // 追跡を停止し、Idle 状態へ戻す
    public void Deactivate()
    {
        StopAppearCoroutine();

        state                  = ShadowChaserState.Idle;
        hasLastAppliedSnapshot = false;
        hasSpawnPosition       = false;
        appearNormalizedTime   = 0f;
    }

    public bool IsActive()
    {
        return state != ShadowChaserState.Idle;
    }

    // =========================================================
    // 出現演出コルーチン
    // =========================================================

    // EaseOutCubic で補間しながら出現演出を実行し、完了後に Following へ移行する
    private IEnumerator CoAppear(Vector3 fallbackPosition, Quaternion fallbackRotation)
    {
        state                = ShadowChaserState.Appearing;
        appearNormalizedTime = 0f;

        // 演出終了目標：履歴位置が取れれば優先して使う
        Vector3              endPosition = fallbackPosition;
        Quaternion           endRotation = fallbackRotation;
        PlayerShadowSnapshot snapshot;

        if (appearOnDelayedTrail && TryGetCurrentRailTarget(out snapshot))
        {
            endPosition = snapshot.position;
            endRotation = snapshot.rotation;
            StoreSnapshot(snapshot);
        }

        // 開始位置はオフセット分ずらす
        Vector3    startPosition = endPosition + appearOffset;
        Quaternion startRotation = endRotation;

        transform.position = startPosition;
        transform.rotation = startRotation;

        float duration = Mathf.Max(0.0001f, appearDuration);
        float elapsed  = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float normalizedTime = Mathf.Clamp01(elapsed / duration);
            appearNormalizedTime = normalizedTime;                      // View 側へ進捗を公開する

            float t = EaseOutCubic(normalizedTime);

            // 演出中も目標位置を履歴に追従させる
            if (appearOnDelayedTrail && TryGetCurrentRailTarget(out snapshot))
            {
                endPosition = snapshot.position;
                endRotation = snapshot.rotation;
                StoreSnapshot(snapshot);
            }

            transform.position = Vector3.Lerp(startPosition, endPosition, t);
            transform.rotation = Quaternion.Slerp(startRotation, endRotation, t);

            yield return null;
        }

        // 演出完了：最終位置を確定する
        if (appearOnDelayedTrail && TryGetCurrentRailTarget(out snapshot))
            ApplySnapshotImmediate(snapshot);
        else
        {
            transform.position = endPosition;
            transform.rotation = endRotation;
        }

        appearNormalizedTime = 1f;
        state                = ShadowChaserState.Following;
        appearCoroutine      = null;
    }

    // =========================================================
    // 追跡ロジック
    // =========================================================

    // 演出なしで即座に Following 状態へ移行する
    private void BeginFollowImmediate(Vector3 fallbackPosition, Quaternion fallbackRotation)
    {
        if (appearOnDelayedTrail && TryGetCurrentRailTarget(out PlayerShadowSnapshot snapshot))
            ApplySnapshotImmediate(snapshot);
        else
        {
            transform.position = fallbackPosition;
            transform.rotation = fallbackRotation;
        }

        state                = ShadowChaserState.Following;
        appearNormalizedTime = 0f;
    }

    // 毎フレーム、遅延スナップショットを取得して位置を更新する
    private void UpdateFollow()
    {
        if (TryGetCurrentRailTarget(out PlayerShadowSnapshot snapshot))
            ApplySnapshotImmediate(snapshot);
    }

    // delayTime 秒前のスナップショットを recorder から取得する
    private bool TryGetCurrentRailTarget(out PlayerShadowSnapshot snapshot)
    {
        snapshot = default;

        if (recorder == null)
            return false;

        return recorder.TryGetSnapshotAtDelay(delayTime, useInterpolation, out snapshot);
    }

    // スナップショットを Transform へ即時適用する
    private void ApplySnapshotImmediate(PlayerShadowSnapshot snapshot)
    {
        transform.position = snapshot.position;
        transform.rotation = snapshot.rotation;

        StoreSnapshot(snapshot);
    }

    // 直近スナップショットを記録する（外部公開・Gizmo 用）
    private void StoreSnapshot(PlayerShadowSnapshot snapshot)
    {
        lastAppliedSnapshot    = snapshot;
        hasLastAppliedSnapshot = true;
    }

    // =========================================================
    // 接触判定
    // =========================================================

    // プレイヤーが contactRadius 以内に入ったらハザード死を要求する
    private void CheckKillContact()
    {
        if (targetPlayer == null)
            return;

        float sqrDistance      = (transform.position - targetPlayer.transform.position).sqrMagnitude;
        float sqrContactRadius = contactRadius * contactRadius;

        if (sqrDistance <= sqrContactRadius)
            targetPlayer.RequestHazardDeath();
    }

    // =========================================================
    // 設定値の保存・復元
    // =========================================================

    // 現在の設定値をリスポーン用スナップショットとして保存する
    private void StoreCurrentSettingsAsInitial()
    {
        initialDelayTime            = delayTime;
        initialUseInterpolation     = useInterpolation;
        initialWasActiveOnStart     = isActiveOnStart;

        initialAppearDuration       = appearDuration;
        initialAppearOffset         = appearOffset;
        initialAppearOnDelayedTrail = appearOnDelayedTrail;

        initialContactRadius        = contactRadius;

        initialShowTargetGizmo      = showTargetGizmo;
        initialTargetGizmoColor     = targetGizmoColor;
        initialShowSpawnGizmo       = showSpawnGizmo;
        initialSpawnGizmoColor      = spawnGizmoColor;
    }

    // リスポーン用スナップショットから設定値を復元する
    private void RestoreInitialSettings()
    {
        delayTime            = initialDelayTime;
        useInterpolation     = initialUseInterpolation;
        isActiveOnStart      = initialWasActiveOnStart;

        appearDuration       = initialAppearDuration;
        appearOffset         = initialAppearOffset;
        appearOnDelayedTrail = initialAppearOnDelayedTrail;

        contactRadius        = initialContactRadius;

        showTargetGizmo      = initialShowTargetGizmo;
        targetGizmoColor     = initialTargetGizmoColor;
        showSpawnGizmo       = initialShowSpawnGizmo;
        spawnGizmoColor      = initialSpawnGizmoColor;
    }

    // =========================================================
    // ユーティリティ
    // =========================================================

    private void StopAppearCoroutine()
    {
        if (appearCoroutine == null)
            return;

        StopCoroutine(appearCoroutine);
        appearCoroutine = null;
    }

    // 3 次イーズアウト（1 − (1−t)³）
    private static float EaseOutCubic(float t)
    {
        t = Mathf.Clamp01(t);
        float inv = 1f - t;
        return 1f - inv * inv * inv;
    }

    // =========================================================
    // Gizmo
    // =========================================================

    private void OnDrawGizmosSelected()
    {
        // 接触判定円
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, contactRadius);

        // 現在の追跡目標位置
        if (showTargetGizmo && hasLastAppliedSnapshot)
        {
            Gizmos.color = targetGizmoColor;
            Gizmos.DrawSphere(lastAppliedSnapshot.position, 0.08f);
        }

        // スポーン位置とオフセット方向
        if (showSpawnGizmo)
        {
            Vector3 spawnPosition = hasSpawnPosition ? currentSpawnPosition : transform.position;

            Gizmos.color = spawnGizmoColor;
            Gizmos.DrawWireSphere(spawnPosition, 0.15f);
            Gizmos.DrawLine(spawnPosition, spawnPosition + appearOffset);
        }
    }
}