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

    [Header("参照: Head 移動コンポーネント")]
    [Tooltip("手先ヘッドの移動本体です。TickAttack で TickMovement(deltaTime) を呼びます。未設定でも controller 自体は動作継続し、palmSocket 位置だけでチェーン更新します。")]
    [SerializeField] private HandHeadMover headMover;

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

    [Header("Scale")]
    [Tooltip("攻撃全体のスケール係数です。最低限、ワープ距離(stepLength)へ反映されます。")]
    [SerializeField] private float attackScale = 1f;

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

    [Header("デバッグ(Runtime): セグメントView一覧")]
    [Tooltip("実行時に生成 / 再利用している ArmSegmentView 一覧です。必要本数不足時に追加生成し、過剰分は非表示化して再利用します。")]
    [SerializeField] private List<ArmSegmentView> runtimeSegmentViews = new List<ArmSegmentView>();

    [Header("デバッグ(Runtime): 実働セグメント長")]
    [Tooltip("StartAttack で解決された実働セグメント長です。head のワープ距離と chain の基準長へ同じ値を適用します。")]
    [SerializeField] private float effectiveSegmentLength = 0.1f;

    // 腕の確定節点列ロジックモデル。
    private ArmChainModel chainModel = new ArmChainModel();
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
        attackScale = Mathf.Max(0.01f, attackScale);

        Vector3 initialWorldPosition = ResolveInitialWorldPosition();

        initialLogicPoint = ProjectWorldToLogicPlane(initialWorldPosition);
        currentPalmLogicPoint = initialLogicPoint;

        if (headRoot != null)
        {
            headRoot.position = initialWorldPosition;
        }


        SyncMoverAndSegmentPlaneModes();
        effectiveSegmentLength = ResolveEffectiveSegmentLength();

        if (headMover != null)
        {
            headMover.SnapToWorldPosition(initialWorldPosition);
            headMover.SetStepLength(effectiveSegmentLength);
        }

        ApplyHeadSortingPlaceholder();

        chainModel.Reset(initialLogicPoint, effectiveSegmentLength, maxSegmentCount);

        debugChainPoints.Clear();
        SyncDebugChainPointsFromModel();

        attackTimer = 0f;
        isAttacking = true;

        if (segmentPoolRoot == null)
        {
            segmentPoolRoot = transform;
        }

        HideAllSegmentViews();

        if (logSegmentCount)
        {
            Debug.Log(
                $"[CreepingArmAttackController] StartAttack effectiveSegmentLength={effectiveSegmentLength:F4} (attackScale={attackScale:F4})",
                this
            );
        }
    }


    // 攻撃を停止する。
    // 進行フラグを下ろし、実行時生成物を破棄する。
    public void StopAttack()
    {
        isAttacking = false;
        attackTimer = 0f;

        if (headMover != null)
        {
            headMover.ResetMovementState();
        }

        chainModel.Clear();
        debugChainPoints.Clear();
        HideAllSegmentViews();
    }

    // 攻撃状態を完全リセットする。
    // StopAttack 後に、タイマーとロジック点と節点列も初期値へ戻す。
    public void ResetAttack()
    {
        StopAttack();

        initialLogicPoint = Vector2.zero;
        currentPalmLogicPoint = Vector2.zero;
    }

    // =====================================================================
    // 毎フレーム更新本体
    // =====================================================================

    // 攻撃進行中のランタイム状態を更新する。
    // 現状はタイマー更新、現在手の平位置更新、自動停止判定、デバッグログ出力だけを担当する。
    private void TickAttack(float deltaTime)
    {
        attackTimer += deltaTime;

        if (headMover != null)
        {
            headMover.TickMovement(deltaTime);
        }

        Vector3 currentPalmWorld = GetCurrentPalmWorldOnAttackPlane();
        currentPalmLogicPoint = ProjectWorldToLogicPlane(currentPalmWorld);

        if (!chainModel.IsInitialized)
        {
            chainModel.Reset(initialLogicPoint, Mathf.Max(0.1f, effectiveSegmentLength), maxSegmentCount);
        }

        bool warpedThisTick = headMover != null && headMover.WarpedThisTick;
        if (warpedThisTick)
        {
            chainModel.PushWarpPoint(currentPalmLogicPoint);
        }

        SyncDebugChainPointsFromModel();

        IReadOnlyList<Vector2> chainPoints = chainModel.ChainPoints;
        int requiredCount = Mathf.Max(0, chainPoints.Count - 1);
        EnsureSegmentViewCount(requiredCount);
        UpdateSegmentsFromChainPoints(chainPoints, currentPalmLogicPoint);


        ApplyHeadSortingPlaceholder();

        if (attackDuration > 0f && attackTimer >= attackDuration)
        {
            StopAttack();
        }

        if (logSegmentCount && warpedThisTick)
        {
            Debug.Log(
                $"[CreepingArmAttackController] chainPoints={debugChainPoints.Count}, activeSegments={requiredCount}, pooledViews={runtimeSegmentViews.Count}, newestSegmentSortingOrder={newestSegmentSortingOrder}, sortingStepPerSegment={sortingStepPerSegment}",
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

    // 必要本数に満たない ArmSegmentView を不足分だけ生成する。
    // 既存分は再利用し、過剰分は非表示化して維持する。
    private void EnsureSegmentViewCount(int requiredCount)
    {
        if (requiredCount <= 0)
        {
            HideAllSegmentViews();
            return;
        }

        if (segmentPrefab == null)
        {
            return;
        }

        if (segmentPoolRoot == null)
        {
            segmentPoolRoot = transform;
        }

        while (runtimeSegmentViews.Count < requiredCount)
        {
            GameObject instance = Instantiate(segmentPrefab, segmentPoolRoot);
            ArmSegmentView view = instance.GetComponent<ArmSegmentView>();
            if (view == null)
            {
                view = instance.GetComponentInChildren<ArmSegmentView>();
            }

            if (view == null)
            {
                Debug.LogWarning("[CreepingArmAttackController] segmentPrefab has no ArmSegmentView.", this);
                Destroy(instance);
                break;
            }

            view.SetVisible(false);
            view.SetPlaneMode(ConvertSegmentViewPlaneMode());
            runtimeSegmentViews.Add(view);
        }
    }

    // chainPoints を使って segment view の見た目と描画順を更新する。
    // chainPoints を使って segment view の見た目と描画順を更新する。
    // 最新節のみ、back を live な palm に差し替える。
    private void UpdateSegmentsFromChainPoints(IReadOnlyList<Vector2> chainPoints, Vector2 currentPalmLogicPointOnPlane)
    {
        int activeCount = Mathf.Max(0, chainPoints.Count - 1);
        int newestSegmentIndex = activeCount - 1;
        for (int i = 0; i < runtimeSegmentViews.Count; i++)
        {
            ArmSegmentView view = runtimeSegmentViews[i];
            if (view == null)
            {
                continue;
            }

            if (i >= activeCount)
            {
                view.SetVisible(false);
                continue;
            }

            Vector2 startLogic;
            Vector2 endLogic;
            if (i == newestSegmentIndex && chainPoints.Count >= 2)
            {
                // 最新節は live palm -> ひとつ前の履歴点で描画する。␊
                startLogic = currentPalmLogicPointOnPlane;
                endLogic = chainPoints[chainPoints.Count - 2];
            }
            else
            {
                // 古い節は常に「新しい側 -> 古い側」の順で描画する。
                startLogic = chainPoints[i + 1];
                endLogic = chainPoints[i];
            }

            Vector3 startWorld = LiftLogicPointToWorld(startLogic);
            Vector3 endWorld = LiftLogicPointToWorld(endLogic);

            int sortingOrder = newestSegmentSortingOrder - ((activeCount - 1 - i) * sortingStepPerSegment);
            view.Apply(startWorld, endWorld, sortingLayerName, sortingOrder);
        }
    }

    // 全セグメント view を非表示化する。
    private void HideAllSegmentViews()
    {
        for (int i = 0; i < runtimeSegmentViews.Count; i++)
        {
            if (runtimeSegmentViews[i] != null)
            {
                runtimeSegmentViews[i].SetVisible(false);
            }
        }
    }

    // chainModel の点列を debug 表示配列へ同期する。
    private void SyncDebugChainPointsFromModel()
    {
        debugChainPoints.Clear();
        if (!chainModel.IsInitialized)
        {
            return;
        }

        IReadOnlyList<Vector2> chainPoints = chainModel.ChainPoints;
        for (int i = 0; i < chainPoints.Count; i++)
        {
            debugChainPoints.Add(chainPoints[i]);
        }
    }

    // AttackPlaneMode を SegmentView 用 plane mode に変換する。
    private ArmSegmentView.SegmentViewPlaneMode ConvertSegmentViewPlaneMode()
    {
        return attackPlaneMode == AttackPlaneMode.XZ
            ? ArmSegmentView.SegmentViewPlaneMode.XZ
            : ArmSegmentView.SegmentViewPlaneMode.XY;
    }

    // controller が source of truth になるよう、mover / view の平面モードを揃える。
    private void SyncMoverAndSegmentPlaneModes()
    {
        if (headMover != null)
        {
            MovementPlaneMode movementMode = attackPlaneMode == AttackPlaneMode.XZ
                ? MovementPlaneMode.XZ
                : MovementPlaneMode.XY;
            headMover.SetMovementPlaneMode(movementMode);
        }

        ArmSegmentView.SegmentViewPlaneMode viewMode = ConvertSegmentViewPlaneMode();
        for (int i = 0; i < runtimeSegmentViews.Count; i++)
        {
            if (runtimeSegmentViews[i] != null)
            {
                runtimeSegmentViews[i].SetPlaneMode(viewMode);
            }
        }
    }

    // 実働セグメント長を解決する。
    // 優先順位:
    // 1. segmentPrefab の ArmSegmentView.RestLength * attackScale
    // 2. Inspector segmentLength * attackScale
    // 3. 最終 fallback 最小安全値
    private float ResolveEffectiveSegmentLength()
    {
        float safeAttackScale = Mathf.Max(0.01f, attackScale);
        ArmSegmentView prefabView = TryGetSegmentPrefabView();
        if (prefabView != null)
        {
            prefabView.InitializeIfNeeded();
            float restLength = prefabView.RestLength;
            if (restLength > 0.0001f)
            {
                float resolved = restLength * safeAttackScale;
                if (resolved > 0.0001f)
                {
                    return resolved;
                }
            }
        }

        float legacyScaledLength = segmentLength * safeAttackScale;
        if (legacyScaledLength > 0.0001f)
        {
            return legacyScaledLength;
        }

        return 0.1f;
    }

    // segmentPrefab から ArmSegmentView を取得する。
    private ArmSegmentView TryGetSegmentPrefabView()
    {
        if (segmentPrefab == null)
        {
            return null;
        }

        ArmSegmentView view = segmentPrefab.GetComponent<ArmSegmentView>();
        if (view != null)
        {
            return view;
        }

        return segmentPrefab.GetComponentInChildren<ArmSegmentView>(true);
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

        int requiredFrontOrder = newestSegmentSortingOrder + Mathf.Max(1, Mathf.Abs(sortingStepPerSegment));
        int resolvedHeadSortingOrder = Mathf.Max(headSortingOrder, requiredFrontOrder);

        SpriteRenderer[] headRenderers = headRoot.GetComponentsInChildren<SpriteRenderer>(true);
        if (headRenderers == null || headRenderers.Length == 0)
        {
            return;
        }

        for (int i = 0; i < headRenderers.Length; i++)
        {
            if (headRenderers[i] == null)
            {
                continue;
            }

            headRenderers[i].sortingLayerName = sortingLayerName;
            headRenderers[i].sortingOrder = resolvedHeadSortingOrder;
        }
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

        Vector3 currentPalmWorld = LiftLogicPointToWorld(currentPalmLogicPoint);
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(currentPalmWorld, 0.08f);
        Gizmos.DrawLine(initialWorld, currentPalmWorld);

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

    }

    // StartAttack 時に使う初期ワールド位置を解決する。
    // 参照優先順位:
    // 1. useSpawnAnchorAsInitialPoint && optionalSpawnAnchor
    // 2. palmSocket
    // 3. headRoot
    // 4. self(transform)
    private Vector3 ResolveInitialWorldPosition()
    {
        if (useSpawnAnchorAsInitialPoint && optionalSpawnAnchor != null)
        {
            return optionalSpawnAnchor.position;
        }

        if (palmSocket != null)
        {
            return palmSocket.position;
        }

        if (headRoot != null)
        {
            return headRoot.position;
        }

        return transform.position;
    }

    // TickAttack 時に参照する現在の手先ワールド位置を解決する。
    // palmSocket 優先、次に headRoot、最後に self(transform)。
    private Vector3 ResolveCurrentPalmWorldPosition()
    {
        if (palmSocket != null)
        {
            return palmSocket.position;
        }

        if (headRoot != null)
        {
            return headRoot.position;
        }


        return transform.position;
    }

    // 現在の palm ワールド位置を、controller の攻撃平面定義へ揃えた座標として返す。
    private Vector3 GetCurrentPalmWorldOnAttackPlane()
    {
        Vector3 rawPalmWorld = ResolveCurrentPalmWorldPosition();
        Vector2 palmLogic = ProjectWorldToLogicPlane(rawPalmWorld);
        return LiftLogicPointToWorld(palmLogic);
    }
}