using UnityEngine;

public sealed partial class PlayerController
{
    private readonly PlayerRuntimeState runtimeState = new PlayerRuntimeState();
    private readonly PlayerFrameRequests frameRequests = new PlayerFrameRequests();
    public bool IsDashActive => runtimeState.isDashing;
    public bool IsGrounded => runtimeState.isGrounded;
    public bool IsAirborne => !runtimeState.isGrounded;
    public bool IsWallGrabbing => runtimeState.isWallGrabbing;
    public int Facing => runtimeState.facing;

    // TODO: WallGrabTimeRemaining は壁掴まり時間制限の内部データ実装後に公開する。

    // 見た目向け単発イベント(1物理フレームだけ true)。
    private bool justLandedThisFrame;
    private bool justCrossedApexThisFrame;

    // 接地/壁判定を担当する専用センサー。
    private PlayerProbeSensor probeSensor;

    // 外部制御の受け皿を担当する内部システム。
    private PlayerExternalControlSystem externalControlSystem;


}