public enum DashRefillReason
{
    Grounded,
    Gimmick,
    Scripted,
    TutorialAssist
}

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