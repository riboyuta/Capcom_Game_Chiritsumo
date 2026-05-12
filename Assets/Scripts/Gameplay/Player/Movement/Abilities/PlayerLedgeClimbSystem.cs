using UnityEngine;

// 崖乗り上げ（レッジクライム）を管理するシステム。
internal sealed class PlayerLedgeClimbSystem
{
    private readonly PlayerLocomotionDependencies deps;

    internal PlayerLedgeClimbSystem(PlayerLocomotionDependencies deps)
    {
        this.deps = deps;
    }

    // ============================================================
    // 初期化・リセット
    // ============================================================

    // 復帰時に崖乗り上げ内部状態を初期化する。
    internal void ResetRuntimeTimers()
    {
        deps.RuntimeState.isLedgeClimbing = false;
        deps.RuntimeState.ledgeClimbStartTime = 0f;
    }

    // ============================================================
    // 崖乗り上げ処理
    // ============================================================

    // 崖の上にとげなどのハザードがあるか判定する（壁登り時の判定用）
    internal bool IsHazardAbove()
    {
        if (!deps.RuntimeState.isWallGrabbing)
        {
            return false;
        }

        int side = deps.RuntimeState.wallGrabSide;
        if (side == 0)
        {
            return false;
        }

        Bounds bounds = deps.CapsuleCollider.bounds;
        Vector3 wallDir = Vector3.right * side;

        // 前方チェック
        Vector3 forwardCheckOrigin = bounds.center;
        forwardCheckOrigin.y += bounds.extents.y * 0.3f;
        forwardCheckOrigin.x += wallDir.x * (bounds.extents.x - 0.01f);

        bool hasWallAhead = Physics.Raycast(
            forwardCheckOrigin,
            wallDir,
            deps.Settings.Wall.LedgeDetectForwardDistance,
            deps.Settings.Detection.GroundLayerMask,
            QueryTriggerInteraction.Ignore);

        if (hasWallAhead)
        {
            return false; // 壁がまだ続いているので上にはとげがない
        }

        // 地面チェック
        Vector3 groundCheckOrigin = forwardCheckOrigin;
        groundCheckOrigin.x += wallDir.x * deps.Settings.Wall.LedgeDetectForwardDistance;

        float groundCheckDistance = deps.Settings.Wall.LedgeGroundCheckDistance + bounds.extents.y;

        bool foundGround = Physics.Raycast(
            groundCheckOrigin,
            Vector3.down,
            out RaycastHit hit,
            groundCheckDistance,
            deps.Settings.Detection.GroundLayerMask,
            QueryTriggerInteraction.Ignore);

        if (foundGround)
        {
            // 崖の上にハザードがあるかチェック
            return HasHazardAtLedgeTop(hit.point, side);
        }

        return false;
    }

    // 崖乗り上げを開始できるか判定し、開始する。
    internal bool TryStartLedgeClimb()
    {
        if (!deps.Settings.Wall.UseLedgeClimb)
        {
            return false;
        }

        if (!deps.RuntimeState.isWallGrabbing)
        {
            return false;
        }

        if (deps.RuntimeState.isLedgeClimbing)
        {
            return false;
        }

        if (!CanDetectLedge(out Vector3 ledgeTopPosition))
        {
            return false;
        }

        StartLedgeClimb(ledgeTopPosition);
        return true;
    }

    // 崖の頂上を検出できるか判定する。
    // 検出手順:
    // 1. 頭上に障害物がないか確認
    // 2. 前方に壁が続いていないか確認
    // 3. 前方上部に地面があるか確認
    // 4. 崖の上にとげなどのハザードがないか確認
    private bool CanDetectLedge(out Vector3 ledgeTopPosition)
    {
        ledgeTopPosition = Vector3.zero;

        int side = deps.RuntimeState.wallGrabSide;
        if (side == 0)
        {
            return false;
        }

        Bounds bounds = deps.CapsuleCollider.bounds;
        Vector3 wallDir = Vector3.right * side;

        Vector3 playerHead = bounds.center;
        playerHead.y = bounds.max.y;

        Vector3 headCheckOrigin = playerHead;
        headCheckOrigin.x += wallDir.x * (bounds.extents.x - 0.01f);

        bool hasObstacleAbove = Physics.Raycast(
            headCheckOrigin,
            Vector3.up,
            deps.Settings.Wall.LedgeDetectUpDistance,
            deps.Settings.Detection.GroundLayerMask,
            QueryTriggerInteraction.Ignore);

        if (hasObstacleAbove)
        {
            return false;
        }

        Vector3 forwardCheckOrigin = bounds.center;
        forwardCheckOrigin.y += bounds.extents.y * 0.3f;
        forwardCheckOrigin.x += wallDir.x * (bounds.extents.x - 0.01f);

        bool hasWallAhead = Physics.Raycast(
            forwardCheckOrigin,
            wallDir,
            deps.Settings.Wall.LedgeDetectForwardDistance,
            deps.Settings.Detection.GroundLayerMask,
            QueryTriggerInteraction.Ignore);

        if (hasWallAhead)
        {
            return false;
        }

        Vector3 groundCheckOrigin = forwardCheckOrigin;
        groundCheckOrigin.x += wallDir.x * deps.Settings.Wall.LedgeDetectForwardDistance;

        float groundCheckDistance = deps.Settings.Wall.LedgeGroundCheckDistance + bounds.extents.y;

        bool foundGround = Physics.Raycast(
            groundCheckOrigin,
            Vector3.down,
            out RaycastHit hit,
            groundCheckDistance,
            deps.Settings.Detection.GroundLayerMask,
            QueryTriggerInteraction.Ignore);

        if (foundGround)
        {
            ledgeTopPosition = hit.point;

            // 崖の上にとげなどのハザードがあるか確認
            if (HasHazardAtLedgeTop(ledgeTopPosition, side))
            {
                return false;
            }

            return true;
        }

        return false;
    }

    // 崖の頂上にとげなどのハザードがあるか確認する。
    private bool HasHazardAtLedgeTop(Vector3 ledgeTopPosition, int side)
    {
        Vector3 checkPosition = ledgeTopPosition;
        checkPosition.x += side * deps.Settings.Wall.LedgeClimbForwardOffset;
        checkPosition.y += deps.Settings.Wall.LedgeClimbUpOffset;

        float checkRadius = deps.CapsuleCollider.radius * 1.2f;

        Collider[] hazards = Physics.OverlapSphere(
            checkPosition,
            checkRadius,
            deps.Settings.Detection.GroundLayerMask,
            QueryTriggerInteraction.Collide);

        foreach (Collider col in hazards)
        {
            if (col.GetComponent<SpikeBlock>() != null)
            {
                return true;
            }
        }

        return false;
    }

    // 崖乗り上げを開始する。
    private void StartLedgeClimb(Vector3 ledgeTopPosition)
    {
        deps.RuntimeState.isLedgeClimbing = true;
        deps.RuntimeState.ledgeClimbStartTime = Time.time;
        deps.RuntimeState.ledgeClimbStartPosition = deps.Rb.position;

        int side = deps.RuntimeState.wallGrabSide;
        Vector3 targetPosition = ledgeTopPosition;
        targetPosition.x += side * deps.Settings.Wall.LedgeClimbForwardOffset;
        targetPosition.y += deps.Settings.Wall.LedgeClimbUpOffset;

        deps.RuntimeState.ledgeClimbTargetPosition = targetPosition;

        // ExitWallGrab は WallActionSystem で処理されるべきだが、ここでは直接呼ぶ
        deps.RuntimeState.isWallGrabbing = false;
        deps.RuntimeState.wallGrabSide = 0;
        deps.Rb.useGravity = true;

        deps.RuntimeState.wallReattachLockTimer = deps.Settings.Wall.LedgeClimbDuration + 0.2f;

        deps.Rb.linearVelocity = Vector3.zero;
        deps.Rb.isKinematic = true;
        deps.CapsuleCollider.enabled = false;
    }

    // 崖乗り上げ中の移動を更新する。
    internal void UpdateLedgeClimb()
    {
        if (!deps.RuntimeState.isLedgeClimbing)
        {
            return;
        }

        float elapsed = Time.time - deps.RuntimeState.ledgeClimbStartTime;
        float duration = deps.Settings.Wall.LedgeClimbDuration;

        if (elapsed >= duration)
        {
            CompleteLedgeClimb();
            return;
        }

        float t = elapsed / duration;
        float easedT = EaseOutCubic(t);

        Vector3 currentPosition = Vector3.Lerp(
            deps.RuntimeState.ledgeClimbStartPosition,
            deps.RuntimeState.ledgeClimbTargetPosition,
            easedT);

        deps.Transform.position = currentPosition;
    }

    // 崖乗り上げを完了する。
    private void CompleteLedgeClimb()
    {
        deps.RuntimeState.isLedgeClimbing = false;
        deps.Transform.position = deps.RuntimeState.ledgeClimbTargetPosition;

        deps.CapsuleCollider.enabled = true;
        deps.Rb.isKinematic = false;
        deps.Rb.linearVelocity = Vector3.zero;
        deps.RuntimeState.isGrounded = false;

        deps.RuntimeState.isWallGrabbing = false;
        deps.RuntimeState.wallGrabSide = 0;
        deps.RuntimeState.isWallSliding = false;
    }

    // イージング関数: EaseOutCubic（滑らかな減速）
    private float EaseOutCubic(float t)
    {
        float f = t - 1f;
        return f * f * f + 1f;
    }
}
