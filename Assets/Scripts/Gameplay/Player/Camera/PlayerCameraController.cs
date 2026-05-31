using UnityEngine;
using UnityEngine.Serialization;

// 同じ GameObject に複数付けて、カメラ制御が競合しないようにする。
[DisallowMultipleComponent]
// このコンポーネントは Camera を必須とする。（※CameraShakeManagerと分離するためRequireComponentを解除しました）
// [RequireComponent(typeof(Camera))]
public sealed class PlayerCameraController : MonoBehaviour
{
    // -----------------------------
    // Inspector 設定値
    // -----------------------------

    [Header("追従に使用するカメラ")]
    [Tooltip("追従処理と画面サイズ計算に使う Camera 参照です。通常はこの GameObject 自身の Camera を設定します。Orthographic 前提の実装です。")]
    // カメラ追従の計算に使う Camera。
    // null の場合は Awake / Reset で同一 GameObject から取得を試みる。
    [SerializeField] private Camera targetCamera;

    [Header("追従対象アンカー")]
    [Tooltip("カメラが追従する基準位置です。通常はプレイヤー直下の CameraTargetAnchor などを設定します。未設定時は自動探索設定に応じて補完を試みます。")]
    // 通常追従で使う対象位置。
    [SerializeField] private Transform targetAnchor;

[Header("プレイヤー状態 Facade")]
[Tooltip("横・上下方向ダッシュカメラ補正に使うプレイヤー状態の読み取り窓口です。未設定時は PlayerFacade を自動探索します。")]
    [SerializeField] private PlayerFacade playerFacade;

    [Header("ワールド全体のfallback境界")]
    [Tooltip("常に使える基本のカメラ移動境界です。エリア別の一時境界が未設定のときはこの境界を使ってカメラ位置を制限します。")]
    [FormerlySerializedAs("RoomBounds")]
    // 通常時に使うカメラ移動可能範囲。
    // activeBounds が無いときのフォールバックとして使う。
    [SerializeField] private RoomBounds worldBounds;

    [Header("アンカー自動探索を使うか")]
    [Tooltip("有効にすると、targetAnchor 未設定時に playerTag と anchorChildName を使って追従対象を自動探索します。手動設定を優先したい場合は無効にします。")]
    // true のとき、targetAnchor が未設定なら自動でプレイヤーを探す。
    [SerializeField] private bool autoFindPlayerAnchor = true;

    [Header("プレイヤー探索用タグ")]
    [Tooltip("アンカー自動探索時にプレイヤーを見つけるためのタグ名です。GameObject.FindWithTag で検索されるため、実際のタグ設定と一致させてください。")]
    // 自動探索時にプレイヤーを見つけるためのタグ名。
    [SerializeField] private string playerTag = "Player";

    [Header("アンカー子オブジェクト名")]
    [Tooltip("プレイヤー配下から探す追従用アンカーの子オブジェクト名です。見つからない場合はプレイヤー本体 Transform を追従対象として使います。")]
    // プレイヤー配下から探す追従アンカーの名前。
    // 見つからなければプレイヤー本体 Transform を使う。
    [SerializeField] private string anchorChildName = "CameraTargetAnchor";

    [Header("カメラオフセット")]
    [Tooltip("追従対象アンカーに加算するカメラ位置オフセットです。2D では通常 Z を負値にしてカメラを手前へ置きます。")]
    // 追従対象に対して、どれだけずらした位置にカメラを置くか。
    // 2D では一般的に Z = -10 付近を使う。
    [SerializeField] private Vector3 cameraOffset = new Vector3(0f, 0f, -10f);

    [Header("X軸追従スムーズ時間")]
    [Tooltip("X 軸方向の追従をどれくらい滑らかにするかの時間です。小さいほど素早く追従し、大きいほど遅れて追従します。")]
    // X 方向の追従の滑らかさ。
    // 小さいと即追従、大きいと遅れて追従する。
    [SerializeField] private float smoothTimeX = 0.08f;

    [Header("Y軸追従スムーズ時間")]
    [Tooltip("Y 軸方向の追従をどれくらい滑らかにするかの時間です。ジャンプや落下の見え方に影響します。小さいほど即追従、大きいほどゆったり追従します。")]
    // Y 方向の追従の滑らかさ。
    // ジャンプや落下時のカメラ感触に強く影響する。
    [SerializeField] private float smoothTimeY = 0.12f;

    [Header("横方向ダッシュカメラ補正を使うか")]
    [Tooltip("有効にすると、横方向ダッシュ時だけ X 軸追従と横方向 LookAhead に疾走感用の補正を加えます。")]
[FormerlySerializedAs("dashCameraEnabled")]
[SerializeField] private bool horizontalDashCameraEnabled = true;

    [Header("横方向ダッシュ判定 X しきい値")]
    [Tooltip("ダッシュ方向の X 絶対値がこの値以上の場合に、横方向ダッシュ候補として扱います。")]
    [SerializeField] private float horizontalDashThreshold = 0.75f;

    [Header("横方向ダッシュ判定 Y 除外しきい値")]
    [Tooltip("ダッシュ方向の Y 絶対値がこの値以下の場合のみ補正します。上下・斜め方向ダッシュを除外するための値です。")]
    [SerializeField] private float verticalDashIgnoreThreshold = 0.25f;

    [Header("横方向ダッシュ開始遅延時間")]
    [Tooltip("横方向ダッシュ開始直後に、専用の遅めの X 軸追従を使う時間です。")]
    [SerializeField] private float dashStartLagDuration = 0.08f;

    [Header("横方向ダッシュ開始直後の X 軸スムーズ時間")]
    [Tooltip("横方向ダッシュ開始直後の遅延中に使う X 軸 SmoothDamp 時間です。大きいほど追従が遅れます。")]
    [SerializeField] private float dashStartSmoothTimeX = 0.16f;

    [Header("横方向ダッシュ中の X 軸スムーズ時間")]
    [Tooltip("開始直後の遅延が終わった後、横方向ダッシュ中に使う X 軸 SmoothDamp 時間です。")]
    [SerializeField] private float dashSmoothTimeX = 0.08f;

    [Header("横方向ダッシュ LookAhead 距離")]
    [Tooltip("横方向ダッシュ中に進行方向へ加算するカメラ LookAhead の距離です。Bounds Clamp 前に適用します。")]
    [SerializeField] private float dashLookAheadX = 1.2f;

    [Header("横方向ダッシュ LookAhead 復帰時間")]
    [Tooltip("横方向ダッシュ LookAhead を滑らかに反映し、ダッシュ終了後に 0 へ戻す SmoothDamp 時間です。")]
    [SerializeField] private float dashLookAheadReturnTime = 0.18f;

[Header("上下方向ダッシュカメラ補正を使うか")]
[Tooltip("有効にすると、上下方向ダッシュ時だけ Y 軸追従と上下方向 LookAhead に疾走感用の補正を加えます。")]
[SerializeField] private bool verticalDashCameraEnabled = true;

[Header("上下方向ダッシュ判定 Y しきい値")]
[Tooltip("ダッシュ方向の Y 絶対値がこの値以上の場合に、上下方向ダッシュ候補として扱います。")]
[SerializeField] private float verticalDashThreshold = 0.75f;

[Header("上下方向ダッシュ判定 X 除外しきい値")]
[Tooltip("ダッシュ方向の X 絶対値がこの値以下の場合のみ補正します。横・斜め方向ダッシュを除外するための値です。")]
[SerializeField] private float horizontalDashIgnoreThreshold = 0.25f;

[Header("上下方向ダッシュ開始遅延時間")]
[Tooltip("上下方向ダッシュ開始直後に、専用の遅めの Y 軸追従を使う時間です。")]
[SerializeField] private float verticalDashStartLagDuration = 0.05f;

[Header("上下方向ダッシュ開始直後の Y 軸スムーズ時間")]
[Tooltip("上下方向ダッシュ開始直後の遅延中に使う Y 軸 SmoothDamp 時間です。大きいほど追従が遅れます。")]
[SerializeField] private float verticalDashStartSmoothTimeY = 0.14f;

[Header("上下方向ダッシュ中の Y 軸スムーズ時間")]
[Tooltip("開始直後の遅延が終わった後、上下方向ダッシュ中に使う Y 軸 SmoothDamp 時間です。")]
[SerializeField] private float verticalDashSmoothTimeY = 0.08f;

[Header("上下方向ダッシュ LookAhead 距離")]
[Tooltip("上下方向ダッシュ中に進行方向へ加算するカメラ LookAhead の距離です。Bounds Clamp 前に適用します。")]
[SerializeField] private float verticalDashLookAheadY = 0.8f;

[Header("上下方向ダッシュ LookAhead 復帰時間")]
[Tooltip("上下方向ダッシュ LookAhead を滑らかに反映し、ダッシュ終了後に 0 へ戻す SmoothDamp 時間です。")]
[SerializeField] private float verticalDashLookAheadReturnTime = 0.14f;

    [Header("Orthographic Size 補間時間")]
    [Tooltip("Room ベースの orthographicSize 上書きが切り替わる際の補間時間です。0 のときは即時反映します。")]
    // orthographicSize の切り替えをどれくらい滑らかにするか。
    [SerializeField] private float orthographicSizeSmoothTime = 0.10f;

    [Header("部屋遷移カメラ時間")]
    [Tooltip("部屋遷移モード時に開始位置から目標位置まで移動する標準時間です。Room 側の上書きがある場合はそちらを優先します。")]
    // 部屋遷移時の基本移動時間。
    // 短すぎると終端の変化が目立ちやすいので少し長めにしている。
    [SerializeField] private float defaultRoomTransitionDuration = 0.30f;

    [Tooltip("部屋遷移完了判定用の許容距離です。")]
    // 外部参照用に残している到達判定距離。
    // 実際の遷移終了判定は時間基準で行う。
    [SerializeField] private float roomTransitionCompleteDistance = 0.005f;

    [Header("ルーム確認カメラ")]
    [Tooltip("ルーム確認モード中のカメラ移動速度です。")]
    [SerializeField] private float roomLookMoveSpeed = 14.0f;

    [Tooltip("ルーム確認モード終了時にプレイヤー追従位置へ戻る時間です。")]
    [SerializeField] private float roomLookReturnDuration = 0.20f;

    [Header("デバッグGizmoを描画するか")]
    [Tooltip("有効にすると、Scene ビュー上に目標位置・Clamp 後位置・追従対象位置の Gizmo を描画します。カメラ挙動確認用です。")]
    // Scene ビュー上にデバッグ用 Gizmo を描画するか。
    [SerializeField] private bool drawDebugGizmos = true;

    [Header("Viewport位置をログ出力するか")]
    [Tooltip("有効にすると、追従対象アンカーの Viewport 座標を毎フレームログ出力します。画面内の見え方確認用ですが、ログ量は増えます。")]
    // true のとき、追従対象の Viewport 座標を毎フレーム出力する。
    // ログが大量に出るので常時 ON にはしない方がよい。
    [SerializeField] private bool logViewportPosition = false;

    // -----------------------------
    // ランタイム状態
    // -----------------------------

    // Room ベースで一時的に上書きされる現在有効な境界。
    // true のときだけ activeBoundsOverride を使う。
    private bool hasActiveBoundsOverride;

    // Room ベースで直接渡されるワールド座標系 Bounds。
    // hasActiveBoundsOverride == true の間だけ有効。
    private Bounds activeBoundsOverride;

    // 通常時に戻るための World 基準 Orthographic Size。
    private float worldOrthographicSize;

    // Room ベースの Orthographic Size 一時上書き。
    private bool hasActiveOrthographicSizeOverride;
    private float activeOrthographicSizeOverride;
    private float orthographicSizeVelocity;

    // 通常時に戻るための World 基準 追従スムーズ時間。
    private float worldSmoothTimeX;
    private float worldSmoothTimeY;

    // Room ベースの追従スムーズ時間 一時上書き。
    private bool hasActiveFollowSmoothingOverride;
    private float activeSmoothTimeXOverride;
    private float activeSmoothTimeYOverride;

    // 通常時に戻るための World 基準 Orthographic Size 補間時間。
    private float worldOrthographicSizeSmoothTime;

    // Room ベースの Orthographic Size 補間時間 一時上書き。
    private bool hasActiveOrthographicSizeSmoothTimeOverride;
    private float activeOrthographicSizeSmoothTimeOverride;

    // Room からの注視オフセット 一時上書き。
    private bool hasActiveRoomFocusOffset;
    private Vector2 activeRoomFocusOffset;

    // SmoothDamp 用の内部速度。
    // ref で渡してフレーム間で保持する必要がある。
    private float velocityX;
    private float velocityY;

    // 横方向ダッシュカメラ補正のランタイム状態。
    private float dashStartLagTimer;
    private int dashHorizontalDirectionSign;
    private float dashLookAheadCurrentX;
    private float dashLookAheadSmoothDampVelocityX;
    private bool isHorizontalDashCameraActive;
    private float activeSmoothTimeXForDebug;

    // 上下方向ダッシュカメラ補正のランタイム状態。
    private float verticalDashStartLagTimer;
    private int verticalDashDirectionSign;
    private float verticalDashLookAheadCurrentY;
    private float verticalDashLookAheadSmoothDampVelocityY;
    private bool isVerticalDashCameraActive;
    private float activeSmoothTimeYForDebug;

    // ダッシュ開始フレームを Facade 経由で補完するためのランタイム状態。
    private bool wasDashActivePreviousFrame;

    // 一時追従ターゲットのランタイム状態。
    // Inspector では設定せず、SetTemporaryTarget / ClearTemporaryTarget でのみ変更する。
    private Transform temporaryTargetAnchor;

    // 一時追従ターゲットの有効期限( Time.time 基準 )。
    private float temporaryTargetExpireTime = -1f;

    // Clamp 前の「行きたい位置」。
    private Vector3 desiredPosition;

    // 境界適用後の「実際に目指す位置」。
    private Vector3 clampedPosition;

    // 部屋遷移モードのランタイム状態。
    private bool isRoomTransitionRunning;
    private float activeRoomTransitionDuration;
    private float roomTransitionElapsed;
    private Vector3 roomTransitionStartPosition;
    private Vector3 roomTransitionTargetPosition;

    // ルーム確認カメラモードのランタイム状態。
    private bool isRoomLookRunning;
    private bool isRoomLookReturning;
    private Bounds roomLookBounds;
    private Vector3 roomLookManualPosition;
    private Vector3 roomLookReturnStartPosition;
    private Vector3 roomLookReturnTargetPosition;
    private float roomLookReturnElapsed;

    // -----------------------------
    // 読み取り専用公開プロパティ
    // -----------------------------

    // 実際に使用するワールド座標系境界。
    // override が有効ならそちらを優先し、無ければ worldBounds を使う。
    private Bounds EffectiveWorldBounds => hasActiveBoundsOverride
        ? activeBoundsOverride
        : worldBounds.WorldBounds;

    // 実際に使用する Orthographic Size。
    // override が有効ならそちらを優先し、無ければ world の通常値を使う。
    private float EffectiveOrthographicSize => hasActiveOrthographicSizeOverride
        ? activeOrthographicSizeOverride
        : worldOrthographicSize;

    // 実際に使用する X/Y 追従スムーズ時間。
    private float EffectiveSmoothTimeX => hasActiveFollowSmoothingOverride
        ? activeSmoothTimeXOverride
        : worldSmoothTimeX;

    private float EffectiveSmoothTimeY => hasActiveFollowSmoothingOverride
        ? activeSmoothTimeYOverride
        : worldSmoothTimeY;

    // 実際に使用する Orthographic Size 補間時間。
    private float EffectiveOrthographicSizeSmoothTime => hasActiveOrthographicSizeSmoothTimeOverride
        ? activeOrthographicSizeSmoothTimeOverride
        : worldOrthographicSizeSmoothTime;

    // デバッグや外部参照用の読み取り専用公開プロパティ。
    public Vector3 DesiredPosition => desiredPosition;
    public Vector3 ClampedPosition => clampedPosition;
    public bool HasActiveBoundsOverride => hasActiveBoundsOverride;
    public Bounds ActiveBoundsOverride => activeBoundsOverride;
    public RoomBounds WorldBounds => worldBounds;
    public Bounds EffectiveBounds => EffectiveWorldBounds;
    public bool HasActiveOrthographicSizeOverride => hasActiveOrthographicSizeOverride;
    public float ActiveOrthographicSizeOverride => activeOrthographicSizeOverride;
    public float EffectiveSize => EffectiveOrthographicSize;
    public bool HasActiveFollowSmoothingOverride => hasActiveFollowSmoothingOverride;
    public float ActiveSmoothTimeXOverride => activeSmoothTimeXOverride;
    public float ActiveSmoothTimeYOverride => activeSmoothTimeYOverride;
    public float EffectiveFollowSmoothTimeX => EffectiveSmoothTimeX;
    public float EffectiveFollowSmoothTimeY => EffectiveSmoothTimeY;
    public bool HasActiveOrthographicSizeSmoothTimeOverride => hasActiveOrthographicSizeSmoothTimeOverride;
    public float ActiveOrthographicSizeSmoothTimeOverride => activeOrthographicSizeSmoothTimeOverride;
    public float EffectiveSizeSmoothTime => EffectiveOrthographicSizeSmoothTime;
    public bool HasTemporaryTarget => temporaryTargetAnchor != null && !IsTemporaryTargetExpired();
    public Transform TemporaryTargetAnchor => temporaryTargetAnchor;
    public float TemporaryTargetExpireTime => temporaryTargetExpireTime;
    public bool IsRoomTransitionRunning => isRoomTransitionRunning;
    public bool IsRoomLookRunning => isRoomLookRunning;
    public bool IsRoomLookReturning => isRoomLookReturning;
    public bool IsRoomLookActive => isRoomLookRunning || isRoomLookReturning;

    // 遷移中でなければ到達済み扱い。
    // 遷移中は現在位置と目標位置の距離で外部から確認できるようにする。
    public bool HasReachedRoomTransitionTarget => !isRoomTransitionRunning
        || Vector3.Distance(transform.position, roomTransitionTargetPosition) <= Mathf.Max(0f, roomTransitionCompleteDistance);

    // -----------------------------
    // Unity lifecycle
    // -----------------------------

    private void Reset()
    {
        // コンポーネント追加時や Reset 時に、同一 GameObject の Camera を自動設定する。
        targetCamera = GetComponent<Camera>();
    }

    private void Awake()
    {
        if (targetCamera == null)
        {
            targetCamera = GetComponent<Camera>();
        }

        if (targetCamera != null)
        {
            worldOrthographicSize = Mathf.Max(0.01f, targetCamera.orthographicSize);
        }

        worldSmoothTimeX = Mathf.Max(0f, smoothTimeX);
        worldSmoothTimeY = Mathf.Max(0f, smoothTimeY);
        worldOrthographicSizeSmoothTime = Mathf.Max(0f, orthographicSizeSmoothTime);

        // 必要に応じて追従対象アンカーとプレイヤー状態 Facade を自動解決する。
        ResolveTargetAnchor();
        ResolvePlayerFacade();
    }

    private void LateUpdate()
    {
        if (!ValidateRuntimeReferences())
        {
            return;
        }

        ApplyOrthographicSize();

        if (isRoomLookRunning)
        {
            // ルーム確認モード中は RoomLookModeController 側から
            // UpdateRoomLookInput が呼ばれてカメラ位置を更新する。
            // 通常追従は止める。
            return;
        }

        if (isRoomLookReturning)
        {
            TickRoomLookReturn();
            return;
        }

        Transform effectiveTarget = GetEffectiveTargetAnchor();
        if (effectiveTarget == null)
        {
            return;
        }

        if (!isRoomTransitionRunning)
        {
            TickDashCamera();
        }

        // 追従対象位置 + カメラオフセット(+必要なら部屋注視オフセット) + 横・上下方向ダッシュ LookAhead で理想位置を作る。
        desiredPosition = BuildFollowDesiredPosition(effectiveTarget);

        // 理想位置をカメラ境界内に収めた最終候補位置を作る。
        clampedPosition = GetClampedPosition(desiredPosition);

        if (isRoomTransitionRunning)
        {
            // 部屋遷移モード中は、開始点と目標点を補間して通常追従は止める。
            roomTransitionElapsed += Time.deltaTime;

            float linearT = activeRoomTransitionDuration <= 0f
                ? 1f
                : Mathf.Clamp01(roomTransitionElapsed / activeRoomTransitionDuration);

            // Linear の進行率を EaseInOut に変換する。
            float easedT = EvaluateRoomTransitionEaseInOut(linearT);

            transform.position = Vector3.Lerp(
                roomTransitionStartPosition,
                roomTransitionTargetPosition,
                easedT);

            // 距離しきい値で早期終了すると終端でガクつきやすい。
            // そのため、遷移終了は「時間が最後まで進み切ったか」で判定する。
            if (linearT >= 1f)
            {
                EndRoomTransition();
            }

            return;
        }

        // X と Y を別々の SmoothDamp で補間する。
        // これにより、軸ごとの追従感を個別に調整できる。
        float finalX = Mathf.SmoothDamp(
            current: transform.position.x,
            target: clampedPosition.x,
            currentVelocity: ref velocityX,
            smoothTime: activeSmoothTimeXForDebug);

        float finalY = Mathf.SmoothDamp(
            current: transform.position.y,
            target: clampedPosition.y,
            currentVelocity: ref velocityY,
            smoothTime: activeSmoothTimeYForDebug);

        // Z は Clamp 後位置をそのまま採用する。
        transform.position = new Vector3(finalX, finalY, clampedPosition.z);

        // 必要なら、追従対象が画面内のどこに居るかを Viewport 座標で確認する。
        if (logViewportPosition)
        {
            Vector3 viewport = targetCamera.WorldToViewportPoint(effectiveTarget.position);
            //Debug.Log(
            //    $"CameraTarget Viewport : x={viewport.x:F2}, y={viewport.y:F2}",
            //    this);
        }
    }

    // -----------------------------
    // 初期化・参照解決
    // -----------------------------

    private bool ValidateRuntimeReferences()
    {
        // Camera が無ければ追従計算できない。
        if (targetCamera == null)
        {
            Debug.LogWarning("PlayerCameraController: Camera reference is missing.", this);
            return false;
        }

        // Anchor が無ければ自動探索を試みる。
        if (targetAnchor == null)
        {
            ResolveTargetAnchor();

            // それでも見つからなければ更新不能。
            if (targetAnchor == null)
            {
                Debug.LogWarning("PlayerCameraController: Target anchor is missing.", this);
                return false;
            }
        }

        // ダッシュ補正は任意機能なので、Facade が無ければ補正無しで通常追従を続ける。
        ResolvePlayerFacade();

        // フォールバック境界が無いと Clamp 計算ができない。
        if (worldBounds == null)
        {
            Debug.LogWarning("PlayerCameraController: World bounds reference is missing.", this);
            return false;
        }

        // この実装は Orthographic カメラ前提。
        if (!targetCamera.orthographic)
        {
            Debug.LogWarning("PlayerCameraController: This version assumes an Orthographic camera.", this);
            return false;
        }

        return true;
    }

    private void ResolvePlayerFacade()
    {
        if (playerFacade == null)
        {
            playerFacade = FindFirstObjectByType<PlayerFacade>();
        }
    }

    private void ResolveTargetAnchor()
    {
        // 自動探索を使わない、または既に設定済みなら何もしない。
        if (!autoFindPlayerAnchor || targetAnchor != null)
        {
            return;
        }

        GameObject player = null;

        try
        {
            // 指定タグのプレイヤーを探す。
            player = GameObject.FindWithTag(playerTag);
        }
        catch (UnityException)
        {
            // タグ未定義などで例外が出るケースを吸収する。
            return;
        }

        // プレイヤーが見つからなければ終了。
        if (player == null)
        {
            return;
        }

        // プレイヤー配下に指定名のアンカーがあるか探す。
        Transform foundAnchor = player.transform.Find(anchorChildName);

        // アンカーがあればそれを使い、無ければプレイヤー本体を追従対象にする。
        targetAnchor = foundAnchor != null ? foundAnchor : player.transform;
    }

    // -----------------------------
    // temporary target 管理
    // -----------------------------

    public void SetTemporaryTarget(Transform target, float duration)
    {
        if (target == null)
        {
            return;
        }

        temporaryTargetAnchor = target;
        temporaryTargetExpireTime = Time.time + Mathf.Max(0.01f, duration);
    }

    public void ClearTemporaryTarget()
    {
        temporaryTargetAnchor = null;
        temporaryTargetExpireTime = -1f;
    }

    public Transform GetEffectiveTargetAnchor()
    {
        if (temporaryTargetAnchor != null && !IsTemporaryTargetExpired())
        {
            return temporaryTargetAnchor;
        }

        if (temporaryTargetAnchor != null && IsTemporaryTargetExpired())
        {
            ClearTemporaryTarget();
        }

        return targetAnchor;
    }

    private bool IsTemporaryTargetExpired()
    {
        return temporaryTargetExpireTime <= Time.time;
    }

    // -----------------------------
    // リスポーン時のカメラ状態リセット
    // -----------------------------

    public void ResetRuntimeStateForRespawn()
    {
        ResetCameraMotionForRespawn();
    }

    public void ResetCameraMotionForRespawn()
    {
        // リスポーン後に追従対象を通常状態へ戻す。
        ClearTemporaryTarget();

        // SmoothDamp の内部速度を初期化して慣性を除去する。
        velocityX = 0f;
        velocityY = 0f;
        orthographicSizeVelocity = 0f;
        ResetDashCameraState();

        // ルーム確認モード中なら通常追従位置へ戻す。
        if (isRoomLookRunning || isRoomLookReturning)
        {
            CancelRoomLookAndSnapToFollow();
        }

        // 部屋遷移中なら現在の目標位置へスナップして遷移を終了する。
        if (isRoomTransitionRunning)
        {
            CancelRoomTransitionAndSnapToTarget();
        }
    }

    // -----------------------------
    // Room 反映時の内部 override 操作 API
    // -----------------------------

    public void SetActiveBoundsOverride(Bounds newBounds)
    {
        activeBoundsOverride = newBounds;
        hasActiveBoundsOverride = true;
    }

    public void ClearActiveBoundsOverride()
    {
        hasActiveBoundsOverride = false;
    }

    public void SetActiveOrthographicSizeOverride(float newSize)
    {
        activeOrthographicSizeOverride = Mathf.Max(0.01f, newSize);
        hasActiveOrthographicSizeOverride = true;
    }

    public void ClearActiveOrthographicSizeOverride()
    {
        hasActiveOrthographicSizeOverride = false;
    }

    public void SetActiveFollowSmoothingOverride(float newSmoothTimeX, float newSmoothTimeY)
    {
        activeSmoothTimeXOverride = Mathf.Max(0f, newSmoothTimeX);
        activeSmoothTimeYOverride = Mathf.Max(0f, newSmoothTimeY);
        hasActiveFollowSmoothingOverride = true;
    }

    public void ClearActiveFollowSmoothingOverride()
    {
        hasActiveFollowSmoothingOverride = false;
    }

    public void SetActiveOrthographicSizeSmoothTimeOverride(float newSmoothTime)
    {
        activeOrthographicSizeSmoothTimeOverride = Mathf.Max(0f, newSmoothTime);
        hasActiveOrthographicSizeSmoothTimeOverride = true;
    }

    public void ClearActiveOrthographicSizeSmoothTimeOverride()
    {
        hasActiveOrthographicSizeSmoothTimeOverride = false;
    }

    public void SetActiveRoomFocusOffset(Vector2 focusOffset)
    {
        activeRoomFocusOffset = focusOffset;
        hasActiveRoomFocusOffset = true;
    }

    public void ClearActiveRoomFocusOffset()
    {
        hasActiveRoomFocusOffset = false;
        activeRoomFocusOffset = Vector2.zero;
    }

    public void ApplyRoomCameraSettings(Room room)
    {
        // null の部屋は反映せずに警告だけ出す。
        if (room == null)
        {
            Debug.LogWarning("PlayerCameraController: ApplyRoomCameraSettings に null が渡されました。", this);
            return;
        }

        // 部屋境界がある場合は境界 override を反映し、無い場合は world 境界に戻す。
        if (room.RoomBounds != null)
        {
            SetActiveBoundsOverride(room.RoomBounds.WorldBounds);
        }
        else
        {
            ClearActiveBoundsOverride();
        }

        // 部屋注視オフセットは常に即反映する。
        SetActiveRoomFocusOffset(room.RoomFocusOffset);

        // 追従スムーズ override の有無に応じて反映を切り替える。
        if (room.HasFollowSmoothingOverride)
        {
            SetActiveFollowSmoothingOverride(room.SmoothTimeX, room.SmoothTimeY);
        }
        else
        {
            ClearActiveFollowSmoothingOverride();
        }

        // orthographic size override の有無に応じて反映を切り替える。
        if (room.HasOrthographicSizeOverride)
        {
            SetActiveOrthographicSizeOverride(room.OrthographicSize);
        }
        else
        {
            ClearActiveOrthographicSizeOverride();
        }

        // orthographic size 補間時間 override の有無に応じて反映を切り替える。
        if (room.HasOrthographicSizeSmoothTimeOverride)
        {
            SetActiveOrthographicSizeSmoothTimeOverride(room.OrthographicSizeSmoothTime);
        }
        else
        {
            ClearActiveOrthographicSizeSmoothTimeOverride();
        }
    }

    public Bounds GetEffectiveBounds()
    {
        return EffectiveWorldBounds;
    }

    // -----------------------------
    // ルーム確認カメラモード
    // -----------------------------

    public bool BeginRoomLook(Bounds lookBounds)
    {
        if (!ValidateRuntimeReferences())
        {
            return false;
        }

        if (isRoomTransitionRunning)
        {
            Debug.LogWarning("PlayerCameraController: 部屋遷移中のため RoomLook を開始できません。", this);
            return false;
        }

        ResetDashCameraState();

        roomLookBounds = lookBounds;
        isRoomLookRunning = true;
        isRoomLookReturning = false;
        roomLookReturnElapsed = 0f;

        // 現在位置を基準に、RoomLook 用 Bounds 内へ収める。
        desiredPosition = transform.position;
        clampedPosition = GetClampedPositionInBounds(desiredPosition, roomLookBounds);

        roomLookManualPosition = clampedPosition;
        transform.position = roomLookManualPosition;

        // 通常追従の慣性を持ち越さない。
        velocityX = 0f;
        velocityY = 0f;

        return true;
    }

    public void UpdateRoomLookInput(Vector2 lookInput, float deltaTime)
    {
        if (!isRoomLookRunning)
        {
            return;
        }

        Vector2 clampedInput = Vector2.ClampMagnitude(lookInput, 1.0f);

        Vector3 moveDelta = new Vector3(
            clampedInput.x,
            clampedInput.y,
            0f) * Mathf.Max(0f, roomLookMoveSpeed) * Mathf.Max(0f, deltaTime);

        desiredPosition = roomLookManualPosition + moveDelta;
        desiredPosition.z = transform.position.z;

        clampedPosition = GetClampedPositionInBounds(desiredPosition, roomLookBounds);

        roomLookManualPosition = clampedPosition;
        transform.position = roomLookManualPosition;
    }

    public void EndRoomLook()
    {
        if (!isRoomLookRunning)
        {
            return;
        }

        isRoomLookRunning = false;
        isRoomLookReturning = true;
        roomLookReturnElapsed = 0f;

        roomLookReturnStartPosition = transform.position;
        roomLookReturnTargetPosition = ComputeFollowClampedPosition();

        velocityX = 0f;
        velocityY = 0f;
    }

    public void CancelRoomLookAndSnapToFollow()
    {
        ResetDashCameraState();

        isRoomLookRunning = false;
        isRoomLookReturning = false;

        Vector3 targetPosition = ComputeFollowClampedPosition();
        transform.position = targetPosition;

        desiredPosition = targetPosition;
        clampedPosition = targetPosition;

        velocityX = 0f;
        velocityY = 0f;
    }

    private void TickRoomLookReturn()
    {
        // 戻り中も、プレイヤー追従位置を取り直す。
        // プレイヤーは外部制御で止まっている想定だが、復帰位置ズレ対策として毎フレーム再計算する。
        roomLookReturnTargetPosition = ComputeFollowClampedPosition();

        roomLookReturnElapsed += Time.deltaTime;

        float duration = Mathf.Max(0f, roomLookReturnDuration);
        float linearT = duration <= 0f
            ? 1f
            : Mathf.Clamp01(roomLookReturnElapsed / duration);

        float easedT = EvaluateRoomTransitionEaseInOut(linearT);

        transform.position = Vector3.Lerp(
            roomLookReturnStartPosition,
            roomLookReturnTargetPosition,
            easedT);

        desiredPosition = roomLookReturnTargetPosition;
        clampedPosition = roomLookReturnTargetPosition;

        if (linearT >= 1f)
        {
            isRoomLookReturning = false;
            transform.position = roomLookReturnTargetPosition;
            ResetDashCameraState();

            velocityX = 0f;
            velocityY = 0f;
        }
    }

    private Vector3 ComputeFollowClampedPosition()
    {
        Transform effectiveTarget = GetEffectiveTargetAnchor();
        if (effectiveTarget == null)
        {
            return transform.position;
        }

        Vector3 followDesired = BuildFollowDesiredPosition(effectiveTarget);
        return GetClampedPosition(followDesired);
    }

    private Vector3 BuildFollowDesiredPosition(Transform effectiveTarget)
    {
        Vector3 baseDesiredPosition = effectiveTarget.position + cameraOffset;
        Vector3 focusOffset3D = hasActiveRoomFocusOffset
            ? new Vector3(-activeRoomFocusOffset.x, activeRoomFocusOffset.y, 0f)
            : Vector3.zero;
        // 横方向は X 軸、上下方向は Y 軸だけを Bounds Clamp 前に加算する。
        Vector3 dashLookAheadOffset = new Vector3(dashLookAheadCurrentX, verticalDashLookAheadCurrentY, 0f);
        return baseDesiredPosition + focusOffset3D + dashLookAheadOffset;
    }

    private void TickDashCamera()
    {
        bool isDashActive = playerFacade != null && playerFacade.IsDashActive;
        bool didDashStart = playerFacade != null
            && (playerFacade.JustDashStartedThisFrame || (!wasDashActivePreviousFrame && isDashActive));
        Vector2 dashDirection = playerFacade != null ? playerFacade.DashDirection : Vector2.zero;

        // 横・上下の条件を別々に判定する。
        // どちらにも入らない斜め方向ダッシュにはカメラ補正を適用しない。
        bool isHorizontalDash = Mathf.Abs(dashDirection.x) >= horizontalDashThreshold
            && Mathf.Abs(dashDirection.y) <= verticalDashIgnoreThreshold;
        bool isVerticalDash = !isHorizontalDash
            && Mathf.Abs(dashDirection.y) >= verticalDashThreshold
            && Mathf.Abs(dashDirection.x) <= horizontalDashIgnoreThreshold;

        TickHorizontalDashCamera(isDashActive, didDashStart, dashDirection, isHorizontalDash);
        TickVerticalDashCamera(isDashActive, didDashStart, dashDirection, isVerticalDash);
        wasDashActivePreviousFrame = isDashActive;
    }

    private void TickHorizontalDashCamera(bool isDashActive, bool didDashStart, Vector2 dashDirection, bool isHorizontalDash)
    {
        activeSmoothTimeXForDebug = EffectiveSmoothTimeX;

        if (!horizontalDashCameraEnabled || playerFacade == null)
        {
            ResetHorizontalDashCameraState();
            return;
        }

        if (didDashStart)
        {
            isHorizontalDashCameraActive = false;

            if (isHorizontalDash)
            {
                dashHorizontalDirectionSign = dashDirection.x >= 0f ? 1 : -1;
                dashStartLagTimer = Mathf.Max(0f, dashStartLagDuration);
                isHorizontalDashCameraActive = true;
            }
        }

        if (!isDashActive)
        {
            isHorizontalDashCameraActive = false;
        }

        if (isHorizontalDashCameraActive && dashStartLagTimer > 0f)
        {
            activeSmoothTimeXForDebug = Mathf.Max(0f, dashStartSmoothTimeX);
            dashStartLagTimer = Mathf.Max(0f, dashStartLagTimer - Time.deltaTime);
        }
        else if (isHorizontalDashCameraActive && isDashActive)
        {
            activeSmoothTimeXForDebug = Mathf.Max(0f, dashSmoothTimeX);
        }

        float dashLookAheadTargetX = isHorizontalDashCameraActive
            ? dashHorizontalDirectionSign * dashLookAheadX
            : 0f;
        dashLookAheadCurrentX = Mathf.SmoothDamp(
            current: dashLookAheadCurrentX,
            target: dashLookAheadTargetX,
            currentVelocity: ref dashLookAheadSmoothDampVelocityX,
            smoothTime: Mathf.Max(0f, dashLookAheadReturnTime));
    }

    private void TickVerticalDashCamera(bool isDashActive, bool didDashStart, Vector2 dashDirection, bool isVerticalDash)
    {
        activeSmoothTimeYForDebug = EffectiveSmoothTimeY;

        if (!verticalDashCameraEnabled || playerFacade == null)
        {
            ResetVerticalDashCameraState();
            return;
        }

        if (didDashStart)
        {
            isVerticalDashCameraActive = false;

            if (isVerticalDash)
            {
                verticalDashDirectionSign = dashDirection.y >= 0f ? 1 : -1;
                verticalDashStartLagTimer = Mathf.Max(0f, verticalDashStartLagDuration);
                isVerticalDashCameraActive = true;
            }
        }

        if (!isDashActive)
        {
            isVerticalDashCameraActive = false;
        }

        if (isVerticalDashCameraActive && verticalDashStartLagTimer > 0f)
        {
            activeSmoothTimeYForDebug = Mathf.Max(0f, verticalDashStartSmoothTimeY);
            verticalDashStartLagTimer = Mathf.Max(0f, verticalDashStartLagTimer - Time.deltaTime);
        }
        else if (isVerticalDashCameraActive && isDashActive)
        {
            activeSmoothTimeYForDebug = Mathf.Max(0f, verticalDashSmoothTimeY);
        }

        float dashLookAheadTargetY = isVerticalDashCameraActive
            ? verticalDashDirectionSign * verticalDashLookAheadY
            : 0f;
        verticalDashLookAheadCurrentY = Mathf.SmoothDamp(
            current: verticalDashLookAheadCurrentY,
            target: dashLookAheadTargetY,
            currentVelocity: ref verticalDashLookAheadSmoothDampVelocityY,
            smoothTime: Mathf.Max(0f, verticalDashLookAheadReturnTime));
    }

    private void ResetHorizontalDashCameraState()
    {
        dashStartLagTimer = 0f;
        dashHorizontalDirectionSign = 0;
        dashLookAheadCurrentX = 0f;
        dashLookAheadSmoothDampVelocityX = 0f;
        isHorizontalDashCameraActive = false;
        activeSmoothTimeXForDebug = EffectiveSmoothTimeX;
    }

    private void ResetVerticalDashCameraState()
    {
        verticalDashStartLagTimer = 0f;
        verticalDashDirectionSign = 0;
        verticalDashLookAheadCurrentY = 0f;
        verticalDashLookAheadSmoothDampVelocityY = 0f;
        isVerticalDashCameraActive = false;
        activeSmoothTimeYForDebug = EffectiveSmoothTimeY;
    }

    private void ResetDashCameraState()
    {
        ResetHorizontalDashCameraState();
        ResetVerticalDashCameraState();
        wasDashActivePreviousFrame = playerFacade != null && playerFacade.IsDashActive;
    }

    public void BeginRoomTransition(Room room)
    {
        // 部屋情報がない場合は遷移を開始できない。
        if (room == null)
        {
            Debug.LogWarning("PlayerCameraController: BeginRoomTransition に null が渡されました。", this);
            return;
        }

        ResetDashCameraState();

        // 現在位置を開始地点として保存する。
        roomTransitionStartPosition = transform.position;

        // 現在の反映済み設定から遷移目標位置を再計算して保存する。
        Transform effectiveTarget = GetEffectiveTargetAnchor();
        if (effectiveTarget != null)
        {
            Vector3 baseDesiredPosition = effectiveTarget.position + cameraOffset;
            if (hasActiveRoomFocusOffset)
            {
                Vector3 focusOffset3D = new Vector3(-activeRoomFocusOffset.x, -activeRoomFocusOffset.y, 0f);
                desiredPosition = baseDesiredPosition + focusOffset3D;
            }
            else
            {
                desiredPosition = baseDesiredPosition;
            }

            clampedPosition = GetClampedPosition(desiredPosition);
            roomTransitionTargetPosition = clampedPosition;
        }
        else
        {
            roomTransitionTargetPosition = transform.position;
        }

        // 遷移時間を部屋設定とデフォルトから確定する。
        activeRoomTransitionDuration = GetEffectiveRoomTransitionDuration(room);
        roomTransitionElapsed = 0f;

        // 通常追従側の SmoothDamp 速度を持ち越すと、
        // 遷移終了後に一瞬だけ変な慣性が乗って見えやすい。
        // そのため、遷移開始時点で内部速度をリセットする。
        velocityX = 0f;
        velocityY = 0f;

        if (activeRoomTransitionDuration <= 0f)
        {
            transform.position = roomTransitionTargetPosition;
            isRoomTransitionRunning = false;
            return;
        }

        isRoomTransitionRunning = true;
    }

    public void CancelRoomTransitionAndSnapToTarget()
    {
        ResetDashCameraState();

        // 遷移中フラグを落として目標位置へ即時スナップする。
        isRoomTransitionRunning = false;
        transform.position = roomTransitionTargetPosition;

        // 遷移キャンセル後に余計な慣性が残らないよう内部速度を初期化する。
        velocityX = 0f;
        velocityY = 0f;
    }

    private void EndRoomTransition()
    {
        // 遷移終了時は最終目標位置へ確定させてから通常追従へ戻す。
        isRoomTransitionRunning = false;
        transform.position = roomTransitionTargetPosition;
        ResetDashCameraState();

        // 通常追従へ戻る瞬間のガクつきを防ぐため、
        // SmoothDamp の内部速度をここでも明示的に初期化する。
        velocityX = 0f;
        velocityY = 0f;
    }

    // -----------------------------
    // camera 実行
    // -----------------------------

    private float GetEffectiveRoomTransitionDuration(Room room)
    {
        // 部屋側の上書きがあれば優先し、無ければデフォルト値を使う。
        float duration = defaultRoomTransitionDuration;
        if (room != null && room.HasRoomTransitionDurationOverride)
        {
            duration = room.RoomTransitionDuration;
        }

        return Mathf.Max(0f, duration);
    }

    // 0→1の進行率を、開始と終了がなめらかなEaseInOutに変換する。
    private float EvaluateRoomTransitionEaseInOut(float t)
    {
        t = Mathf.Clamp01(t);

        // Mathf.SmoothStep(0, 1, t) と同じ形。
        // 明示的に書くことで「何をしているか」を追いやすくする。
        return t * t * (3f - 2f * t);
    }

    private Vector3 GetClampedPosition(Vector3 desired)
    {
        // 現在使うべきワールド境界を取得する。
        Bounds bounds = EffectiveWorldBounds;

        // Orthographic カメラの画面半サイズを求める。
        float halfHeight = targetCamera.orthographicSize;
        float halfWidth = halfHeight * targetCamera.aspect;

        // カメラ中心が取りうる最小・最大位置を計算する。
        // 画面端が境界をはみ出さないよう、半画面サイズ分を内側に寄せる。
        float minX = bounds.min.x + halfWidth;
        float maxX = bounds.max.x - halfWidth;
        float minY = bounds.min.y + halfHeight;
        float maxY = bounds.max.y - halfHeight;

        // 境界が画面より狭い場合は Clamp 不能になるので、中心固定にする。
        float clampedX = (minX > maxX) ? bounds.center.x : Mathf.Clamp(desired.x, minX, maxX);
        float clampedY = (minY > maxY) ? bounds.center.y : Mathf.Clamp(desired.y, minY, maxY);

        // Z は desired 側の値をそのまま使う。
        return new Vector3(clampedX, clampedY, desired.z);
    }

    private Vector3 GetClampedPositionInBounds(Vector3 desired, Bounds bounds)
    {
        // Orthographic カメラの画面半サイズを求める。
        float halfHeight = targetCamera.orthographicSize;
        float halfWidth = halfHeight * targetCamera.aspect;

        // カメラ中心が取りうる最小・最大位置を計算する。
        // 画面端が境界をはみ出さないよう、半画面サイズ分を内側に寄せる。
        float minX = bounds.min.x + halfWidth;
        float maxX = bounds.max.x - halfWidth;
        float minY = bounds.min.y + halfHeight;
        float maxY = bounds.max.y - halfHeight;

        // 境界が画面より狭い場合は Clamp 不能になるので、中心固定にする。
        float clampedX = (minX > maxX) ? bounds.center.x : Mathf.Clamp(desired.x, minX, maxX);
        float clampedY = (minY > maxY) ? bounds.center.y : Mathf.Clamp(desired.y, minY, maxY);

        // Z は desired 側の値をそのまま使う。
        return new Vector3(clampedX, clampedY, desired.z);
    }

    private void ApplyOrthographicSize()
    {
        float targetSize = Mathf.Max(0.01f, EffectiveOrthographicSize);
        float smoothTime = Mathf.Max(0f, EffectiveOrthographicSizeSmoothTime);

        if (smoothTime <= 0f)
        {
            targetCamera.orthographicSize = targetSize;
            orthographicSizeVelocity = 0f;
            return;
        }

        targetCamera.orthographicSize = Mathf.SmoothDamp(
            current: targetCamera.orthographicSize,
            target: targetSize,
            currentVelocity: ref orthographicSizeVelocity,
            smoothTime: smoothTime);
    }

    // 現在カメラに映っているワールド座標範囲を返す。
    // SonarChargerEnemy の突進停止境界など、画面端基準の判定に使う。
    public Bounds GetCurrentViewBounds()
    {
        Camera cam = targetCamera != null ? targetCamera : GetComponent<Camera>();

        if (cam == null)
        {
            return new Bounds(transform.position, Vector3.zero);
        }

        float halfHeight = Mathf.Max(0.01f, EffectiveSize);
        float halfWidth = halfHeight * cam.aspect;

        Vector3 center = transform.position;

        return new Bounds(
            new Vector3(center.x, center.y, center.z),
            new Vector3(halfWidth * 2.0f, halfHeight * 2.0f, 1000.0f));
    }

    // -----------------------------
    // debug / gizmo
    // -----------------------------

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        // Gizmo 無効なら何も描かない。
        if (!drawDebugGizmos)
        {
            return;
        }

        // 理想位置(desiredPosition)を黄色で表示。
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(desiredPosition, 0.15f);

        // Clamp 後位置(clampedPosition)を緑で表示。
        Gizmos.color = Color.green;
        Gizmos.DrawSphere(clampedPosition, 0.18f);

        // 理想位置から Clamp 後位置までの差を線で表示。
        Gizmos.color = Color.white;
        Gizmos.DrawLine(desiredPosition, clampedPosition);

        // 通常追従アンカー位置をマゼンタで表示。
        if (targetAnchor != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawSphere(targetAnchor.position, 0.12f);
        }

        // 一時追従アンカー位置をシアンで表示。
        if (temporaryTargetAnchor != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(temporaryTargetAnchor.position, 0.12f);
        }
    }
#endif
}