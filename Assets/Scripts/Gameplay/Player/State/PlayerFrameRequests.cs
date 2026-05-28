internal sealed class PlayerFrameRequests
{
    // Update で検出したジャンプ押下を FixedUpdate まで保持する要求。
    public bool jumpRequested;

    // Update で検出したダッシュ押下を FixedUpdate まで保持する要求。
    public bool dashRequested;

    // Update で検出したストンピング押下を FixedUpdate まで保持する要求。
    public bool stompRequested;

    // このフレームで積まれた入力ブロック要求の合算フラグ。
    public PlayerController.InputBlockFlags requestedInputBlockFlagsThisFrame = PlayerController.InputBlockFlags.None;

    // この physics tick で積まれた移動補正要求の合算値。
    public PlayerLocomotionModifierRequest requestedLocomotionModifierThisTick = PlayerLocomotionModifierRequest.Identity;

    // この物理フレームで外部打ち上げ通知を受けたかどうかの要求フラグ。
    public bool wasExternallyLaunchedThisFrame;

    public void RequestInputBlock(PlayerController.InputBlockFlags flags)
    {
        requestedInputBlockFlagsThisFrame |= flags;
    }

    public void AccumulateLocomotionModifier(PlayerLocomotionModifierRequest request)
    {
        requestedLocomotionModifierThisTick.moveSpeedMultiplier *= request.moveSpeedMultiplier;
        requestedLocomotionModifierThisTick.groundAccelerationMultiplier *= request.groundAccelerationMultiplier;
        requestedLocomotionModifierThisTick.airAccelerationMultiplier *= request.airAccelerationMultiplier;
        requestedLocomotionModifierThisTick.gravityScaleMultiplier *= request.gravityScaleMultiplier;
        requestedLocomotionModifierThisTick.dashSpeedMultiplier *= request.dashSpeedMultiplier;
    }

    // 1フレームだけ有効な要求を初期化する。
    public void ResetPerFrameRequests()
    {
        requestedInputBlockFlagsThisFrame = PlayerController.InputBlockFlags.None;
    }

    // 1physics tick だけ有効な要求を初期化する。
    public void ResetPerTickRequests()
    {
        requestedLocomotionModifierThisTick = PlayerLocomotionModifierRequest.Identity;
        wasExternallyLaunchedThisFrame = false;
    }
}

public sealed partial class PlayerController
{
    [System.Flags]
    public enum InputBlockFlags
    {
        None = 0,
        Move = 1 << 0,
        Jump = 1 << 1,
        Dash = 1 << 2,
        Grab = 1 << 3
    }
}