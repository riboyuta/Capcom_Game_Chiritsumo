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

    [Header("参照: プレイヤー公開窓口")]
    [Tooltip("部屋遷移中の入力遮断要求を送る公開窓口です。未設定時は実行時に探索を試みます。")]
    [SerializeField] private PlayerFacade playerFacade;

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
    private bool isTransitionInputBlocked;

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
            ApplyCurrentRoomCameraSettings();
            return;
        }

        Debug.LogWarning("RoomManager: initialRoom が未設定のため currentRoom を確定できません。", this);
    }
    private void Update()
    {
        // 現在部屋が確定していない間は遷移判定しない。
        if (currentRoom == null)
        {
            ApplyTransitionInputBlock();
            return;
        }

        // 遷移中フラグが立っている間は入力遮断を維持し、カメラ到達まで待機する。
        if (isTransitioning)
        {
            ApplyTransitionInputBlock();

            if (playerCameraController == null)
            {
                Debug.LogWarning("RoomManager: 遷移中ですが playerCameraController が未設定のため遷移を強制終了します。", this);
                EndRoomTransitionInputBlock();
                pendingRoom = null;
                isTransitioning = false;
                return;
            }

            if (!playerCameraController.IsRoomTransitionRunning || playerCameraController.HasReachedRoomTransitionTarget)
            {
                EndRoomTransitionInputBlock();
                pendingRoom = null;
                isTransitioning = false;

                if (enableDebugLog)
                {
                    Debug.Log("RoomManager: カメラ遷移完了を検出したため入力遮断を解除しました。", this);
                }
            }

            return;
        }

        // プレイヤー参照が無い場合は判定できない。
        if (playerController == null)
        {
            ApplyTransitionInputBlock();
            return;
        }

        // 現在部屋の境界情報が無い場合は警告して処理を止める。
        if (currentRoom.RoomBounds == null)
        {
            Debug.LogWarning($"RoomManager: currentRoom '{currentRoom.name}' の RoomBounds が未設定です。", this);
            ApplyTransitionInputBlock();
            return;
        }

        // プレイヤー位置と速度を使って境界外への移動方向を判定する。
        Vector3 playerPosition = playerController.transform.position;
        Vector3 playerVelocity = playerController.CurrentVelocity;
        Bounds currentBounds = currentRoom.RoomBounds.WorldBounds;

        if (TryGetTransitionDirection(currentBounds, playerPosition, playerVelocity, out RoomDirection direction))
        {
            TryTransition(direction);
        }

        ApplyTransitionInputBlock();
    }

    private void ResolveReferences()
    {
        // 未設定のプレイヤー参照だけを補完する。
        if (playerController == null)
        {
            playerController = FindFirstObjectByType<PlayerController>();
        }

        if (playerFacade == null)
        {
            playerFacade = FindFirstObjectByType<PlayerFacade>();
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

    private void ApplyTransitionInputBlock()
    {
        // 遷移中入力遮断が無効なら何もしない。
        if (!isTransitionInputBlocked)
        {
            return;
        }

        // 公開窓口参照がなければ要求できない。
        if (playerFacade == null)
        {
            Debug.LogWarning("RoomManager: playerFacade が未設定のため遷移中入力遮断を適用できません。", this);
            return;
        }

        // 遷移中は移動系入力を毎フレーム遮断する。
        playerFacade.RequestInputBlockThisFrame(
            PlayerController.InputBlockFlags.Move
            | PlayerController.InputBlockFlags.Jump
            | PlayerController.InputBlockFlags.Dash
            | PlayerController.InputBlockFlags.Grab);
    }

    public void BeginRoomTransitionInputBlock()
    {
        // 遷移中入力遮断を開始する。
        isTransitionInputBlocked = true;

        if (enableDebugLog)
        {
            Debug.Log("RoomManager: 部屋遷移中入力遮断を開始しました。", this);
        }
    }

    [ContextMenu("End Room Transition Input Block")]
    public void EndRoomTransitionInputBlock()
    {
        // 遷移中入力遮断を終了する。
        isTransitionInputBlocked = false;

        if (enableDebugLog)
        {
            Debug.Log("RoomManager: 部屋遷移中入力遮断を終了しました。", this);
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

    private void ApplyCurrentRoomCameraSettings()
    {
        // 現在部屋が無ければ何も反映しない。
        if (currentRoom == null)
        {
            return;
        }

        // カメラ参照が無い場合は反映不能なので警告して終了する。
        if (playerCameraController == null)
        {
            Debug.LogWarning("RoomManager: playerCameraController が未設定のためカメラ設定を反映できません。", this);
            return;
        }

        // 現在部屋のカメラ設定を即時反映する。
        playerCameraController.ApplyRoomCameraSettings(currentRoom);

        // デバッグ有効時のみ、反映した部屋名と注視オフセットを出力する。
        if (enableDebugLog)
        {
            Debug.Log(
                $"RoomManager: カメラ設定を適用しました。Room='{currentRoom.name}', FocusOffset={currentRoom.RoomFocusOffset}",
                this);
        }
    }
    private void UpdateCheckpointForRoomEntry(Room room, RoomDirection direction)
    {
        // 遷移先の部屋が無ければ更新できない。
        if (room == null)
        {
            Debug.LogWarning("RoomManager: checkpoint 更新対象の room が null です。", this);
            return;
        }

        // チェックポイント管理が無ければ更新できない。
        if (checkpointSystem == null)
        {
            Debug.LogWarning($"RoomManager: checkpointSystem が未設定のため Room '{room.name}' の checkpoint を更新できません。", this);
            return;
        }

        // 遷移方向に対応する復帰位置を選ぶ。
        Transform respawnPoint = null;
        switch (direction)
        {
            case RoomDirection.Right:
                respawnPoint = room.RespawnFromLeft;
                break;
            case RoomDirection.Left:
                respawnPoint = room.RespawnFromRight;
                break;
            case RoomDirection.Up:
                respawnPoint = room.RespawnFromDown;
                break;
            case RoomDirection.Down:
                respawnPoint = room.RespawnFromUp;
                break;
            case RoomDirection.None:
                return;
        }

        // 対応する復帰位置が無い場合は遷移は維持して更新だけ見送る。
        if (respawnPoint == null)
        {
            Debug.LogWarning(
                $"RoomManager: Room '{room.name}' の direction '{direction}' に対応する respawn point が未設定のため checkpoint 更新をスキップします。",
                this);
            return;
        }

        // 復帰地点を新しい部屋入口に更新する。
        checkpointSystem.SetCheckpoint(respawnPoint);

        // デバッグ有効時だけ更新結果をログ出力する。
        if (enableDebugLog)
        {
            Debug.Log(
                $"RoomManager: checkpoint を更新しました。Room='{room.name}', Direction={direction}, Checkpoint='{respawnPoint.name}'",
                this);
        }
    }

    private bool TryGetTransitionDirection(
        Bounds bounds,
        Vector3 playerPosition,
        Vector3 playerVelocity,
        out RoomDirection direction)
    {
        // 優先順に方向を検査し、最初に成立した方向を返す。
        for (int i = 0; i < directionPriority.Length; i++)
        {
            RoomDirection candidate = directionPriority[i];
            bool exceededBounds = false;
            bool velocityMatched = true;

            switch (candidate)
            {
                case RoomDirection.Right:
                    exceededBounds = playerPosition.x > bounds.max.x + transitionEpsilon;
                    velocityMatched = playerVelocity.x > 0f;
                    break;

                case RoomDirection.Left:
                    exceededBounds = playerPosition.x < bounds.min.x - transitionEpsilon;
                    velocityMatched = playerVelocity.x < 0f;
                    break;

                case RoomDirection.Up:
                    exceededBounds = playerPosition.y > bounds.max.y + transitionEpsilon;
                    velocityMatched = playerVelocity.y > 0f;
                    break;

                case RoomDirection.Down:
                    exceededBounds = playerPosition.y < bounds.min.y - transitionEpsilon;
                    velocityMatched = playerVelocity.y < 0f;
                    break;
            }

            if (!exceededBounds)
            {
                continue;
            }

            if (requireMatchingVelocitySign && !velocityMatched)
            {
                continue;
            }

            direction = candidate;
            return true;
        }

        direction = RoomDirection.None;
        return false;
    }

    private bool TryTransition(RoomDirection direction)
    {
        // 現在部屋が無い場合は遷移できない。
        if (currentRoom == null)
        {
            return false;
        }

        // 遷移方向に応じた隣接部屋を選ぶ。
        Room nextRoom = null;
        switch (direction)
        {
            case RoomDirection.Left:
                nextRoom = currentRoom.LeftRoom;
                break;
            case RoomDirection.Right:
                nextRoom = currentRoom.RightRoom;
                break;
            case RoomDirection.Up:
                nextRoom = currentRoom.UpRoom;
                break;
            case RoomDirection.Down:
                nextRoom = currentRoom.DownRoom;
                break;
        }

        // 隣接部屋が未設定なら遷移しない。
        if (nextRoom == null)
        {
            return false;
        }

        // 状態切り替え直後にカメラ設定反映と遷移開始を行う。
        isTransitioning = true;
        previousRoom = currentRoom;
        pendingRoom = nextRoom;
        currentRoom = nextRoom;
        lastTransitionDirection = direction;
        ApplyCurrentRoomCameraSettings();
        UpdateCheckpointForRoomEntry(currentRoom, direction);
        if (playerCameraController != null)
        {
            playerCameraController.BeginRoomTransition(currentRoom);
        }
        else
        {
            Debug.LogWarning("RoomManager: playerCameraController が未設定のためカメラ遷移を開始できません。", this);
        }

        BeginRoomTransitionInputBlock();

        if (enableDebugLog)
        {
            Debug.Log(
                $"RoomManager: '{previousRoom.name}' -> '{currentRoom.name}' に遷移しました。Direction={direction}",
                this);
        }

        return true;
    }
}