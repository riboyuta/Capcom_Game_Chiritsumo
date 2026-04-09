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

    // --- Grind Rail (レール滑走) 関連の状態 ---

    // 現在レール上を滑らかに滑走中かどうか
    private bool isGrinding;

    // 現在乗っているレールギミック
    private RailGimmick currentRail;

    // レール上で現在位置しているセグメントのインデックス
    private int currentRailSegment;

    // 現在のセグメント上での進行距離
    private float distanceOnRailSegment;

    // ウェイポイントに対する進行方向 (+1: start to end, -1: end to start)
    private int grindDirection = 1;

    // レール再吸着ロックタイマー
    private float railReattachLockTimer;
}