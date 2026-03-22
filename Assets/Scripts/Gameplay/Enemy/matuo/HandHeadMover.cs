using UnityEngine;

// 移動ロジックをどの平面で扱うかを表す。
// XY は 2D 横スク向け、XZ は 3D 空間上の床面移動向けを想定する。
public enum MovementPlaneMode
{
    XY,
    XZ,
}

// 責務:
// - target に向かう移動方向を平面上で計算する
// - headRoot を対象平面上で一定距離ワープさせる
// - ワープ間の停止時間を管理する
// - target 方向への向き更新を行う
//
// 非責務:
// - target の選定ロジックは担当しない
// - 攻撃開始 / 停止の上位判断は担当しない
// - 見た目アニメーションや当たり判定は担当しない
//
// 依存先:
// - target: 追従先の Transform
// - headRoot: 実際に移動させる Transform
// - Time.deltaTime / Time.time: TickMovement と停止中揺れに使用
//
// 前提条件:
// - 外部から TickMovement(deltaTime) が継続的に呼ばれる
// - movementPlaneMode に応じて XY または XZ 平面上で移動を解釈する
// - headRoot 未設定時は自分自身の transform を移動対象として使う
public sealed class HandHeadMover : MonoBehaviour
{
    // =====================================================================
    // Inspector 設定値
    // =====================================================================

    [Header("参照: 移動ターゲット")]
    [Tooltip("移動先として追従する Transform です。TickMovement と TryUpdateDirection で目標位置参照に使います。未設定だと移動方向を解決できないため、この mover は停止します。")]
    [SerializeField] private Transform target;

    [Header("参照: Head ルート")]
    [Tooltip("実際に移動させる Transform です。未設定時はこのコンポーネント自身の transform を使います。headRoot を変えると、移動させる見た目や当たり判定の基準が変わります。")]
    [SerializeField] private Transform headRoot;

    [Header("移動: 平面モード")]
    [Tooltip("移動を XY 平面で扱うか XZ 平面で扱うかの設定です。ProjectWorldToPlane と LiftPlaneToWorld の座標変換に使います。切り替えると同じ target でも移動方向の解釈が変わります。")]
    [SerializeField] private MovementPlaneMode movementPlaneMode = MovementPlaneMode.XY;

    [Header("ワープ移動: 停止時間(秒)")]
    [Tooltip("各ワープのあとに待つ時間です。TickMovement で pauseTimer を減算し、0 以下になったタイミングで次の WarpStep を許可します。大きくするとカクカクした遅い追跡になり、小さくすると連続的にワープしやすくなります。")]
    [SerializeField] private float pauseDuration = 0.25f;

    [Header("ワープ移動: 1回の移動距離")]
    [Tooltip("1 回のワープで進む平面距離です。WarpStep で currentMoveDirectionWorld に沿って移動量として使います。大きくすると一歩が長くなり、小さくすると細かく刻む移動になります。0 以下だと位置は変えず、停止時間だけ進みます。")]
    [SerializeField] private float stepLength = 1f;

    [Header("向き: 常時ターゲット方向へ向ける")]
    [Tooltip("有効にすると、TickMovement 中に currentMoveDirectionWorld を使って headRoot の向きを更新します。無効なら移動方向が更新されても見た目の回転は変えません。")]
    [SerializeField] private bool faceTargetContinuously = true;

    [Header("向き: 停止中揺れ角度")]
    [Tooltip("停止中に向きへ加える揺れ角度の振幅です。UpdateFacing で isPaused かつ wobbleFrequency が正のときに使います。大きくすると停止中の揺れが大きくなり、0 なら揺れません。")]
    [SerializeField] private float wobbleAngleAmplitude = 0f;

    [Header("向き: 停止中揺れ周波数")]
    [Tooltip("停止中揺れの速さです。UpdateFacing で Time.time と組み合わせて使用します。大きくすると細かく速く揺れ、小さくするとゆっくり揺れます。0 以下だと揺れません。")]
    [SerializeField] private float wobbleFrequency = 0f;

    [Header("デバッグ: 移動Gizmos表示")]
    [Tooltip("有効にすると OnDrawGizmosSelected で現在方向と target 投影点を表示します。移動ロジック確認用であり、ゲーム挙動そのものは変わりません。")]
    [SerializeField] private bool showMoveDebug = false;

    // =====================================================================
    // 実行時状態
    // 確認専用の Runtime 値。調整用ではなく、現在の移動内部状態観測に使う。
    // =====================================================================

    [Header("デバッグ(Runtime): 現在移動方向(World)")]
    [Tooltip("現在採用されているワールド移動方向です。TryUpdateDirection で更新され、TickMovement で実際のワープ方向として使います。調整用ではなく方向解決の観測用です。")]
    [SerializeField] private Vector3 currentMoveDirectionWorld;

    [Header("デバッグ(Runtime): 有効方向あり")]
    [Tooltip("現在有効な移動方向が解決できているかどうかです。target 未設定や target と同位置のときは false になります。調整用ではなく移動可否の観測用です。")]
    [SerializeField] private bool hasValidDirection;

    [Header("デバッグ(Runtime): 停止タイマー")]
    [Tooltip("次のワープまでの残り停止時間です。TickMovement で減算し、0 以下になったフレームで WarpStep を実行します。調整用ではなくワープ間隔確認用です。")]
    [SerializeField] private float pauseTimer;

    [Header("デバッグ(Runtime): 今フレームワープしたか")]
    [Tooltip("この TickMovement 呼び出しで実際に WarpStep が走ったかどうかです。攻撃側が節点追加タイミングを観測するときに使う想定で、調整用ではなく確認専用です。")]
    [SerializeField] private bool warpedThisTick;

    [Header("デバッグ(Runtime): 最終ワープ開始位置(World)")]
    [Tooltip("直近の WarpStep 実行時にワープ前だったワールド座標です。ワープ軌跡確認用であり、調整値ではありません。")]
    [SerializeField] private Vector3 lastWarpFromWorld;

    [Header("デバッグ(Runtime): 最終ワープ到達位置(World)")]
    [Tooltip("直近の WarpStep 実行時にワープ後だったワールド座標です。ワープ軌跡確認用であり、調整値ではありません。")]
    [SerializeField] private Vector3 lastWarpToWorld;

    // =====================================================================
    // 公開プロパティ
    // =====================================================================

    // 現在採用されている移動方向の参照口。
    public Vector3 CurrentMoveDirectionWorld => currentMoveDirectionWorld;

    // 移動可能状態かどうかの参照口。
    // target があり、かつ有効な方向が確定しているときだけ true。
    public bool IsMoving => target != null && hasValidDirection;

    // 今フレームのワープ発生有無の参照口。
    public bool WarpedThisTick => warpedThisTick;

    // 直近ワープ前位置の参照口。
    public Vector3 LastWarpFromWorld => lastWarpFromWorld;

    // 直近ワープ後位置の参照口。
    public Vector3 LastWarpToWorld => lastWarpToWorld;

    // =====================================================================
    // 初期化
    // =====================================================================

    // 起動時に移動状態を初期状態へ戻す。
    private void Awake()
    {
        ResetMovementState();
    }

    // =====================================================================
    // 公開操作
    // =====================================================================

    // headRoot を target に向けて平面上でワープ移動させる。
    // 外部の Update / Tick から呼ばれる前提で、停止タイマー満了時だけ stepLength 分ワープする。
    public void TickMovement(float deltaTime)
    {
        warpedThisTick = false;

        Transform activeHeadRoot = headRoot != null ? headRoot : transform;
        if (target == null)
        {
            hasValidDirection = false;
            currentMoveDirectionWorld = Vector3.zero;
            return;
        }

        if (!TryUpdateDirection())
        {
            if (faceTargetContinuously)
            {
                UpdateFacing(activeHeadRoot, false);
            }

            return;
        }

        if (faceTargetContinuously)
        {
            bool isPaused = pauseTimer > 0f;
            UpdateFacing(activeHeadRoot, isPaused);
        }

        if (deltaTime > 0f && float.IsFinite(deltaTime))
        {
            pauseTimer -= deltaTime;
        }

        if (pauseTimer > 0f)
        {
            return;
        }

        WarpStep(activeHeadRoot);
    }

    // headRoot を指定ワールド座標へ即時移動し、移動状態をリセットする。
    // 開始位置合わせや強制再配置の入口として使う。
    public void SnapToWorldPosition(Vector3 worldPosition)
    {
        Transform activeHeadRoot = headRoot != null ? headRoot : transform;
        activeHeadRoot.position = worldPosition;
        ResetMovementState();
    }

    // 追従先 target を差し替える。
    // target 変更後に古い方向を引きずらないよう、内部状態も同時にリセットする。
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        ResetMovementState();
    }

    // 移動平面モードを外部から同期する。
    // controller 側の攻撃平面を source of truth にしたいときに使う。
    public void SetMovementPlaneMode(MovementPlaneMode newPlaneMode)
    {
        movementPlaneMode = newPlaneMode;
        ResetMovementState();
    }

    // 1 回あたりのワープ距離を外部から更新する。
    // 非有限値は受け付けず、0 未満にはしない。
    public void SetStepLength(float length)
    {
        if (!float.IsFinite(length))
        {
            return;
        }

        stepLength = Mathf.Max(0f, length);
    }

    // ワープ間の停止時間を外部から更新する。
    // 非有限値は受け付けず、0 未満にはしない。
    public void SetPauseDuration(float duration)
    {
        if (!float.IsFinite(duration))
        {
            return;
        }

        pauseDuration = Mathf.Max(0f, duration);
    }

    // 方向・停止タイマー・ワープ観測値を初期状態へ戻す。
    // target 差し替えや強制再配置の直後に使う前提。
    public void ResetMovementState()
    {
        currentMoveDirectionWorld = Vector3.zero;
        hasValidDirection = false;
        pauseTimer = 0f;
        warpedThisTick = false;

        Transform activeHeadRoot = headRoot != null ? headRoot : transform;
        lastWarpFromWorld = activeHeadRoot.position;
        lastWarpToWorld = activeHeadRoot.position;
    }

    // =====================================================================
    // 座標変換
    // =====================================================================

    // ワールド座標を移動ロジック平面座標へ射影する。
    // XY モードなら (x, y)、XZ モードなら (x, z) を使う。
    private Vector2 ProjectWorldToPlane(Vector3 world)
    {
        return movementPlaneMode == MovementPlaneMode.XY
            ? new Vector2(world.x, world.y)
            : new Vector2(world.x, world.z);
    }

    // 平面座標をワールド座標へ戻す。
    // XY では z、XZ では y を referenceWorld から維持して、平面外の軸を固定する。
    private Vector3 LiftPlaneToWorld(Vector2 planePoint, Vector3 referenceWorld)
    {
        if (movementPlaneMode == MovementPlaneMode.XY)
        {
            return new Vector3(planePoint.x, planePoint.y, referenceWorld.z);
        }

        return new Vector3(planePoint.x, referenceWorld.y, planePoint.y);
    }

    // =====================================================================
    // 方向解決 / ワープ / 向き
    // =====================================================================

    // 現在位置から target への平面方向を更新する。
    // 同位置に近い場合は有効方向なしとして扱い、移動を停止できる状態へ戻す。
    private bool TryUpdateDirection()
    {
        Transform activeHeadRoot = headRoot != null ? headRoot : transform;
        if (target == null)
        {
            hasValidDirection = false;
            currentMoveDirectionWorld = Vector3.zero;
            return false;
        }

        Vector2 fromPlane = ProjectWorldToPlane(activeHeadRoot.position);
        Vector2 toPlane = ProjectWorldToPlane(target.position);
        Vector2 planeDelta = toPlane - fromPlane;

        float sqrMagnitude = planeDelta.sqrMagnitude;
        if (sqrMagnitude <= 1e-8f)
        {
            hasValidDirection = false;
            currentMoveDirectionWorld = Vector3.zero;
            return false;
        }

        Vector2 normalizedPlaneDirection = planeDelta / Mathf.Sqrt(sqrMagnitude);
        currentMoveDirectionWorld = movementPlaneMode == MovementPlaneMode.XY
            ? new Vector3(normalizedPlaneDirection.x, normalizedPlaneDirection.y, 0f)
            : new Vector3(normalizedPlaneDirection.x, 0f, normalizedPlaneDirection.y);

        hasValidDirection = true;
        return true;
    }

    // 現在方向に沿って 1 ステップ分ワープする。
    // stepLength が 0 以下、方向が無効、到達先が非有限値なら位置は変えず停止時間だけ再設定する。
    private void WarpStep(Transform activeHeadRoot)
    {
        float clampedStepLength = Mathf.Max(0f, stepLength);
        if (clampedStepLength <= 0f)
        {
            pauseTimer = Mathf.Max(0f, pauseDuration);
            return;
        }

        Vector3 warpFrom = activeHeadRoot.position;
        Vector2 fromPlane = ProjectWorldToPlane(warpFrom);
        Vector2 dirPlane = ProjectWorldToPlane(currentMoveDirectionWorld);
        float directionSqrMagnitude = dirPlane.sqrMagnitude;

        if (directionSqrMagnitude <= 1e-8f)
        {
            pauseTimer = Mathf.Max(0f, pauseDuration);
            return;
        }

        dirPlane /= Mathf.Sqrt(directionSqrMagnitude);
        Vector2 toPlane = fromPlane + dirPlane * clampedStepLength;
        Vector3 warpTo = LiftPlaneToWorld(toPlane, warpFrom);

        if (!IsFiniteVector3(warpTo))
        {
            pauseTimer = Mathf.Max(0f, pauseDuration);
            return;
        }

        activeHeadRoot.position = warpTo;
        lastWarpFromWorld = warpFrom;
        lastWarpToWorld = warpTo;
        warpedThisTick = true;
        pauseTimer = Mathf.Max(0f, pauseDuration);
    }

    // 現在方向へ見た目の向きを合わせる。
    // 停止中かつ揺れ設定が有効なときだけ、baseRotation に wobble を加える。
    private void UpdateFacing(Transform activeHeadRoot, bool isPaused)
    {
        Vector2 facingDirPlane = ProjectWorldToPlane(currentMoveDirectionWorld);
        if (facingDirPlane.sqrMagnitude <= 1e-8f)
        {
            return;
        }

        Quaternion baseRotation = movementPlaneMode == MovementPlaneMode.XY
            ? Quaternion.LookRotation(Vector3.forward, new Vector3(facingDirPlane.x, facingDirPlane.y, 0f))
            : Quaternion.LookRotation(new Vector3(facingDirPlane.x, 0f, facingDirPlane.y), Vector3.up);

        if (!isPaused || wobbleAngleAmplitude <= 0f || wobbleFrequency <= 0f)
        {
            activeHeadRoot.rotation = baseRotation;
            return;
        }

        float wobbleAngle = Mathf.Sin(Time.time * wobbleFrequency * Mathf.PI * 2f) * wobbleAngleAmplitude;
        Quaternion wobbleRotation = movementPlaneMode == MovementPlaneMode.XY
            ? Quaternion.AngleAxis(wobbleAngle, Vector3.forward)
            : Quaternion.AngleAxis(wobbleAngle, Vector3.up);

        activeHeadRoot.rotation = wobbleRotation * baseRotation;
    }

    // Vector3 の全成分が有限値かどうかを判定する。
    private static bool IsFiniteVector3(Vector3 value)
    {
        return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
    }

    // =====================================================================
    // Gizmos
    // =====================================================================

    // 選択時に現在移動方向と target の平面投影位置を表示する。
    // showMoveDebug が無効なら何も描かない。
    private void OnDrawGizmosSelected()
    {
        if (!showMoveDebug)
        {
            return;
        }

        Transform activeHeadRoot = headRoot != null ? headRoot : transform;
        Vector3 origin = activeHeadRoot.position;

        if (hasValidDirection)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(origin, origin + currentMoveDirectionWorld.normalized);
        }

        if (target != null)
        {
            Gizmos.color = Color.yellow;
            Vector3 projectedTarget = LiftPlaneToWorld(ProjectWorldToPlane(target.position), origin);
            Gizmos.DrawWireSphere(projectedTarget, 0.12f);
            Gizmos.DrawLine(origin, projectedTarget);
        }
    }
}