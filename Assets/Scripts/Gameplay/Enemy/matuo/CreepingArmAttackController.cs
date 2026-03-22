using System.Collections.Generic;
using UnityEngine;

// 攻撃ロジック平面の選択。
// XY は 2D 横スク向け、XZ は 3D 空間上の床面ロジック向けを想定する。
public enum AttackPlaneMode
{
    XY,
    XZ,
}

// 責務:
// - Creeping Arm 攻撃の開始 / 停止を管理する
// - ワールド座標と攻撃ロジック平面座標の相互変換を行う
// - 最小限のランタイム状態を保持し、将来の専用クラス接続口を提供する
//
// 非責務:
// - 腕 / 手の平の移動計算本体は担当しない
// - 節点列生成やセグメント見た目更新の本体は担当しない
// - 当たり判定やダメージ適用本体は担当しない
//
// 依存先:
// - optionalSpawnAnchor / headRoot / palmSocket: 開始地点・見た目位置の参照元
// - segmentPoolRoot / segmentPrefab: 将来のセグメント生成接続口
// - SpriteRenderer(headRoot 配下): 仮の描画順設定に使用
//
// 前提条件:
// - StartAttack / StopAttack が外部から適切なタイミングで呼ばれる
// - attackPlaneMode に応じて XY または XZ 平面でロジック座標を扱う
// - TODO 部分は将来の専用クラスへ委譲される
public sealed class CreepingArmAttackController : MonoBehaviour
{
    // =====================================================================
    // Inspector 設定値
    // =====================================================================

    [Header("参照: 開始地点アンカー")]
    [Tooltip("攻撃開始時の初期ロジック点として使う任意アンカーです。useSpawnAnchorAsInitialPoint が有効なときに StartAttack で参照します。未設定なら palmSocket、さらに無ければ自分自身の transform.position を使います。")]
    [SerializeField] private Transform optionalSpawnAnchor;

    [Header("参照: Head ルート")]
    [Tooltip("攻撃ヘッドの見た目ルートです。StartAttack で初期位置反映と仮の描画順設定に使います。未設定だと head の初期ワールド位置更新や描画順仮設定は行われません。")]
    [SerializeField] private Transform headRoot;

    [Header("参照: Palm ソケット")]
    [Tooltip("現在の手の平位置の参照元です。StartAttack の初期地点候補と TickAttack 中の currentPalmLogicPoint 更新に使います。未設定だと現在手の平位置の追従更新は行われません。")]
    [SerializeField] private Transform palmSocket;

    [Header("参照: セグメント配置ルート")]
    [Tooltip("将来セグメント GameObject を生成・配置する親 Transform です。StartAttack 時に未設定なら自分自身へ補完します。セグメント生成実装が接続されたときの配置先になります。")]
    [SerializeField] private Transform segmentPoolRoot;

    [Header("参照: セグメント Prefab")]
    [Tooltip("将来の節表示に使うセグメント prefab です。現状の scaffold では実生成に未使用ですが、ログや接続前提の確認に使います。未設定でも攻撃開始自体は継続します。")]
    [SerializeField] private GameObject segmentPrefab;

    [Header("チェーン設定: セグメント長")]
    [Tooltip("1 節あたりの基準長さです。StartAttack で下限 0.01 に丸めて保持し、将来の節点列更新やセグメント生成の基準として使います。大きくすると粗く長い節、小さくすると細かい節構成になります。")]
    [SerializeField] private float segmentLength = 0.6f;

    [Header("チェーン設定: 最大セグメント数")]
    [Tooltip("生成・管理するセグメント数の上限です。StartAttack で最低 1 に丸めます。大きくすると長い腕を表現しやすくなり、小さいと短い腕になります。")]
    [SerializeField] private int maxSegmentCount = 12;

    [Header("チェーン設定: 末尾から現在手の平へ補助線表示")]
    [Tooltip("Gizmos 表示時に最後の節点から currentPalmLogicPoint まで補助線を引くかどうかです。デバッグ可視化専用で、ゲーム挙動そのものは変わりません。")]
    [SerializeField] private bool drawTailToCurrentPalm = true;

    [Header("平面設定: 攻撃平面モード")]
    [Tooltip("攻撃ロジックを XY 平面で扱うか XZ 平面で扱うかを決めます。ProjectWorldToLogicPlane と LiftLogicPointToWorld の座標変換に使われ、選択によって見かけ上の攻撃平面が変わります。")]
    [SerializeField] private AttackPlaneMode attackPlaneMode = AttackPlaneMode.XY;

    [Header("平面設定: 固定奥行き")]
    [Tooltip("ロジック平面からワールド座標へ戻すときに固定する奥行き値です。XY モードでは Z、XZ モードでは Y に入ります。値を変えると見た目の配置面が変わります。")]
    [SerializeField] private float fixedDepth = 0f;

    [Header("描画順: Sorting Layer 名")]
    [Tooltip("headRoot 配下 SpriteRenderer の仮描画順設定に使う Sorting Layer 名です。ApplyHeadSortingPlaceholder で使用します。将来セグメント描画順更新を実装したときの基準にもなります。")]
    [SerializeField] private string sortingLayerName = "EnemyAttack";

    [Header("描画順: Head の order")]
    [Tooltip("headRoot 配下 SpriteRenderer に設定する基準 sortingOrder です。値を大きくすると他 Sprite より手前に描かれやすくなります。現状は head の仮設定にのみ使います。")]
    [SerializeField] private int headSortingOrder = 200;

    [Header("描画順: 最新セグメントの order")]
    [Tooltip("将来、最新セグメントに割り当てる想定の sortingOrder 基準値です。現状はログ出力にのみ使い、見た目更新本体は未実装です。")]
    [SerializeField] private int newestSegmentSortingOrder = 199;

    [Header("描画順: セグメントごとの差分")]
    [Tooltip("将来、セグメントごとに sortingOrder をずらすときの差分値です。現状はログ出力にのみ使います。値を大きくすると前後関係の差が広がります。")]
    [SerializeField] private int sortingStepPerSegment = 1;

    [Header("実行設定: OnEnable で自動開始")]
    [Tooltip("有効にすると、このコンポーネントが有効化されたとき自動で StartAttack を呼びます。無効なら外部から明示的に StartAttack を呼ぶ運用になります。")]
    [SerializeField] private bool autoStartOnEnable = false;

    [Header("実行設定: 攻撃継続時間(秒)")]
    [Tooltip("攻撃を自動停止するまでの時間です。0 以下なら自動停止せず、TickAttack で attackTimer がこの値以上になったときだけ StopAttack します。値を大きくすると長く継続します。")]
    [SerializeField] private float attackDuration = 0f;

    [Header("実行設定: 開始点に SpawnAnchor を使う")]
    [Tooltip("有効にすると、optionalSpawnAnchor がある場合に StartAttack の初期地点として優先使用します。無効なら palmSocket、さらに無ければ transform.position が使われます。")]
    [SerializeField] private bool useSpawnAnchorAsInitialPoint = true;

    [Header("デバッグ: Gizmos 表示")]
    [Tooltip("有効にすると、初期点・手の平位置・節点列の Gizmos を表示します。ロジック確認用であり、ゲーム挙動そのものは変わりません。")]
    [SerializeField] private bool showGizmos = true;

    [Header("デバッグ: セグメント数ログ表示")]
    [Tooltip("有効にすると、TickAttack 中に debugChainPoints 数や runtimeSegmentInstances 数をログ出力します。観測用であり、調整値ではありません。")]
    [SerializeField] private bool logSegmentCount = false;

    // =====================================================================
    // 実行時状態
    // 確認専用の Runtime 値。調整用ではなく、現在の攻撃進行や内部状態観測に使う。
    // =====================================================================

    [Header("デバッグ(Runtime): 攻撃中フラグ")]
    [Tooltip("現在攻撃シーケンスが進行中かどうかの確認専用フラグです。StartAttack で true、StopAttack で false になります。調整用ではなく状態観測用です。")]
    [SerializeField] private bool isAttacking;

    [Header("デバッグ(Runtime): 攻撃経過時間")]
    [Tooltip("攻撃開始からの経過時間です。TickAttack で deltaTime 加算され、自動停止判定に使われます。調整用ではなく進行確認用です。")]
    [SerializeField] private float attackTimer;

    [Header("デバッグ(Runtime): 初期ロジック点")]
    [Tooltip("攻撃開始時に確定した初期ロジック平面座標です。StartAttack で設定され、Gizmos 描画や head 初期位置反映に使います。調整用ではなく開始点確認用です。")]
    [SerializeField] private Vector2 initialLogicPoint;

    [Header("デバッグ(Runtime): 現在手の平ロジック点")]
    [Tooltip("現在の palmSocket を攻撃平面へ射影したロジック座標です。TickAttack で更新され、Gizmos と将来の節点更新接続口に使います。調整用ではなく観測用です。")]
    [SerializeField] private Vector2 currentPalmLogicPoint;

    [Header("デバッグ(Runtime): 節点列")]
    [Tooltip("現在のロジック節点列を確認するための一覧です。現状は StartAttack 時に初期点のみ入り、将来の ArmChainModel 接続先になります。調整用ではなく可視化用です。")]
    [SerializeField] private List<Vector2> debugChainPoints = new List<Vector2>();

    [Header("デバッグ(Runtime): セグメント実体一覧")]
    [Tooltip("実行時に生成したセグメント GameObject 一覧です。StopAttack や ClearRuntimeState で破棄対象になります。現状は未生成のため空が基本で、将来の表示接続確認用です。")]
    [SerializeField] private List<GameObject> runtimeSegmentInstances = new List<GameObject>();

    // =====================================================================
    // ライフサイクル
    // =====================================================================

    // 有効化時に必要なら自動で攻撃を開始する。
    private void OnEnable()
    {
        if (autoStartOnEnable)
        {
            StartAttack();
        }
    }

    // 毎フレーム、攻撃進行中ならランタイム更新を進める。
    private void Update()
    {
        if (!isAttacking)
        {
            return;
        }

        TickAttack(Time.deltaTime);
    }

    // =====================================================================
    // 公開操作
    // =====================================================================

    // 攻撃を開始する。
    // 初期地点の確定、ロジック平面座標初期化、head の初期位置反映、ランタイム状態のリセットを行う。
    public void StartAttack()
    {
        segmentLength = Mathf.Max(0.01f, segmentLength);
        maxSegmentCount = Mathf.Max(1, maxSegmentCount);

        Vector3 initialWorldPosition;
        if (useSpawnAnchorAsInitialPoint && optionalSpawnAnchor != null)
        {
            initialWorldPosition = optionalSpawnAnchor.position;
        }
        else if (palmSocket != null)
        {
            initialWorldPosition = palmSocket.position;
        }
        else
        {
            initialWorldPosition = transform.position;
        }

        initialLogicPoint = ProjectWorldToLogicPlane(initialWorldPosition);
        currentPalmLogicPoint = initialLogicPoint;

        if (headRoot != null)
        {
            headRoot.position = LiftLogicPointToWorld(initialLogicPoint);
            ApplyHeadSortingPlaceholder();
        }

        debugChainPoints.Clear();
        debugChainPoints.Add(initialLogicPoint);

        attackTimer = 0f;
        isAttacking = true;

        if (segmentPoolRoot == null)
        {
            segmentPoolRoot = transform;
        }

        if (logSegmentCount && segmentPrefab == null)
        {
            Debug.LogWarning(
                "[CreepingArmAttackController] segmentPrefab is not assigned yet. Scaffold mode continues without segment visuals.",
                this
            );
        }

        // NOTE:
        // runtimeSegment は Start 時点では生成しない。
        // 将来 ArmSegmentView 接続時に必要な初期表示をここへ入れる。
    }

    // 攻撃を停止する。
    // 進行フラグを下ろし、実行時生成物を破棄する。
    public void StopAttack()
    {
        isAttacking = false;
        ClearRuntimeState();
    }

    // 攻撃状態を完全リセットする。
    // StopAttack 後に、タイマーとロジック点と節点列も初期値へ戻す。
    public void ResetAttack()
    {
        StopAttack();

        attackTimer = 0f;
        initialLogicPoint = Vector2.zero;
        currentPalmLogicPoint = Vector2.zero;
        debugChainPoints.Clear();
    }

    // =====================================================================
    // 毎フレーム更新本体
    // =====================================================================

    // 攻撃進行中のランタイム状態を更新する。
    // 現状はタイマー更新、現在手の平位置更新、自動停止判定、デバッグログ出力だけを担当する。
    private void TickAttack(float deltaTime)
    {
        attackTimer += deltaTime;

        if (palmSocket != null)
        {
            currentPalmLogicPoint = ProjectWorldToLogicPlane(palmSocket.position);
        }

        // TODO:
        // ここで HandHeadMover を呼び、head / palm の移動目標を更新する。
        // ここで ArmChainModel を呼び、2D ロジック節点列を更新する。
        // ここで ArmSegmentView を呼び、節点列を見た目へ反映する。
        // ArmLethalHitbox などの当たり判定制御も専用コンポーネントへ委譲する。
        // newestSegmentSortingOrder / sortingStepPerSegment を使った描画順更新を実装する。

        if (attackDuration > 0f && attackTimer >= attackDuration)
        {
            StopAttack();
        }

        if (logSegmentCount)
        {
            Debug.Log(
                $"[CreepingArmAttackController] debugChainPoints={debugChainPoints.Count}, runtimeSegments={runtimeSegmentInstances.Count}, newestSegmentSortingOrder={newestSegmentSortingOrder}, sortingStepPerSegment={sortingStepPerSegment}",
                this
            );
        }
    }

    // =====================================================================
    // 座標変換
    // =====================================================================

    // ワールド座標を攻撃ロジック平面座標へ射影する。
    // XY モードなら (x, y)、XZ モードなら (x, z) を使う。
    private Vector2 ProjectWorldToLogicPlane(Vector3 world)
    {
        switch (attackPlaneMode)
        {
            case AttackPlaneMode.XY:
                return new Vector2(world.x, world.y);

            case AttackPlaneMode.XZ:
                return new Vector2(world.x, world.z);

            default:
                return new Vector2(world.x, world.y);
        }
    }

    // ロジック平面座標をワールド座標へ戻す。
    // fixedDepth を、XY では Z、XZ では Y として使う。
    private Vector3 LiftLogicPointToWorld(Vector2 logicPoint)
    {
        switch (attackPlaneMode)
        {
            case AttackPlaneMode.XY:
                return new Vector3(logicPoint.x, logicPoint.y, fixedDepth);

            case AttackPlaneMode.XZ:
                return new Vector3(logicPoint.x, fixedDepth, logicPoint.y);

            default:
                return new Vector3(logicPoint.x, logicPoint.y, fixedDepth);
        }
    }

    // =====================================================================
    // 実行時生成物整理
    // =====================================================================

    // 実行時に生成したセグメント実体を破棄する。
    // pooling 未導入の現状では Destroy を使い、一覧も空に戻す。
    private void ClearRuntimeState()
    {
        for (int i = 0; i < runtimeSegmentInstances.Count; i++)
        {
            GameObject instance = runtimeSegmentInstances[i];
            if (instance != null)
            {
                Destroy(instance);
            }
        }

        runtimeSegmentInstances.Clear();

        // TODO:
        // pooling を導入する場合は Destroy ではなく inactive 化へ切り替える。
    }

    // =====================================================================
    // 仮描画順設定
    // =====================================================================

    // headRoot 配下の SpriteRenderer へ仮の描画順を設定する。
    // 現状は head のみ対象で、将来のセグメント描画順実装前の最低限 placeholder とする。
    private void ApplyHeadSortingPlaceholder()
    {
        if (headRoot == null)
        {
            return;
        }

        SpriteRenderer headRenderer = headRoot.GetComponentInChildren<SpriteRenderer>();
        if (headRenderer == null)
        {
            return;
        }

        headRenderer.sortingLayerName = sortingLayerName;
        headRenderer.sortingOrder = headSortingOrder;
    }

    // =====================================================================
    // Gizmos
    // =====================================================================

    // 選択時に初期点、現在手の平位置、節点列を可視化する。
    // showGizmos が無効なら何も描かない。
    private void OnDrawGizmosSelected()
    {
        if (!showGizmos)
        {
            return;
        }

        Vector3 initialWorld = LiftLogicPointToWorld(initialLogicPoint);
        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(initialWorld, 0.08f);

        if (palmSocket != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(palmSocket.position, 0.08f);
            Gizmos.DrawLine(initialWorld, palmSocket.position);
        }

        if (debugChainPoints == null || debugChainPoints.Count == 0)
        {
            return;
        }

        Gizmos.color = Color.green;
        Vector3 prev = LiftLogicPointToWorld(debugChainPoints[0]);
        for (int i = 1; i < debugChainPoints.Count; i++)
        {
            Vector3 next = LiftLogicPointToWorld(debugChainPoints[i]);
            Gizmos.DrawLine(prev, next);
            Gizmos.DrawSphere(next, 0.05f);
            prev = next;
        }

        if (drawTailToCurrentPalm)
        {
            Vector3 palmWorld = LiftLogicPointToWorld(currentPalmLogicPoint);
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(prev, palmWorld);
            Gizmos.DrawSphere(palmWorld, 0.05f);
        }
    }
}