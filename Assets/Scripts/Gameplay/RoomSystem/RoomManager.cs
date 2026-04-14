using UnityEngine;

[DisallowMultipleComponent]
public sealed class RoomManager : MonoBehaviour
{
    private void Awake();
    private void Start();
    private void OnValidate();

    private void ResolveReferences();
    private void EvaluateRoomTransition();
    private bool TryGetExitDirection(out RoomDirection direction);
    private bool TryStartTransition(RoomDirection direction);
    private void ApplyRoom(Room room);
    private void UpdateCheckpoint(Room room, RoomDirection direction);
    private void Log(string message);
    public enum RoomDirection
    {
        None = 0,
        Left = 1,
        Right = 2,
        Up = 3,
        Down = 4
    }

    [Header("開始部屋")]
    [Tooltip("ゲーム開始時に現在部屋として扱う Room です。")]
    [SerializeField] private Room initialRoom;

    [Header("参照: プレイヤー")]
    [Tooltip("部屋遷移判定に使う PlayerController です。位置、速度、後の入力停止連携に使います。")]
    [SerializeField] private PlayerController playerController;

    [Header("参照: カメラ")]
    [Tooltip("部屋ごとのカメラ境界と注視設定を反映する PlayerCameraController です。")]
    [SerializeField] private PlayerCameraController playerCameraController;

    [Header("参照: チェックポイント")]
    [Tooltip("部屋遷移成功時に復帰地点を更新する CheckpointSystem です。")]
    [SerializeField] private CheckpointSystem checkpointSystem;

    [Header("境界判定")]
    [Tooltip("部屋境界を超えたと判定するための余白です。0 より少し大きい値にすると境界ぴったりのガタつきを減らせます。")]
    [SerializeField, Min(0f)] private float exitEpsilon = 0.05f;

    [Header("デバッグ")]
    [Tooltip("有効にすると部屋遷移判定や部屋切り替えのログを出します。")]
    [SerializeField] private bool enableDebugLog = true;

    private Room currentRoom;
    private Room previousRoom;
    private RoomDirection lastTransitionDirection = RoomDirection.None;
    private bool isTransitioning;

    public Room CurrentRoom => currentRoom;
    public Room PreviousRoom => previousRoom;
    public RoomDirection LastTransitionDirection => lastTransitionDirection;
    public bool IsTransitioning => isTransitioning;


}
