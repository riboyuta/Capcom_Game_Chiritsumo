using UnityEngine;

// 角補正（ジャンプ時・ダッシュ時）を管理するシステム。
internal sealed class PlayerInputAssistSystem
{
    private readonly PlayerLocomotionDependencies deps;

    internal PlayerInputAssistSystem(PlayerLocomotionDependencies deps)
    {
        this.deps = deps;
    }

    // ============================================================
    // ジャンプ角補正
    // ============================================================

    // 上昇中に天井角へ引っかかったとき、横へ少しずらして抜けやすくする。
    internal bool TryApplyCornerCorrection()
    {
        if (!deps.Settings.InputAssist.UseCornerCorrection)
        {
            return false;
        }

        if (deps.RuntimeState.isGrounded || deps.RuntimeState.isDashing || deps.RuntimeState.isWallGrabbing || deps.RuntimeState.isLedgeClimbing)
        {
            return false;
        }

        Vector3 originalVelocity = deps.Rb.linearVelocity;
        if (originalVelocity.y <= 0f)
        {
            return false;
        }

        int preferredDirection = ResolveCornerCorrectionDirection();

        if (!TryFindJumpCornerCorrectionOffset(preferredDirection, out float offsetX))
        {
            return false;
        }

        Vector3 correctedPosition = deps.Rb.position;
        correctedPosition.x += offsetX;
        deps.Rb.position = correctedPosition;

        // 補正そのものでは速度を変えない。
        // 「横にずれた分の力で斜めに飛ぶ」状態を作らないため、速度は元のまま戻す。
        deps.Rb.linearVelocity = originalVelocity;
        return true;
    }

    // 角補正の試行方向を解決する。
    private int ResolveCornerCorrectionDirection()
    {
        float inputX = Mathf.Clamp(deps.InputReader.Move.x, -1f, 1f);
        if (inputX > 0.01f)
        {
            return 1;
        }

        if (inputX < -0.01f)
        {
            return -1;
        }

        float velocityX = deps.Rb.linearVelocity.x;
        if (velocityX > 0.01f)
        {
            return 1;
        }

        if (velocityX < -0.01f)
        {
            return -1;
        }

        return 0;
    }

    // ジャンプ角補正用の横オフセットを探す。
    // Celeste っぽく、一発テレポートではなく小刻みに横へ試す。
    private bool TryFindJumpCornerCorrectionOffset(int preferredDirection, out float resolvedOffsetX)
    {
        resolvedOffsetX = 0f;

        int firstDirection = preferredDirection != 0 ? preferredDirection : 1;
        int secondDirection = -firstDirection;

        return TryFindJumpCornerCorrectionOffsetInDirection(firstDirection, out resolvedOffsetX)
            || TryFindJumpCornerCorrectionOffsetInDirection(secondDirection, out resolvedOffsetX);
    }

    private bool TryFindJumpCornerCorrectionOffsetInDirection(int direction, out float resolvedOffsetX)
    {
        resolvedOffsetX = 0f;

        // 平天井に頭中央が当たっているなら角補正しない。
        if (IsHeadBlockedAtPosition(deps.Rb.position, 0))
        {
            return false;
        }

        // その方向の角が当たっていないなら、この方向には補正しない。
        if (!IsHeadBlockedAtPosition(deps.Rb.position, direction))
        {
            return false;
        }

        int probeCount = deps.Settings.InputAssist.CornerCorrectionProbeCount;
        float maxDistance = deps.Settings.InputAssist.CornerCorrectionDistance;

        for (int probeIndex = 1; probeIndex <= probeCount; probeIndex++)
        {
            float t = probeIndex / (float)probeCount;
            float offsetX = maxDistance * t * direction;

            Vector3 candidatePosition = deps.Rb.position + Vector3.right * offsetX;

            // 横へずらした後も頭中央がまだ詰まるなら不採用。
            if (IsHeadBlockedAtPosition(candidatePosition, 0))
            {
                continue;
            }

            if (!IsCapsuleFreeAtPosition(candidatePosition))
            {
                continue;
            }

            resolvedOffsetX = offsetX;
            return true;
        }

        return false;
    }

    // 指定位置で頭の指定側が天井に当たっているか確認する。
    // sideSign:
    // -1 = 左角
    //  0 = 頭中央
    //  1 = 右角
    private bool IsHeadBlockedAtPosition(Vector3 targetPosition, int sideSign)
    {
        Bounds bounds = deps.CapsuleCollider.bounds;
        Vector3 center = bounds.center + (targetPosition - deps.Rb.position);
        Vector3 up = deps.Transform.up;

        Vector3 origin = center;
        origin += up * (bounds.extents.y - 0.02f);
        origin += Vector3.right * sideSign * (bounds.extents.x - 0.01f);

        return Physics.Raycast(
            origin,
            up,
            deps.Settings.InputAssist.CornerCorrectionUpCheckDistance,
            deps.Settings.Detection.GroundLayerMask,
            QueryTriggerInteraction.Ignore);
    }

    // 指定位置にプレイヤーのカプセルを置けるか確認する。
    private bool IsCapsuleFreeAtPosition(Vector3 targetPosition)
    {
        GetCapsuleWorldPointsAtPosition(
            targetPosition,
            out Vector3 topPoint,
            out Vector3 bottomPoint,
            out float worldRadius);

        return !Physics.CheckCapsule(
            topPoint,
            bottomPoint,
            worldRadius * 0.98f,
            deps.Settings.Detection.GroundLayerMask,
            QueryTriggerInteraction.Ignore);
    }

    // ============================================================
    // ダッシュ角補正
    // ============================================================

    // ダッシュ中に壁角へ引っかかったとき、少し上へずらして抜けやすくする。
    internal bool TryApplyDashCornerCorrection()
    {
        if (!deps.Settings.Dash.UseDashCornerCorrection)
        {
            return false;
        }

        if (!deps.RuntimeState.isDashing)
        {
            return false;
        }

        if (deps.RuntimeState.isWallGrabbing || deps.RuntimeState.isLedgeClimbing)
        {
            return false;
        }

        if (!deps.RuntimeState.isTouchingWall || deps.RuntimeState.wallSide == 0)
        {
            return false;
        }

        // 水平成分がない真上/真下ダッシュでは補正しない。
        if (Mathf.Abs(deps.RuntimeState.dashDirection.x) <= 0.01f)
        {
            return false;
        }

        // 下方向ダッシュでは使わない。
        if (deps.RuntimeState.dashDirection.y < 0f)
        {
            return false;
        }

        // 実際に壁へ向かってダッシュしているときだけ発動。
        bool movingIntoWall = deps.RuntimeState.dashDirection.x * deps.RuntimeState.wallSide > 0.01f;
        if (!movingIntoWall)
        {
            return false;
        }

        float upDistance = deps.Settings.Dash.DashCornerCorrectionUpDistance;
        if (upDistance <= 0f)
        {
            return false;
        }

        int directionSign = deps.RuntimeState.dashDirection.x > 0f ? 1 : -1;
        Vector3 horizontalDirection = deps.Transform.right * directionSign;

        Vector3 currentPosition = deps.Rb.position;
        Vector3 candidatePosition = currentPosition + deps.Transform.up * upDistance;

        // 上へずらした位置そのものが詰まっているなら補正しない。
        if (IsCapsuleBlockedAtPosition(candidatePosition))
        {
            return false;
        }

        // 上へずらした後、前方へ抜けられるか確認する。
        // ここが詰まっているなら、角ではなく普通の壁として扱う。
        float forwardCheckDistance =
            deps.GetWorldCapsuleRadius() +
            deps.Settings.Detection.WallCheckDistance +
            0.01f;

        Vector3 forwardCandidatePosition =
            candidatePosition + horizontalDirection * forwardCheckDistance;

        if (IsCapsuleBlockedAtPosition(forwardCandidatePosition))
        {
            return false;
        }

        deps.Rb.position = candidatePosition;
        return true;
    }

    // ============================================================
    // ユーティリティ
    // ============================================================

    // 指定位置にカプセルを置いたときのワールド座標を求める。
    private void GetCapsuleWorldPointsAtPosition(
        Vector3 targetPosition,
        out Vector3 topPoint,
        out Vector3 bottomPoint,
        out float worldRadius)
    {
        Vector3 worldCenter = deps.Transform.TransformPoint(deps.CapsuleCollider.center);
        Vector3 positionDelta = targetPosition - deps.Rb.position;
        worldCenter += positionDelta;

        Vector3 up = deps.Transform.up;
        worldRadius = deps.GetWorldCapsuleRadius();

        float halfHeight = Mathf.Max(deps.CapsuleCollider.height * 0.5f, deps.CapsuleCollider.radius);
        float worldHalfHeight = halfHeight * Mathf.Abs(deps.Transform.lossyScale.y);
        float sphereOffset = Mathf.Max(0f, worldHalfHeight - worldRadius);

        topPoint = worldCenter + up * sphereOffset;
        bottomPoint = worldCenter - up * sphereOffset;
    }

    // 指定位置にプレイヤーカプセルを置いたとき、地形に重なるか確認する。
    private bool IsCapsuleBlockedAtPosition(Vector3 targetPosition)
    {
        GetCapsuleWorldPointsAtPosition(
            targetPosition,
            out Vector3 topPoint,
            out Vector3 bottomPoint,
            out float worldRadius);

        int solidLayerMask =
            deps.Settings.Detection.GroundLayerMask.value |
            deps.Settings.Detection.WallLayerMask.value;

        return Physics.CheckCapsule(
            topPoint,
            bottomPoint,
            worldRadius * 0.98f,
            solidLayerMask,
            QueryTriggerInteraction.Ignore);
    }
}
