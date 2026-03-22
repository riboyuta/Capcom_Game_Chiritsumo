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
// - headRoot を対象平面上で一定速度または距離連動速度で移動させる
// - リターゲット間隔や stopDistance による移動制御を行う
//
// 非責務:
// - target の選定ロジックは担当しない
// - 攻撃開始 / 停止の上位判断は担当しない
// - 見た目アニメーションや当たり判定は担当しない
//
// 依存先:
// - target: 追従先の Transform
// - headRoot: 実際に移動させる Transform
// - Time.deltaTime: TickMovement の進行に使用
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

    [Header("移動: 基本速度")]
    [Tooltip("平面上の移動速度です。TickMovement で step 計算に使います。大きくすると速く進み、小さくすると遅くなります。0 以下なら移動しません。")]
    [SerializeField] private float moveSpeed = 4f;

    [Header("移動: 平面モード")]
    [Tooltip("移動を XY 平面で扱うか XZ 平面で扱うかの設定です。ProjectWorldToPlane と LiftPlaneToWorld の座標変換に使います。切り替えると同じ target でも移動方向の解釈が変わります。")]
    [SerializeField] private MovementPlaneMode movementPlaneMode = MovementPlaneMode.XY;

    [Header("移動: 常時方向更新")]
    [Tooltip("有効にすると毎回 TickMovement で target 方向を再計算します。無効にすると初回または retargetInterval 経過時だけ更新し、途中は同じ方向へ進み続けます。")]
    [SerializeField] private bool updateDirectionContinuously = true;

    [Header("移動: 停止距離")]
    [Tooltip("target までの平面距離がこの値以下になったら移動を止める距離です。TickMovement の冒頭で判定に使います。大きくすると手前で止まり、小さくするとより近くまで寄ります。0 なら停止距離判定を実質無効化できます。")]
    [SerializeField] private float stopDistance = 0f;

    [Header("再照準: 更新間隔(秒)")]
    [Tooltip("updateDirectionContinuously が無効なときに、方向を再計算する間隔です。retargetTimer と比較して TryUpdateDirection を呼ぶか決めます。0 なら初回取得後は方向更新しません。")]
    [SerializeField] private float retargetInterval = 0f;

    [Header("速度補正: 距離連動ブースト有効")]
    [Tooltip("有効にすると、target までの距離に応じて速度倍率を上げます。EvaluateSpeedMultiplier で使われ、遠いと速く、近いと通常速度に近づきます。無効なら常に等速です。")]
    [SerializeField] private bool useDistanceSpeedBoost = false;

    [Header("速度補正: 最大ブースト距離")]
    [Tooltip("距離連動ブーストで最大倍率に達する基準距離です。EvaluateSpeedMultiplier で distanceForMaxBoost として使います。小さくすると短距離でもすぐ最大倍率になり、大きくすると緩やかに加速します。")]
    [SerializeField] private float distanceForMaxBoost = 6f;

    [Header("速度補正: 最大速度倍率")]
    [Tooltip("距離連動ブースト時の最大速度倍率です。useDistanceSpeedBoost が有効なときに使います。1 より大きいほど遠距離で速く動き、1 以下は実質通常速度になります。")]
    [SerializeField] private float maxSpeedMultiplier = 1.5f;

    [Header("デバッグ: 移動Gizmos表示")]
    [Tooltip("有効にすると OnDrawGizmosSelected で現在方向と target 投影点を表示します。移動ロジック確認用であり、ゲーム挙動そのものは変わりません。")]
    [SerializeField] private bool showMoveDebug = false;

    // =====================================================================
    // 実行時状態
    // 確認専用の Runtime 値。調整用ではなく、現在の移動内部状態観測に使う。
    // =====================================================================

    [Header("デバッグ(Runtime): 現在移動方向(World)")]
    [Tooltip("現在採用されているワールド移動方向です。TryUpdateDirection で更新され、TickMovement で実際の移動方向として使います。調整用ではなく方向解決の観測用です。")]
    [SerializeField] private Vector3 currentMoveDirectionWorld;

    [Header("デバッグ(Runtime): 再照準タイマー")]
    [Tooltip("次に方向更新するまでの経過時間です。updateDirectionContinuously が無効かつ retargetInterval が正のときに加算されます。調整用ではなく再照準タイミング確認用です。")]
    [SerializeField] private float retargetTimer;

    [Header("デバッグ(Runtime): 有効方向あり")]
    [Tooltip("現在有効な移動方向が解決できているかどうかです。target 未設定や target と同位置のときは false になります。調整用ではなく移動可否の観測用です。")]
    [SerializeField] private bool hasValidDirection;

    // =====================================================================
    // 公開プロパティ
    // =====================================================================

    // 現在採用されている移動方向の参照口。
    public Vector3 CurrentMoveDirectionWorld => currentMoveDirectionWorld;

    // 移動可能状態かどうかの参照口。
    // target があり、かつ有効な方向が確定しているときだけ true。
    public bool IsMoving => target != null && hasValidDirection;

    // =====================================================================
    // 公開操作
    // =====================================================================

    // headRoot を target に向けて平面上で進める。
    // 外部の Update / Tick から呼ばれる前提で、停止距離・再照準・速度倍率をまとめて処理する。
    public void TickMovement(float deltaTime)
    {
        if (deltaTime <= 0f)
        {
            return;
        }

        Transform activeHeadRoot = headRoot != null ? headRoot : transform;
        if (target == null)
        {
            return;
        }

        // 停止距離を設ける場合は、十分近い時点で移動を打ち切る。
        if (stopDistance > 0f)
        {
            float distanceToTargetOnPlane = DistanceOnPlane(activeHeadRoot.position, target.position);
            if (distanceToTargetOnPlane <= stopDistance)
            {
                return;
            }
        }

        bool shouldUpdateDirection = updateDirectionContinuously;
        if (!updateDirectionContinuously)
        {
            // 常時更新しない構成では、
            // 初回未解決時または再照準間隔経過時だけ方向を更新する。
            if (!hasValidDirection)
            {
                shouldUpdateDirection = true;
            }
            else if (retargetInterval > 0f)
            {
                retargetTimer += deltaTime;
                if (retargetTimer >= retargetInterval)
                {
                    shouldUpdateDirection = true;
                    retargetTimer = 0f;
                }
            }
        }

        if (shouldUpdateDirection)
        {
            TryUpdateDirection();
        }

        if (!hasValidDirection)
        {
            return;
        }

        float distance = DistanceOnPlane(activeHeadRoot.position, target.position);
        float speedMultiplier = EvaluateSpeedMultiplier(distance);
        float step = Mathf.Max(0f, moveSpeed) * speedMultiplier * deltaTime;
        if (step <= 0f)
        {
            return;
        }

        Vector2 currentPlanePos = ProjectWorldToPlane(activeHeadRoot.position);
        Vector2 moveDirPlane = ProjectWorldToPlane(currentMoveDirectionWorld);
        float dirSqrMagnitude = moveDirPlane.sqrMagnitude;
        if (dirSqrMagnitude <= 1e-8f)
        {
            hasValidDirection = false;
            currentMoveDirectionWorld = Vector3.zero;
            return;
        }

        moveDirPlane /= Mathf.Sqrt(dirSqrMagnitude);
        Vector2 nextPlanePos = currentPlanePos + moveDirPlane * step;
        activeHeadRoot.position = LiftPlaneToWorld(nextPlanePos, activeHeadRoot.position);
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

    // 方向・再照準タイマー・有効方向フラグを初期状態へ戻す。
    // target 差し替えや強制再配置の直後に使う前提。
    public void ResetMovementState()
    {
        currentMoveDirectionWorld = Vector3.zero;
        retargetTimer = 0f;
        hasValidDirection = false;
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
    // 方向解決 / 速度補正
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
        retargetTimer = 0f;
        return true;
    }

    // target までの距離に応じた速度倍率を返す。
    // useDistanceSpeedBoost が無効なら常に 1 を返し、有効時だけ距離で補間する。
    private float EvaluateSpeedMultiplier(float distance)
    {
        if (!useDistanceSpeedBoost)
        {
            return 1f;
        }

        float maxBoostDistance = Mathf.Max(0f, distanceForMaxBoost);
        float maxMultiplier = Mathf.Max(1f, maxSpeedMultiplier);
        if (maxBoostDistance <= 0f)
        {
            return maxMultiplier;
        }

        float t = Mathf.Clamp01(distance / maxBoostDistance);
        return Mathf.Lerp(1f, maxMultiplier, t);
    }

    // 2 点間の平面距離を返す。
    // 停止距離判定や速度倍率評価の基準距離として使う。
    private float DistanceOnPlane(Vector3 a, Vector3 b)
    {
        return Vector2.Distance(ProjectWorldToPlane(a), ProjectWorldToPlane(b));
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