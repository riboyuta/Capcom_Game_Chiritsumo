using UnityEngine;

[DisallowMultipleComponent]
public sealed class RoomManager : MonoBehaviour
{
    public enum RoomDirection
    {
        None = 0,
        Left = 1,
        Right = 2,
        Up = 3,
        Down = 4,
    }

    [Header("開始部屋")]
    [Tooltip("ゲーム開始時に現在部屋として扱う Room です。")]
    [SerializeField] private Room initialRoom;

    [Header("参照: プレイヤー")]
    [Tooltip("部屋遷移判定に使うプレイヤー本体です。位置・速度・停止制御に使います。")]
    [SerializeField] private PlayerController playerController;

    [Header("参照: カメラ")]
    [Tooltip("部屋ごとのカメラ境界と注視設定を反映するカメラ制御です。")]
    [SerializeField] private PlayerCameraController playerCameraController;

    [Header("参照: チェックポイント")]
    [Tooltip("部屋遷移成功時に復帰地点を更新するチェックポイント管理です。")]
    [SerializeField] private CheckpointSystem checkpointSystem;

    [Header("遷移判定")]
    [Tooltip("部屋境界を越えたとみなす余白です。境界ぴったりでの誤判定を減らします。")]
    [SerializeField] private float transitionEpsilon = 0.05f;

    [Tooltip("遷移方向と同じ向きの速度が出ている時だけ遷移を許可します。")]
    [SerializeField] private bool requireMatchingVelocitySign = true;

    [Tooltip("上下左右のどの方向を優先して遷移判定するかの優先順位です。")]
    [SerializeField]
    private RoomDirection[] directionPriority =
    {
        RoomDirection.Right,
        RoomDirection.Left,
        RoomDirection.Up,
        RoomDirection.Down,
    };

    [Header("デバッグ表示")]
    [Tooltip("有効にすると部屋切り替えや判定ログを出力します。")]
    [SerializeField] private bool enableDebugLog = true;

    private Room currentRoom;
    private Room previousRoom;
    private Room pendingRoom;

    private RoomDirection lastTransitionDirection = RoomDirection.None;
    private bool isTransitioning;

    public Room CurrentRoom => currentRoom;
    public Room PreviousRoom => previousRoom;
    public Room PendingRoom => pendingRoom;
    public RoomDirection LastTransitionDirection => lastTransitionDirection;
    public bool IsTransitioning => isTransitioning;

    private void Awake()
    {
        // 実行時に未設定の参照を補完する。
        ResolveReferences();
    }

    private void OnValidate()
    {
        // Inspector 上でも軽量な参照補完だけ行う。
        ResolveReferences();
    }

    private void Start()
    {
        // 開始部屋が設定されている場合は現在部屋を確定する。
        if (initialRoom != null)
        {
            ForceSetCurrentRoom(initialRoom);
            return;
        }

        Debug.LogWarning("RoomManager: initialRoom が未設定のため currentRoom を確定できません。", this);
    }

    private void ResolveReferences()
    {
        // 未設定のプレイヤー参照だけを補完する。
        if (playerController == null)
        {
            playerController = FindFirstObjectByType<PlayerController>();
        }

        // 未設定のカメラ参照だけを補完する。
        if (playerCameraController == null)
        {
            playerCameraController = FindFirstObjectByType<PlayerCameraController>();
        }

        // 未設定のチェックポイント参照だけを補完する。
        if (checkpointSystem == null)
        {
            checkpointSystem = FindFirstObjectByType<CheckpointSystem>();
        }
    }

    public void ForceSetCurrentRoom(Room room)
    {
        // null の部屋は受け付けず状態を変更しない。
        if (room == null)
        {
            Debug.LogWarning("RoomManager: ForceSetCurrentRoom に null が渡されました。", this);
            return;
        }

        // 現在部屋を直接確定し、遷移中状態を初期化する。
        previousRoom = currentRoom;
        currentRoom = room;
        pendingRoom = null;
        lastTransitionDirection = RoomDirection.None;
        isTransitioning = false;

        if (enableDebugLog)
        {
            Debug.Log($"RoomManager: currentRoom を '{currentRoom.name}' に設定しました。", this);
        }
    }
}