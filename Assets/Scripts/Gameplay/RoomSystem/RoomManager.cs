using System;
using System.Collections.Generic;
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

    // 部屋遷移が完了した時に発火するイベント
    public event Action<Room> OnRoomTransitionComplete;

    [Header("開始部屋")]
    [Tooltip("ゲーム開始時に現在部屋として扱う Room です。")]
    [SerializeField] private Room initialRoom;

    [Header("参照: プレイヤー")]
    [Tooltip("部屋遷移判定に使うプレイヤー本体です。位置・速度・停止制御に使います。")]
    [SerializeField] private PlayerController playerController;

    [Header("参照: プレイヤー公開窓口")]
    [Tooltip("部屋遷移中の入力遮断要求を送る公開窓口です。未設定時は実行時に探索を試みます。")]
    [SerializeField] private PlayerFacade playerFacade;
    [Header("参照: GameController")]
    [Tooltip("部屋遷移開始時にランキング用タイマーを停止する GameController です。未設定時は実行時に自動取得します。")]
    [SerializeField] private GameController gameController;

    [Header("参照: カメラ")]
    [Tooltip("部屋ごとのカメラ境界と注視設定を反映するカメラ制御です。")]
    [SerializeField] private PlayerCameraController playerCameraController;

    [Header("参照: チェックポイント")]
    [Tooltip("部屋遷移成功時に復帰地点を更新するチェックポイント管理です。")]
    [SerializeField] private CheckpointSystem checkpointSystem;

    [Header("参照: ステージ初期化")]
    [Tooltip("部屋遷移完了時にステージ上の IRespawnResettable を全初期化するシステムです。")]
    [SerializeField] private StageResetSystem stageResetSystem;

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
    private PlayerExternalControlSession roomTransitionControlSession = PlayerExternalControlSession.Invalid;

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
        // Gate 候補の初期構築を行う（方向単位 Blocker は Step3 まで無効化）。
        InitializeRoomBlockers();

        // 開始部屋が設定されている場合は現在部屋を確定する。
        if (initialRoom != null)
        {
            ForceSetCurrentRoom(initialRoom);
            ApplyCurrentRoomCameraSettings();

            // 初期部屋設定完了イベントを発火
            OnRoomTransitionComplete?.Invoke(currentRoom);

            return;
        }

        Debug.LogWarning("RoomManager: initialRoom が未設定のため currentRoom を確定できません。", this);
    }
    private void Update()
    {
        // 現在部屋が確定していない間は遷移判定しない。
        if (currentRoom == null)
        {
            return;
        }

        // 遷移中フラグが立っている間はカメラ到達まで待機する。
        if (isTransitioning)
        {
            if (playerCameraController == null)
            {
                Debug.LogWarning("RoomManager: 遷移中ですが playerCameraController が未設定のため遷移を強制終了します。", this);
                EndRoomTransitionExternalControl();
                pendingRoom = null;
                isTransitioning = false;
                return;
            }

            if (!playerCameraController.IsRoomTransitionRunning || playerCameraController.HasReachedRoomTransitionTarget)
            {
                EndRoomTransitionExternalControl();
                pendingRoom = null;
                isTransitioning = false;

                // 部屋遷移完了イベントを発火
                OnRoomTransitionComplete?.Invoke(currentRoom);

                if (enableDebugLog)
                {
                    Debug.Log("RoomManager: カメラ遷移完了を検出したため external control を終了しました。", this);
                }

                return;
            }

            return;
        }

        // プレイヤー参照が無い場合は判定できない。
        if (playerController == null)
        {
            return;
        }

        // 現在部屋の境界情報が無い場合は警告して処理を止める。
        if (currentRoom.RoomBounds == null)
        {
            Debug.LogWarning($"RoomManager: currentRoom '{currentRoom.name}' の RoomBounds が未設定です。", this);
            return;
        }

        // プレイヤー位置と速度を使って境界外への移動方向を判定する。
        Vector3 playerPosition = playerController.transform.position;
        Vector3 playerVelocity = playerController.CurrentVelocity;
        Bounds currentBounds = currentRoom.RoomBounds.WorldBounds;

        if (TryGetTransitionDirection(currentBounds, playerPosition, playerVelocity, out RoomDirection direction))
        {
            TryTransition(direction, playerPosition);
        }
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

        if (gameController == null)
        {
            gameController = FindFirstObjectByType<GameController>();
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

        // 未設定のステージ初期化参照だけを補完する。
        if (stageResetSystem == null)
        {
            stageResetSystem = FindFirstObjectByType<StageResetSystem>();
        }
    }

    private void PauseRankingTimerOnRoomTransitionBegin()
    {
        if (gameController == null)
        {
            gameController = FindFirstObjectByType<GameController>();
        }

        if (gameController == null)
        {
            return;
        }

        // 部屋遷移中のカメラ移動時間と次部屋安全エリア待機時間をランキングタイムに含めない。
        gameController.PauseElapsedTime();
    }

    private bool BeginRoomTransitionExternalControl()
    {
        // 公開窓口参照がなければ要求できない。
        if (playerFacade == null)
        {
            Debug.LogWarning("RoomManager: playerFacade が未設定のため部屋遷移 external control を開始できません。", this);
            return false;
        }

        // 既存セッションが残っていたら先に終了してから開始する。
        if (roomTransitionControlSession.IsValid)
        {
            roomTransitionControlSession.EndControl();
            roomTransitionControlSession = PlayerExternalControlSession.Invalid;
        }

        // 部屋遷移中にプレイヤー停止するための要求を組み立てる。
        PlayerExternalControlRequest request = new PlayerExternalControlRequest
        {
            Owner = this,
            Mode = ExternalControlMode.ScriptDriven,
            InputBlockFlags = PlayerController.InputBlockFlags.Move
                              | PlayerController.InputBlockFlags.Jump
                              | PlayerController.InputBlockFlags.Dash
                              | PlayerController.InputBlockFlags.Grab,
            PhysicsPolicy = ExternalPhysicsPolicy.Suspend,
            GravityPolicy = ExternalGravityPolicy.ForceOff,
            VisualPolicy = ExternalVisualPolicy.Keep,
            VelocityPolicy = ExternalVelocityPolicy.ZeroAll,
        };

        // 開始できたらセッションを保持し、失敗時は警告する。
        if (!playerFacade.TryBeginExternalControl(request, out roomTransitionControlSession))
        {
            roomTransitionControlSession = PlayerExternalControlSession.Invalid;
            Debug.LogWarning("RoomManager: 部屋遷移 external control の開始に失敗しました。プレイヤーが移動できる可能性があります。", this);
            return false;
        }

        if (enableDebugLog)
        {
            Debug.Log("RoomManager: 部屋遷移 external control を開始しました。", this);
        }

        return true;
    }

    public void BeginRoomTransitionInputBlock()
    {
        // 後方互換のため API 名は維持し、内部では external control 開始へ委譲する。
        BeginRoomTransitionExternalControl();
    }

    [ContextMenu("End Room Transition Input Block")]
    public void EndRoomTransitionInputBlock()
    {
        // 後方互換のため API 名は維持し、内部では external control 終了へ委譲する。
        EndRoomTransitionExternalControl();
    }

    private void EndRoomTransitionExternalControl()
    {
        // 有効な遷移セッションがある場合だけ終了する。
        if (roomTransitionControlSession.IsValid)
        {
            roomTransitionControlSession.EndControl();
        }

        roomTransitionControlSession = PlayerExternalControlSession.Invalid;

        if (enableDebugLog)
        {
            Debug.Log("RoomManager: 部屋遷移 external control を終了しました。", this);
        }
    }

    private void ResetStageOnRoomTransitionBegin()
    {
        // ステージ初期化システムが無い場合は安全のため何もしない。
        if (stageResetSystem == null)
        {
            Debug.LogWarning("RoomManager: stageResetSystem が未設定のため部屋遷移開始時の全初期化を実行できません。", this);
            return;
        }

        // カメラ遷移開始直前にステージ全体の復帰状態リセットを行う。
        stageResetSystem.ResetAllToRespawnState();

        // デバッグ有効時のみ、部屋遷移開始境界で全初期化を呼んだことを記録する。
        if (enableDebugLog)
        {
            Debug.Log("RoomManager: 部屋遷移開始境界で StageResetSystem.ResetAllToRespawnState() を実行しました。", this);
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
        EndRoomTransitionExternalControl();

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
    private void UpdateCheckpointForRoomEntry(Room room)
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

        // 部屋ごとに1つだけ持つ復帰位置を使い、入室方向では分岐しない。
        Transform respawnPoint = room.RespawnPoint;

        // 部屋単位の復帰位置が無い場合は遷移は維持して更新だけ見送る。
        if (respawnPoint == null)
        {
            Debug.LogWarning(
                $"RoomManager: Room '{room.name}' の respawn point が未設定のため checkpoint 更新をスキップします。",
                this);
            return;
        }

        // 復帰地点を新しい部屋入口に更新する。
        checkpointSystem.SetCheckpoint(respawnPoint);

        // デバッグ有効時だけ更新結果をログ出力する。
        if (enableDebugLog)
        {
            Debug.Log(
                $"RoomManager: checkpoint を更新しました。Room='{room.name}', Checkpoint='{respawnPoint.name}'",
                this);
        }
    }

    private void InitializeRoomBlockers()
    {
        Room[] rooms = FindObjectsByType<Room>(FindObjectsSortMode.None);
        List<(Room room, RoomBlockerSet blockerSet)> blockerSets = new();
        for (int i = 0; i < rooms.Length; i++)
        {
            Room room = rooms[i];
            if (room == null || room.RoomBounds == null)
            {
                continue;
            }

            RoomBlockerSet blockerSet = EnsureRoomBlockerSet(room);
            blockerSets.Add((room, blockerSet));
        }

        for (int i = 0; i < blockerSets.Count; i++)
        {
            (Room room, RoomBlockerSet blockerSet) = blockerSets[i];
            blockerSet.RebuildGateSegments(room, rooms);
        }

        for (int i = 0; i < blockerSets.Count; i++)
        {
            blockerSets[i].blockerSet.BuildReverseGateLinks();
        }
    }

    private RoomBlockerSet EnsureRoomBlockerSet(Room room)
    {
        // Room 配下の専用子 "RoomBlockerSet" を再利用または生成する。
        Transform blockerSetTransform = room.transform.Find("RoomBlockerSet");
        if (blockerSetTransform == null)
        {
            GameObject blockerSetObject = new GameObject("RoomBlockerSet");
            blockerSetObject.transform.SetParent(room.transform, false);
            blockerSetTransform = blockerSetObject.transform;
        }

        RoomBlockerSet blockerSet = blockerSetTransform.GetComponent<RoomBlockerSet>();
        if (blockerSet == null)
        {
            blockerSet = blockerSetTransform.gameObject.AddComponent<RoomBlockerSet>();
        }

        return blockerSet;
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

    private bool TryTransition(RoomDirection moveDirection, Vector3 playerPosition)
    {
        // 現在部屋が無い場合は遷移できない。
        if (currentRoom == null)
        {
            return false;
        }

        RoomBlockerSet currentBlockerSet = EnsureRoomBlockerSet(currentRoom);
        RoomBlockerSet.GateHandle throughGate;
        Room nextRoom = null;
        bool blockedByAmbiguous = false;
        bool blockedByOneWay = false;
        bool foundByGate = currentBlockerSet.TryFindGate(
            moveDirection,
            playerPosition,
            out throughGate,
            out nextRoom,
            out blockedByAmbiguous,
            out blockedByOneWay);

        if (!foundByGate)
        {
            if (enableDebugLog)
            {
                string reason = blockedByAmbiguous
                    ? "ambiguous gate"
                    : blockedByOneWay
                        ? "one-way blocked gate"
                        : "no matching gate";
                Debug.Log($"RoomManager: Gate 検索で遷移先を決定できませんでした。Direction={moveDirection}, reason={reason}", this);
            }

            return false;
        }

        PauseRankingTimerOnRoomTransitionBegin();

        // 状態切り替え直後にステージ全体をリセット（カメラ遷移直前に敵を消すため）
        ResetStageOnRoomTransitionBegin();

        // カメラ設定反映と遷移開始を行う。
        isTransitioning = true;
        previousRoom = currentRoom;
        pendingRoom = nextRoom;
        currentRoom = nextRoom;
        lastTransitionDirection = moveDirection;
        ApplyCurrentRoomCameraSettings();
        UpdateCheckpointForRoomEntry(currentRoom);
        if (playerCameraController != null)
        {
            playerCameraController.BeginRoomTransition(currentRoom);
        }
        else
        {
            Debug.LogWarning("RoomManager: playerCameraController が未設定のためカメラ遷移を開始できません。", this);
        }

        if (currentRoom.EnableOneWayBlockerOnEntry)
        {
            RoomBlockerSet enteredRoomBlockerSet = EnsureRoomBlockerSet(currentRoom);
            if (enteredRoomBlockerSet.TryGetReverseGate(throughGate, out RoomBlockerSet.GateHandle reverseGate))
            {
                reverseGate.ownerSet.SetGateBlocked(reverseGate, true);
            }
            else
            {
                Debug.LogWarning($"RoomManager: ReverseGate が見つからないため one-way blocker を有効化できません。fromGateTarget='{nextRoom?.name}'", this);
            }
        }

        BeginRoomTransitionExternalControl();

        if (enableDebugLog)
        {
            Debug.Log(
                $"RoomManager: '{previousRoom.name}' -> '{currentRoom.name}' に遷移しました。Direction={moveDirection}",
                this);
        }

        return true;
    }
}
