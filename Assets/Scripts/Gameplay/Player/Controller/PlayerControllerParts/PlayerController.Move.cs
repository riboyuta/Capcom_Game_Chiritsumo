using UnityEngine;

public struct PlayerLocomotionModifierRequest
{
    public float moveSpeedMultiplier;
    public float groundAccelerationMultiplier;
    public float airAccelerationMultiplier;
    public float gravityScaleMultiplier;
    public float dashSpeedMultiplier;

    public static PlayerLocomotionModifierRequest Identity => new PlayerLocomotionModifierRequest
    {
        moveSpeedMultiplier = 1f,
        groundAccelerationMultiplier = 1f,
        airAccelerationMultiplier = 1f,
        gravityScaleMultiplier = 1f,
        dashSpeedMultiplier = 1f
    };
}

public sealed partial class PlayerController
{
    public void RequestLocomotionModifierThisTick(PlayerLocomotionModifierRequest request)
    {
        frameRequests.requestedLocomotionModifierThisTick.moveSpeedMultiplier *= request.moveSpeedMultiplier;
        frameRequests.requestedLocomotionModifierThisTick.groundAccelerationMultiplier *= request.groundAccelerationMultiplier;
        frameRequests.requestedLocomotionModifierThisTick.airAccelerationMultiplier *= request.airAccelerationMultiplier;
        frameRequests.requestedLocomotionModifierThisTick.gravityScaleMultiplier *= request.gravityScaleMultiplier;
        frameRequests.requestedLocomotionModifierThisTick.dashSpeedMultiplier *= request.dashSpeedMultiplier;
    }

    private void ApplyGrindMovement(float deltaTime)
    {
        if (currentRail == null || currentRail.RailPath.Count < 2)
        {
            EndGrind(Vector3.zero);
            return;
        }

        // ジャンプ入力があれば離脱
        if (frameRequests.jumpRequested)
        {
            frameRequests.jumpRequested = false;
            // レールの傾きに関係なくひとまず上方向にジャンプさせる
            Vector3 jumpVel = rb.linearVelocity;
            jumpVel.y = movementSettings.Rail.GrindJumpVerticalVelocity;

            // ジャンプ後しばらく同じレールまたは他のレールに吸着しないようにロック
            runtimeState.railReattachLockTimer = movementSettings.Rail.ReattachLockTime;

            // 横方向の速度は保つ
            EndGrind(jumpVel);
            UpdateAudioEvents();
            return;
        }

        // 前進する距離
        float moveDelta = movementSettings.Rail.GrindSpeed * deltaTime;

        // レールをなぞる処理
        while (moveDelta > 0f)
        {
            Vector3 startP = currentRail.RailPath[currentRailSegment];
            Vector3 endP = currentRail.RailPath[currentRailSegment + 1];
            float segmentLength = Vector3.Distance(startP, endP);

            if (grindDirection > 0)
            {
                // 順方向に進む
                float remainingSegment = segmentLength - distanceOnRailSegment;
                if (moveDelta <= remainingSegment)
                {
                    // このセグメント内で移動が収まる場合
                    distanceOnRailSegment += moveDelta;
                    moveDelta = 0f;
                }
                else
                {
                    // 次のセグメントへまたがる場合
                    moveDelta -= remainingSegment;
                    currentRailSegment++;
                    distanceOnRailSegment = 0f;

                    // 終点に達した場合
                    if (currentRailSegment >= currentRail.RailPath.Count - 1)
                    {
                        Vector3 launchDir = (endP - startP).normalized;
                        Vector3 launchVel = launchDir * movementSettings.Rail.GrindSpeed;
                        EndGrind(launchVel);
                        return;
                    }
                }
            }
            else
            {
                // 逆方向に進む
                float remainingSegment = distanceOnRailSegment;
                if (moveDelta <= remainingSegment)
                {
                    distanceOnRailSegment -= moveDelta;
                    moveDelta = 0f;
                }
                else
                {
                    moveDelta -= remainingSegment;
                    currentRailSegment--;

                    // 始点に達した場合
                    if (currentRailSegment < 0)
                    {
                        Vector3 launchDir = (startP - endP).normalized; // 逆向き
                        Vector3 launchVel = launchDir * movementSettings.Rail.GrindSpeed;
                        EndGrind(launchVel);
                        return;
                    }
                    else
                    {
                        // 次（インデックス的には前）のセグメントの長さを取得
                        Vector3 nextStart = currentRail.RailPath[currentRailSegment];
                        Vector3 nextEnd = currentRail.RailPath[currentRailSegment + 1];
                        distanceOnRailSegment = Vector3.Distance(nextStart, nextEnd);
                    }
                }
            }
        }

        // 最終的な位置計算と速度反映
        Vector3 finalStart = currentRail.RailPath[currentRailSegment];
        Vector3 finalEnd = currentRail.RailPath[currentRailSegment + 1];
        Vector3 segDir = (finalEnd - finalStart).normalized;

        Vector3 targetPos = finalStart + segDir * distanceOnRailSegment;

        // 足元がレールに接するようにオフセットを計算
        float worldHalfHeight = (capsuleCollider.height * 0.5f) * Mathf.Abs(transform.lossyScale.y);
        Vector3 centerOffset = transform.TransformVector(capsuleCollider.center);
        float feetOffset = worldHalfHeight - centerOffset.y;
        targetPos.y += feetOffset;

        // Rigidbody を直接動かす
        rb.MovePosition(targetPos);

        // 見た目や他ロジック用に速度も更新
        rb.linearVelocity = segDir * movementSettings.Rail.GrindSpeed * grindDirection;

        // 向き更新（X成分がある場合のみ）
        if (Mathf.Abs(segDir.x) > 0.001f)
        {
            runtimeState.facing = segDir.x > 0f ? 1 : -1;
        }
    }
}