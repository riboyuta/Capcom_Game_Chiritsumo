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

    public bool IsRailReattachLocked => runtimeState.railReattachLockTimer > 0f;

    public void SetRailReattachLock(float duration)
    {
        runtimeState.railReattachLockTimer = Mathf.Max(runtimeState.railReattachLockTimer, duration);
    }
}