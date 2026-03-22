using UnityEngine;

// 責務:
// - world の 2 点(back/front)を受け取り、腕節 prefab の見た目(向き/長さ/位置)を合わせる
// - StartSocket を back 側へ、EndSocket を front 側へなるべく一致させる
// - sorting layer / order と表示切り替えを提供する
//
// 非責務:
// - 節点追加や節点列管理
// - 移動本体、寿命管理、即死判定
// - controller 司令塔、IK、物理挙動
//
// 依存先:
// - startSocket / endSocket: 見た目合わせの基準点
// - spriteRoot: スケール変更対象
// - spriteRenderer: sorting 設定先
//
// 前提条件:
// - StartSocket と EndSocket が prefab 内で正しく配置されている
// - Apply(backWorld, frontWorld, ...) を外部から継続的に呼ぶ
// - planeMode に応じて XY または XZ 平面で見た目を解釈する
public sealed class ArmSegmentView : MonoBehaviour
{
    // 節見た目をどの平面で解釈するか。
    // XY は 2D 横スク向け、XZ は床面配置向けを想定する。
    public enum SegmentViewPlaneMode
    {
        XY,
        XZ,
    }

    // 見た目合わせ計算で 0 長さ扱いを避けるための最小値。
    private const float MinLengthEpsilon = 1e-4f;

    // =====================================================================
    // Inspector 設定値
    // =====================================================================

    [Header("参照: Sprite ルート")]
    [Tooltip("節見た目のスケール変更対象です。Apply 時の長さ合わせで localScale を変更します。未設定時は transform 自身を使います。差し替えると、どの階層に長さスケールを掛けるかが変わります。")]
    [SerializeField] private Transform spriteRoot;

    [Header("参照: Start ソケット")]
    [Tooltip("backWorld 側へ合わせる基準ソケットです。InitializeIfNeeded で初期位置を記録し、Apply 時の root 平行移動で基準点として使います。未設定時は子名 'StartSocket' から自動補完を試みます。")]
    [SerializeField] private Transform startSocket;

    [Header("参照: End ソケット")]
    [Tooltip("frontWorld 側へ伸ばす向きと基準長さの終点ソケットです。InitializeIfNeeded で restLength 算出に使います。未設定時は子名 'EndSocket' から自動補完を試みます。")]
    [SerializeField] private Transform endSocket;

    [Header("参照: SpriteRenderer")]
    [Tooltip("sorting layer / sorting order の適用先です。ApplySorting で使用します。未設定時は spriteRoot、または子階層から自動補完を試みます。")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("見た目: 平面モード")]
    [Tooltip("節見た目を XY 平面で扱うか XZ 平面で扱うかの設定です。ProjectWorldToPlane と BuildTargetAxisWorld に使います。切り替えると back/front の解釈平面が変わります。")]
    [SerializeField] private SegmentViewPlaneMode planeMode = SegmentViewPlaneMode.XY;

    [Header("見た目: Awake 自動初期化")]
    [Tooltip("有効にすると Awake 時に InitializeIfNeeded を呼びます。無効なら外部から明示的に初期化する前提になります。通常は有効のまま使います。")]
    [SerializeField] private bool autoInitializeOnAwake = true;

    [Header("見た目: X スケール許可")]
    [Tooltip("有効にすると、back/front の距離に応じて X スケールで長さを合わせます。無効にすると長さスケールを変えません。通常は腕の長さ調整に使います。")]
    [SerializeField] private bool allowScaleX = true;

    [Header("見た目: Y スケール許可")]
    [Tooltip("有効にすると、将来の幅方向スケール変更を許可する拡張口です。現状ロジックでは幅は維持しており、true にしても見た目挙動は変わりません。")]
    [SerializeField] private bool allowScaleY = false;

    [Header("描画順: 上書き Sorting Layer 名")]
    [Tooltip("Apply 側から渡された sortingLayerName より優先して使う Sorting Layer 名です。空文字なら外部指定をそのまま使います。固定したい Layer があるときに使います。")]
    [SerializeField] private string overrideSortingLayerName = string.Empty;

    [Header("デバッグ: Socket Gizmos 表示")]
    [Tooltip("有効にすると OnDrawGizmosSelected で StartSocket / EndSocket とその接続線を表示します。見た目基準点の確認用であり、ゲーム挙動そのものは変わりません。")]
    [SerializeField] private bool showSocketGizmos = false;

    // =====================================================================
    // 実行時状態
    // 確認専用の Runtime 値。調整用ではなく、初期化結果や socket 基準の観測に使う。
    // =====================================================================

    [Header("デバッグ(Runtime): 基準長さ")]
    [Tooltip("StartSocket と EndSocket の初期ローカル距離です。InitializeIfNeeded で算出し、Apply 時の長さスケール比率計算に使います。調整用ではなく、prefab 基準長さの観測用です。")]
    [SerializeField] private float restLength;

    [Header("デバッグ(Runtime): 初期ローカルスケール")]
    [Tooltip("spriteRoot または transform の初期 localScale です。Apply 時の伸縮で基準スケールとして使います。調整用ではなく、初期化結果確認用です。")]
    [SerializeField] private Vector3 initialLocalScale = Vector3.one;

    [Header("デバッグ(Runtime): 初期化済みフラグ")]
    [Tooltip("InitializeIfNeeded が有効に完了したかどうかです。StartSocket / EndSocket が揃い、基準長さが確定したとき true になります。調整用ではなく初期化状態確認用です。")]
    [SerializeField] private bool isInitialized;

    [Header("デバッグ(Runtime): Start 初期ローカル位置")]
    [Tooltip("InitializeIfNeeded 時点の StartSocket.localPosition です。基準姿勢確認用に保持します。調整用ではなく prefab 基準点の観測用です。")]
    [SerializeField] private Vector3 cachedStartLocalPosition;

    [Header("デバッグ(Runtime): End 初期ローカル位置")]
    [Tooltip("InitializeIfNeeded 時点の EndSocket.localPosition です。基準姿勢確認用に保持します。調整用ではなく prefab 基準点の観測用です。")]
    [SerializeField] private Vector3 cachedEndLocalPosition;

    // =====================================================================
    // 公開参照口
    // =====================================================================

    // prefab 基準長さの参照口。
    public float RestLength => restLength;

    // =====================================================================
    // 初期化 / 検証
    // =====================================================================

    // Inspector 追加直後や Reset 時に参照の自動補完を行う。
    private void Reset()
    {
        AutoAssignReferences();
    }

    // Inspector 変更時に参照の自動補完を行う。
    // 既存設定は壊さず、未設定参照だけを補う。
    private void OnValidate()
    {
        AutoAssignReferences();
    }

    // 必要なら起動時に初期化を完了させる。
    private void Awake()
    {
        if (autoInitializeOnAwake)
        {
            InitializeIfNeeded();
        }
    }

    // =====================================================================
    // 公開操作
    // =====================================================================

    // 基準長さと初期スケールを未初期化時だけ確定する。
    // StartSocket / EndSocket が揃っていない場合は初期化を保留する。
    public void InitializeIfNeeded()
    {
        if (isInitialized)
        {
            return;
        }

        AutoAssignReferences();

        if (startSocket == null || endSocket == null)
        {
            return;
        }

        cachedStartLocalPosition = startSocket.localPosition;
        cachedEndLocalPosition = endSocket.localPosition;

        restLength = Vector3.Distance(cachedStartLocalPosition, cachedEndLocalPosition);
        if (restLength <= MinLengthEpsilon)
        {
            restLength = 0f;
            return;
        }

        Transform scaleTarget = spriteRoot != null ? spriteRoot : transform;
        initialLocalScale = scaleTarget.localScale;
        isInitialized = true;
    }

    // 見た目適用の簡易入口。
    // sorting layer を外部指定しない場合は空文字で委譲する。
    public void Apply(Vector3 backWorld, Vector3 frontWorld, int sortingOrder)
    {
        Apply(backWorld, frontWorld, string.Empty, sortingOrder);
    }

    // world の 2 点に合わせて、向き・長さ・位置・描画順を更新する。
    // StartSocket を backWorld 側へ合わせることを優先し、長さは plane 上距離から求める。
    public void Apply(Vector3 backWorld, Vector3 frontWorld, string sortingLayerName, int sortingOrder)
    {
        InitializeIfNeeded();

        if (!isInitialized || startSocket == null || endSocket == null)
        {
            return;
        }

        if (!gameObject.activeSelf)
        {
            SetVisible(true);
        }

        Vector2 backPlane = ProjectWorldToPlane(backWorld);
        Vector2 frontPlane = ProjectWorldToPlane(frontWorld);
        Vector2 planeDelta = frontPlane - backPlane;
        float desiredLength = planeDelta.magnitude;

        if (desiredLength <= MinLengthEpsilon)
        {
            ApplySorting(sortingLayerName, sortingOrder);
            return;
        }

        Vector3 targetAxisWorld = BuildTargetAxisWorld(planeDelta / desiredLength);
        if (targetAxisWorld.sqrMagnitude <= 1e-8f)
        {
            ApplySorting(sortingLayerName, sortingOrder);
            return;
        }

        // 1. 向き合わせ
        transform.rotation = Quaternion.FromToRotation(Vector3.right, targetAxisWorld.normalized);

        // 2. 長さ合わせ
        Transform scaleTarget = spriteRoot != null ? spriteRoot : transform;
        Vector3 nextScale = initialLocalScale;

        if (allowScaleX)
        {
            float scaleRatio = desiredLength / Mathf.Max(restLength, MinLengthEpsilon);
            nextScale.x = initialLocalScale.x * scaleRatio;
        }

        if (allowScaleY)
        {
            // 今回は幅方向維持が基本。
            // 将来の幅変化拡張口としてフックだけ残す。
            nextScale.y = initialLocalScale.y;
        }

        scaleTarget.localScale = nextScale;

        // 3. StartSocket が backWorld に一致するよう root を平行移動
        Vector3 constrainedBackWorld = backWorld;
        switch (planeMode)
        {
            case SegmentViewPlaneMode.XY:
                constrainedBackWorld.z = backWorld.z;
                break;

            case SegmentViewPlaneMode.XZ:
                constrainedBackWorld.y = backWorld.y;
                break;
        }

        Vector3 offsetToBack = constrainedBackWorld - startSocket.position;
        transform.position += offsetToBack;

        ApplySorting(sortingLayerName, sortingOrder);
    }

    // GameObject 単位で表示 / 非表示を切り替える。
    public void SetVisible(bool visible)
    {
        gameObject.SetActive(visible);
    }

    // =====================================================================
    // 座標変換 / 軸解決
    // =====================================================================

    // world 座標を現在の表示平面へ射影する。
    private Vector2 ProjectWorldToPlane(Vector3 world)
    {
        switch (planeMode)
        {
            case SegmentViewPlaneMode.XZ:
                return new Vector2(world.x, world.z);

            case SegmentViewPlaneMode.XY:
            default:
                return new Vector2(world.x, world.y);
        }
    }

    // 平面方向ベクトルを world 方向ベクトルへ戻す。
    private Vector3 BuildTargetAxisWorld(Vector2 planeDirection)
    {
        switch (planeMode)
        {
            case SegmentViewPlaneMode.XZ:
                return new Vector3(planeDirection.x, 0f, planeDirection.y);

            case SegmentViewPlaneMode.XY:
            default:
                return new Vector3(planeDirection.x, planeDirection.y, 0f);
        }
    }

    // 現在平面上での 2 点間距離を返す。
    // 現状は Gizmos 内の参照用だが、平面距離取得の補助として残す。
    private float DistanceOnPlane(Vector3 a, Vector3 b)
    {
        Vector2 pa = ProjectWorldToPlane(a);
        Vector2 pb = ProjectWorldToPlane(b);
        return Vector2.Distance(pa, pb);
    }

    // =====================================================================
    // 描画順
    // =====================================================================

    // sorting layer / order を SpriteRenderer へ適用する。
    // overrideSortingLayerName が設定されていれば外部指定より優先する。
    private void ApplySorting(string sortingLayerName, int sortingOrder)
    {
        if (spriteRenderer == null)
        {
            return;
        }

        string effectiveLayerName = sortingLayerName;
        if (!string.IsNullOrEmpty(overrideSortingLayerName))
        {
            effectiveLayerName = overrideSortingLayerName;
        }

        if (!string.IsNullOrEmpty(effectiveLayerName))
        {
            spriteRenderer.sortingLayerName = effectiveLayerName;
        }

        spriteRenderer.sortingOrder = sortingOrder;
    }

    // =====================================================================
    // 参照補完
    // =====================================================================

    // 未設定参照を prefab 既定名から補完する。
    // 既存設定を壊さないため、未設定時だけ探索する。
    private void AutoAssignReferences()
    {
        if (spriteRoot == null)
        {
            Transform foundSpriteRoot = transform.Find("SpriteRoot");
            if (foundSpriteRoot != null)
            {
                spriteRoot = foundSpriteRoot;
            }
        }

        if (startSocket == null)
        {
            Transform foundStartSocket = transform.Find("StartSocket");
            if (foundStartSocket != null)
            {
                startSocket = foundStartSocket;
            }
        }

        if (endSocket == null)
        {
            Transform foundEndSocket = transform.Find("EndSocket");
            if (foundEndSocket != null)
            {
                endSocket = foundEndSocket;
            }
        }

        if (spriteRenderer == null)
        {
            if (spriteRoot != null)
            {
                spriteRenderer = spriteRoot.GetComponent<SpriteRenderer>();
            }

            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }
        }
    }

    // =====================================================================
    // Gizmos
    // =====================================================================

    // 選択時に Socket 基準点を可視化する。
    // showSocketGizmos が無効なら何も描かない。
    private void OnDrawGizmosSelected()
    {
        if (!showSocketGizmos || startSocket == null || endSocket == null)
        {
            return;
        }

        Gizmos.color = Color.green;
        Gizmos.DrawSphere(startSocket.position, 0.04f);

        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(endSocket.position, 0.04f);

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(startSocket.position, endSocket.position);

        // 参照用:
        // 現在平面での距離が取得できることを明示する。
        _ = DistanceOnPlane(startSocket.position, endSocket.position);
    }
}