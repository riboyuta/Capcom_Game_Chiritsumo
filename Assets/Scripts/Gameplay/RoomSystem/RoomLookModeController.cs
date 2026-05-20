using Game.Input;
using UnityEngine;
using UnityEngine.InputSystem;

// セーフゾーン内で使用できる「ルーム確認カメラモード」の管理コンポーネント。
// プレイヤー操作停止、カメラ切り替え、入力処理をまとめて担当する。
[DisallowMultipleComponent]
public sealed class RoomLookModeController : MonoBehaviour
{
    private enum RoomLookState
    {
        None,
        Looking,
        Returning,
    }

    [Header("参照")]
    [Tooltip("低レベル入力を読む RawInputSource です。未設定時は実行時に探索します。")]
    [SerializeField] private RawInputSource rawInputSource;

    [Tooltip("プレイヤー操作停止に使う PlayerFacade です。未設定時は実行時に探索します。")]
    [SerializeField] private PlayerFacade playerFacade;

    [Tooltip("ルーム確認カメラを制御する PlayerCameraController です。未設定時は実行時に探索します。")]
    [SerializeField] private PlayerCameraController playerCameraController;

    [Tooltip("現在部屋と部屋遷移状態を確認する RoomManager です。未設定時は実行時に探索します。")]
    [SerializeField] private RoomManager roomManager;

    [Header("入力: 開始/終了")]
    [Tooltip("キーボードでルーム確認モードを開始/終了するキーです。")]
    [SerializeField] private Key toggleKey = Key.Tab;

    [Tooltip("ゲームパッドでルーム確認モードを開始/終了するボタンです。")]
    [SerializeField] private RawGamepadButton toggleGamepadButton = RawGamepadButton.Y;

    [Tooltip("キーボードでルーム確認モードをキャンセルするキーです。")]
    [SerializeField] private Key cancelKey = Key.Escape;

    [Tooltip("ゲームパッドでルーム確認モードをキャンセルするボタンです。")]
    [SerializeField] private RawGamepadButton cancelGamepadButton = RawGamepadButton.B;

    [Header("デバッグ")]
    [Tooltip("有効にするとルーム確認モードの開始/終了ログを出力します。")]
    [SerializeField] private bool enableDebugLog = true;

    // 入力のデッドゾーン判定用の閾値。スティックのドリフトを無視する。
    private const float InputDeadZoneThreshold = 0.0001f;

    // 現在のルーム確認モード状態。
    private RoomLookState state = RoomLookState.None;

    // プレイヤー外部制御セッション。
    private PlayerExternalControlSession roomLookControlSession = PlayerExternalControlSession.Invalid;

    // 参照解決が完了したかどうか。毎フレームのFindを避けるために使用。
    private bool referencesResolved;

    // ルーム確認モードがアクティブかどうか。
    public bool IsRoomLookActive => state != RoomLookState.None;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnValidate()
    {
        // エディタでの変更時には再度解決を試みる。
        referencesResolved = false;
    }

    private void Update()
    {
        // 参照が未解決の場合のみ解決を試みる。
        if (!referencesResolved)
        {
            ResolveReferences();
        }

        if (rawInputSource == null || playerCameraController == null)
        {
            return;
        }

        switch (state)
        {
            case RoomLookState.None:
                TickNormal();
                break;

            case RoomLookState.Looking:
                TickLooking();
                break;

            case RoomLookState.Returning:
                TickReturning();
                break;
        }
    }

    private void OnDisable()
    {
        ForceEndRoomLook();
    }

    private void TickNormal()
    {
        if (!WasTogglePressedThisFrame())
        {
            return;
        }

        TryBeginRoomLook();
    }

    private void TickLooking()
    {
        // ゾーンが無効になった場合は強制終了する。
        RoomLookZone zone = RoomLookZone.CurrentZone;
        if (zone == null || !zone.IsAvailable)
        {
            BeginReturnFromRoomLook();
            return;
        }

        if (WasTogglePressedThisFrame() || WasCancelPressedThisFrame())
        {
            BeginReturnFromRoomLook();
            return;
        }

        Vector2 lookInput = ReadRoomLookMoveInput();
        playerCameraController.UpdateRoomLookInput(lookInput, Time.deltaTime);
    }

    // 復帰中の更新処理。
    // カメラの戻り補間が完了するのを待ち、完了したらプレイヤー制御を解放する。
    private void TickReturning()
    {
        // カメラ側の戻り補間が終わるまではプレイヤー停止を維持する。
        // これにより、カメラとプレイヤーの同期が取れてから操作可能になる。
        if (playerCameraController != null && playerCameraController.IsRoomLookActive)
        {
            return;
        }

        // プレイヤーの外部制御を終了し、通常の操作を復帰させる。
        EndExternalControl();
        state = RoomLookState.None;

        if (enableDebugLog)
        {
            Debug.Log("RoomLookModeController: ルーム確認モードを終了しました。", this);
        }
    }

    // ルーム確認モードの開始を試みる。
    // 各種条件（部屋遷移中ではない、ゾーン内にいる、接地している等）を満たす場合のみ開始する。
    // 戻り値: 開始に成功した場合は true、失敗した場合は false。
    private bool TryBeginRoomLook()
    {
        // 部屋遷移中は開始できない。
        // カメラが移動中にルーム確認を始めると、カメラ制御が競合して不安定になる。
        if (roomManager != null && roomManager.IsTransitioning)
        {
            return false;
        }

        // 現在のセーフゾーンを取得。
        // ゾーンが存在しないか、無効化されている場合は開始できない。
        RoomLookZone zone = RoomLookZone.CurrentZone;
        if (zone == null || !zone.IsAvailable)
        {
            return false;
        }

        // ゾーンが指定している確認対象の部屋を取得。
        Room lookRoom = zone.LookRoom;
        if (lookRoom == null || lookRoom.RoomBounds == null)
        {
            return false;
        }

        // プレイヤー制御に必要な PlayerFacade が未設定の場合は開始できない。
        if (playerFacade == null)
        {
            Debug.LogWarning("RoomLookModeController: playerFacade が未設定のためプレイヤーを停止できません。", this);
            return false;
        }

        // ルーム確認モードは接地中のみ開始できる。
        // 空中・落下中・壁掴まり中に開始すると、解除時の物理状態や見た目が不安定になりやすいため。
        if (!playerFacade.IsGrounded)
        {
            if (enableDebugLog)
            {
                Debug.Log("RoomLookModeController: プレイヤーが接地していないためルーム確認モードを開始しません。", this);
            }

            return false;
        }

        // プレイヤーの外部制御を開始し、移動・ジャンプ・ダッシュ等を停止する。
        if (!BeginExternalControl())
        {
            return false;
        }

        // 確認対象の部屋の境界を取得。
        Bounds lookBounds = lookRoom.RoomBounds.WorldBounds;

        // カメラコントローラーにルーム確認モードを開始させる。
        // 失敗した場合はプレイヤー制御を解放して終了。
        if (!playerCameraController.BeginRoomLook(lookBounds))
        {
            EndExternalControl();
            return false;
        }

        // 状態を Looking に遷移。
        state = RoomLookState.Looking;

        if (enableDebugLog)
        {
            Debug.Log($"RoomLookModeController: ルーム確認モードを開始しました。Room='{lookRoom.name}'", this);
        }

        return true;
    }

    private bool BeginExternalControl()
    {
        if (roomLookControlSession.IsValid)
        {
            roomLookControlSession.EndControl();
            roomLookControlSession = PlayerExternalControlSession.Invalid;
        }

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

        if (!playerFacade.TryBeginExternalControl(request, out roomLookControlSession))
        {
            roomLookControlSession = PlayerExternalControlSession.Invalid;
            Debug.LogWarning("RoomLookModeController: external control の開始に失敗しました。", this);
            return false;
        }

        return true;
    }

    // ルーム確認モードからの復帰を開始する。
    // カメラをプレイヤー追従モードに戻す補間を開始し、状態を Returning に遷移する。
    private void BeginReturnFromRoomLook()
    {
        // Looking 状態でない場合は何もしない。
        if (state != RoomLookState.Looking)
        {
            return;
        }

        // カメラコントローラーにルーム確認モードの終了を通知。
        // カメラはプレイヤー位置への戻り補間を開始する。
        if (playerCameraController != null)
        {
            playerCameraController.EndRoomLook();
        }

        // 状態を Returning に遷移。
        // この状態では、カメラの補間が完了するまでプレイヤー制御を維持する。
        state = RoomLookState.Returning;

        if (enableDebugLog)
        {
            Debug.Log("RoomLookModeController: ルーム確認モードの復帰を開始しました。", this);
        }
    }

    private void ForceEndRoomLook()
    {
        if (playerCameraController != null && playerCameraController.IsRoomLookActive)
        {
            playerCameraController.CancelRoomLookAndSnapToFollow();
        }

        EndExternalControl();
        state = RoomLookState.None;
    }

    private void EndExternalControl()
    {
        if (roomLookControlSession.IsValid)
        {
            roomLookControlSession.EndControl();
        }

        roomLookControlSession = PlayerExternalControlSession.Invalid;
    }

    // ルーム確認モード中のカメラ移動入力を読み取る。
    // ゲームパッドとキーボードの両方をサポートし、ゲームパッド優先で処理する。
    // 戻り値: 正規化されたカメラ移動方向ベクトル。
    private Vector2 ReadRoomLookMoveInput()
    {
        if (rawInputSource == null)
        {
            return Vector2.zero;
        }

        // ゲームパッド入力を優先。
        // スティックに入力があればそれを使用する。
        Vector2 gamepadInput = rawInputSource.GamepadMoveVector;
        if (gamepadInput.sqrMagnitude > InputDeadZoneThreshold)
        {
            return Vector2.ClampMagnitude(gamepadInput, 1.0f);
        }

        // ゲームパッド入力がない場合はキーボード入力を使用。
        return Vector2.ClampMagnitude(rawInputSource.KeyboardMoveVector, 1.0f);
    }

    // トグルキー（モード開始/終了キー）が今フレームで押されたかを判定する。
    // キーボードとゲームパッドの両方をチェックし、どちらかが押されていれば true を返す。
    private bool WasTogglePressedThisFrame()
    {
        if (rawInputSource == null)
        {
            return false;
        }

        bool keyboardPressed = rawInputSource.GetKeyState(toggleKey).PressedThisFrame;
        bool gamepadPressed = rawInputSource.GetGamepadButtonState(toggleGamepadButton).PressedThisFrame;

        return keyboardPressed || gamepadPressed;
    }

    // キャンセルキー（モード終了専用キー）が今フレームで押されたかを判定する。
    // キーボードとゲームパッドの両方をチェックし、どちらかが押されていれば true を返す。
    private bool WasCancelPressedThisFrame()
    {
        if (rawInputSource == null)
        {
            return false;
        }

        bool keyboardPressed = rawInputSource.GetKeyState(cancelKey).PressedThisFrame;
        bool gamepadPressed = rawInputSource.GetGamepadButtonState(cancelGamepadButton).PressedThisFrame;

        return keyboardPressed || gamepadPressed;
    }

    private void ResolveReferences()
    {
        if (rawInputSource == null)
        {
            rawInputSource = FindFirstObjectByType<RawInputSource>();
        }

        if (playerFacade == null)
        {
            playerFacade = FindFirstObjectByType<PlayerFacade>();
        }

        if (playerCameraController == null)
        {
            playerCameraController = FindFirstObjectByType<PlayerCameraController>();
        }

        if (roomManager == null)
        {
            roomManager = FindFirstObjectByType<RoomManager>();
        }

        // すべての必須参照が解決できたらフラグを立てる。
        if (rawInputSource != null && playerFacade != null && 
            playerCameraController != null && roomManager != null)
        {
            referencesResolved = true;
        }
    }
}