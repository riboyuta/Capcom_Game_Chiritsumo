using UnityEngine;
using Game.Input;
using System.Collections.Generic;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Collider))]
public class CannonGimmick : MonoBehaviour, IRespawnResettable
{
    public enum CannonType
    {
        Arbitrary, // 任意のタイミングでボタンを押して発射
        Forced     // 一定時間経過後に強制発射
    }

    public enum RotationMode
    {
        Continuous, // 常に一定速度で回転
        Stepped     // 一定角度ずつカクカク回転
    }

    [Header("大砲の設定")]
    [Tooltip("任意発射(Arbitrary)か、強制発射(Forced)かを選択")]
    public CannonType type = CannonType.Arbitrary;

    [Header("発射設定")]
    [Tooltip("発射方向（自身の上方向ローカルY軸）に向けて撃ち出す速度")]
    public float fireSpeed = 25f;
    
    [Tooltip("最大飛行距離")]
    public float maxFlightDistance = 50f;
    
    [Tooltip("何かにぶつかったら飛行を終了するレイヤー指定")]
    public LayerMask collisionLayers;

    [Header("回転設定")]
    [Tooltip("Continuous: 常に回転, Stepped: 一定角度ずつカクカク回転")]
    public RotationMode rotationMode = RotationMode.Continuous;

    [Header("回転方向")]
    [Tooltip("時計回りに回転するかどうか（オフなら反時計回り）")]
    public bool isClockwise = false;

    private int GetRotationSign()
    {
        return isClockwise ? -1 : 1;
    }

    [Header("Continuous回転設定")]
    [Tooltip("Continuous時の回転速度 (度/秒) Z軸中心")]
    public float rotationSpeed = 60f;

    [Header("Stepped回転設定")]
    [Tooltip("1回のカクカクで動く角度")]
    public float steppedAngle = 45f;
    [Tooltip("角度を曲がりきるのにかかる時間(秒)")]
    public float steppedRotationDuration = 0.15f;
    [Tooltip("曲がった後に停止して待機する時間(秒)")]
    public float steppedWaitDuration = 0.8f;

    [Header("移動設定")]
    [Tooltip("2点間を往復移動するかどうか")]
    public bool enableMoving = false;
    [Tooltip("往復する目的地へのオフセット (例: Xに5を入れると右に5m進んで戻る)")]
    public Vector3 moveOffset = new Vector3(5f, 0f, 0f);
    [Tooltip("片道の移動にかかる時間(秒)")]
    public float moveDuration = 2f;
    [Tooltip("端に到達した際の待機時間(秒)")]
    public float moveWaitTime = 1f;

    [Header("タイミング設定 (Forced時)")]
    [Tooltip("プレイヤーが格納されてから発射されるまでの秒数")]
    public float forcedFireDelay = 0.5f;

    [Header("入力設定 (Arbitrary時)")]
    [Tooltip("撃ち出しに使う入力ボタン")]
    public InputActionBinding fireInput = new InputActionBinding(Key.C, Key.Space, Game.Input.RawGamepadButton.A);

    private bool hasPlayer = false;
    private GameObject storedPlayer;
    private PlayerFacade storedPlayerFacade;
    private Rigidbody storedPlayerRb;
    // プレイヤーの透明化状態を復帰するためのリスト
    // VisualPolicy.Hide がプレイヤー側で未実装のため、大砲側で手動管理する
    private List<Renderer> storedRenderers = new List<Renderer>();
    
    private float fireTimer = 0f;
    private RawInputSource rawInputSource;

    private Collider myCollider;

    // PlayerFacade 外部制御セッション
    private PlayerExternalControlSession activeSession = PlayerExternalControlSession.Invalid;

    private enum SteppedState { Waiting, Rotating }
    private SteppedState currentRotState = SteppedState.Waiting;
    private float rotTimer = 0f;
    private Quaternion startRotation;
    private Quaternion targetRotation;

    private Vector3 originPos;
    private Vector3 destinationPos;
    private float moveTimer = 0f;
    private enum MoveState { MovingToDest, WaitingAtDest, MovingToOrigin, WaitingAtOrigin }
    private MoveState currentMoveState = MoveState.MovingToDest;

    private bool hasCapturedInitialState = false;
    private bool initialEnabledState = true;
    private Vector3 initialTransformPosition;
    private Quaternion initialTransformRotation;
    private Vector3 initialOriginPos;
    private Vector3 initialDestinationPos;
    private MoveState initialMoveState = MoveState.MovingToDest;
    private float initialMoveTimer = 0f;
    private SteppedState initialRotState = SteppedState.Waiting;
    private float initialRotTimer = 0f;
    private Quaternion initialStartRotation;
    private Quaternion initialTargetRotation;
    private readonly List<CannonFlightMonitor> spawnedFlightMonitors = new List<CannonFlightMonitor>();

    private void Awake()
    {
        myCollider = GetComponent<Collider>();
        startRotation = transform.rotation;
        targetRotation = transform.rotation;

        originPos = transform.position;
        destinationPos = originPos + moveOffset;
    }

    private void Update()
    {
        UpdateBaseMovement();

        if (rotationMode == RotationMode.Continuous)
        {
            transform.Rotate(Vector3.forward * rotationSpeed * GetRotationSign() * Time.deltaTime);
        }
        else
        {
            UpdateSteppedRotation();
        }

        if (hasPlayer && storedPlayer != null)
        {
            // セッション経由でプレイヤーを大砲の中心位置に固定する
            if (activeSession.IsValid)
            {
                activeSession.RequestAnchorPoseThisFrame(transform.position, Quaternion.identity);
            }

            if (type == CannonType.Arbitrary)
            {
                // 入力待ち
                if (CanFire() && CheckFireInput())
                {
                    FirePlayer();
                }
            }
            else if (type == CannonType.Forced)
            {
                // 時間待ち
                fireTimer -= Time.deltaTime;
                if (fireTimer <= 0f && CanFire())
                {
                    FirePlayer();
                }
            }
        }
    }

    private void UpdateSteppedRotation()
    {
        rotTimer += Time.deltaTime;

        if (currentRotState == SteppedState.Waiting)
        {
            if (rotTimer >= steppedWaitDuration)
            {
                currentRotState = SteppedState.Rotating;
                rotTimer = 0f;
                startRotation = transform.rotation;
                
                Vector3 euler = transform.eulerAngles;
                euler.z += steppedAngle * GetRotationSign();
                targetRotation = Quaternion.Euler(euler);
            }
        }
        else if (currentRotState == SteppedState.Rotating)
        {
            if (steppedRotationDuration <= 0f)
            {
                transform.rotation = targetRotation;
                CompleteRotationStep();
            }
            else
            {
                float t = rotTimer / steppedRotationDuration;
                if (t >= 1f)
                {
                    transform.rotation = targetRotation;
                    CompleteRotationStep();
                }
                else
                {
                    // 滑らかに補間
                    transform.rotation = Quaternion.Lerp(startRotation, targetRotation, t);
                }
            }
        }
    }

    private void CompleteRotationStep()
    {
        currentRotState = SteppedState.Waiting;
        rotTimer = 0f;
    }

    private void UpdateBaseMovement()
    {
        if (!enableMoving || moveDuration <= 0f) return;

        moveTimer += Time.deltaTime;

        if (currentMoveState == MoveState.MovingToDest)
        {
            float t = Mathf.Clamp01(moveTimer / moveDuration);
            t = Mathf.SmoothStep(0f, 1f, t);
            transform.position = Vector3.Lerp(originPos, destinationPos, t);

            if (t >= 1f)
            {
                currentMoveState = MoveState.WaitingAtDest;
                moveTimer = 0f;
            }
        }
        else if (currentMoveState == MoveState.WaitingAtDest)
        {
            if (moveTimer >= moveWaitTime)
            {
                currentMoveState = MoveState.MovingToOrigin;
                moveTimer = 0f;
            }
        }
        else if (currentMoveState == MoveState.MovingToOrigin)
        {
            float t = Mathf.Clamp01(moveTimer / moveDuration);
            t = Mathf.SmoothStep(0f, 1f, t);
            transform.position = Vector3.Lerp(destinationPos, originPos, t);

            if (t >= 1f)
            {
                currentMoveState = MoveState.WaitingAtOrigin;
                moveTimer = 0f;
            }
        }
        else if (currentMoveState == MoveState.WaitingAtOrigin)
        {
            if (moveTimer >= moveWaitTime)
            {
                currentMoveState = MoveState.MovingToDest;
                moveTimer = 0f;
            }
        }
    }

    private bool CanFire()
    {
        if (rotationMode == RotationMode.Continuous) return true;
        return currentRotState == SteppedState.Waiting;
    }

    private bool CheckFireInput()
    {
        if (rawInputSource == null) return false;

        bool isPrimaryPressed = rawInputSource.WasKeyPressedThisFrame(fireInput.PrimaryKeyboardKey);
        bool isSecondaryPressed = rawInputSource.WasKeyPressedThisFrame(fireInput.SecondaryKeyboardKey);
        bool isGamepadPressed = rawInputSource.WasGamepadButtonPressedThisFrame(fireInput.GamepadButton);

        return isPrimaryPressed || isSecondaryPressed || isGamepadPressed;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasPlayer) return;

        if (other.CompareTag("Player"))
        {
            HandlePlayerEnter(other.gameObject, other.attachedRigidbody, other);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (hasPlayer) return;

        if (collision.collider.CompareTag("Player"))
        {
            HandlePlayerEnter(collision.gameObject, collision.rigidbody, collision.collider);
        }
    }

    private Collider storedPlayerCollider;

    private void HandlePlayerEnter(GameObject playerObj, Rigidbody playerRb, Collider playerCol)
    {
        // PlayerFacade を取得する
        var facade = playerObj.GetComponent<PlayerFacade>();
        if (facade == null) return;

        // 外部制御セッションの要求を構築する
        var request = new PlayerExternalControlRequest
        {
            Owner = this,
            Mode = ExternalControlMode.Anchored,
            InputBlockFlags = PlayerController.InputBlockFlags.Move
                | PlayerController.InputBlockFlags.Jump
                | PlayerController.InputBlockFlags.Dash
                | PlayerController.InputBlockFlags.Grab,
            PhysicsPolicy = ExternalPhysicsPolicy.Suspend,
            GravityPolicy = ExternalGravityPolicy.ForceOff,
            VisualPolicy = ExternalVisualPolicy.Hide
        };

        // 外部制御を受け入れ可能か確認する
        if (!facade.CanAcceptExternalControl(request)) return;

        // 外部制御セッションを開始する
        if (!facade.TryBeginExternalControl(request, out PlayerExternalControlSession session) || !session.IsValid)
        {
            return;
        }

        hasPlayer = true;
        storedPlayer = playerObj;
        storedPlayerFacade = facade;
        storedPlayerRb = playerRb;
        storedPlayerCollider = playerCol;
        activeSession = session;

        // 互いの当たり判定を無視してガタつき（物理演算の反発）を防ぐ
        if (myCollider != null && storedPlayerCollider != null)
        {
            Physics.IgnoreCollision(myCollider, storedPlayerCollider, true);
        }

        // 物理挙動を停止する（重力で落ちないようにする）
        // PhysicsPolicy.Suspend を指定しているが、プレイヤー側で Rigidbody の
        // isKinematic 制御が未実装のため、大砲側で直接設定する
        if (storedPlayerRb != null)
        {
            storedPlayerRb.linearVelocity = Vector3.zero;
            storedPlayerRb.isKinematic = true;
        }

        // 見た目を透明にする
        // VisualPolicy.Hide を指定しているが、プレイヤー側で Renderer ON/OFF が
        // 未実装のため、大砲側で手動管理する
        storedRenderers.Clear();
        var renderers = storedPlayer.GetComponentsInChildren<Renderer>();
        foreach (var r in renderers)
        {
            // もともと有効なレンダラーだけオフにし、対象として覚える
            if (r.enabled)
            {
                storedRenderers.Add(r);
                r.enabled = false;
            }
        }

        fireTimer = forcedFireDelay;

        // 入力判定用に RawInputSource を探す
        if (rawInputSource == null)
        {
            // プレイヤーに付いている前提で取得
            rawInputSource = storedPlayer.GetComponent<RawInputSource>();
        }
    }


    private void FirePlayer()
    {
        if (!hasPlayer) return;
        hasPlayer = false;

        if (storedPlayer != null)
        {
            // 外部制御セッションを終了し、PlayerController の通常制御を復帰させる
            if (activeSession.IsValid)
            {
                activeSession.EndControl();
            }
            activeSession = PlayerExternalControlSession.Invalid;

            // 物理挙動を復帰する
            if (storedPlayerRb != null)
            {
                storedPlayerRb.isKinematic = false;
                // 重力は CannonFlightMonitor 側でコントロールする
            }

            // 発射した瞬間にテクスチャ（見た目）を再度表示させる
            foreach (var r in storedRenderers)
            {
                if (r != null)
                {
                    r.enabled = true;
                }
            }

            // フライト監視用の一時的なスクリプトをアタッチして飛ばす
            var monitor = storedPlayer.AddComponent<CannonFlightMonitor>();
            monitor.Initialize(
                transform.up, 
                fireSpeed, 
                maxFlightDistance, 
                collisionLayers, 
                storedPlayerCollider,
                myCollider
            );
            spawnedFlightMonitors.Add(monitor);

            // 参照のクリア処理
            storedPlayer = null;
            storedPlayerFacade = null;
            storedPlayerRb = null;
            storedPlayerCollider = null;
            storedRenderers.Clear();
        }
    }

    private void OnDisable()
    {
        // 大砲が無効化された場合も、保持中プレイヤーと入力参照を安全に解放する
        CleanupActiveSession();
        RestoreStoredPlayerState();
        ClearRuntimeReferences();
    }

    private void OnDrawGizmos()
    {
        if (!enableMoving) return;

        // エディタ実行中でない場合は現在のTransformをA地点、実行中は記憶したoriginPosをA地点にする
        Vector3 aPoint = Application.isPlaying ? originPos : transform.position;
        Vector3 bPoint = Application.isPlaying ? destinationPos : aPoint + moveOffset;

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(aPoint, 0.5f);
        
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(bPoint, 0.5f);
        
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(aPoint, bPoint);
    }

    public void CaptureInitialState()
    {
        // 初回の正しい初期状態だけ保存し、途中状態で上書きしない
        if (hasCapturedInitialState)
        {
            return;
        }

        initialEnabledState = enabled;
        initialTransformPosition = transform.position;
        initialTransformRotation = transform.rotation;
        initialOriginPos = originPos;
        initialDestinationPos = destinationPos;
        initialMoveState = currentMoveState;
        initialMoveTimer = moveTimer;
        initialRotState = currentRotState;
        initialRotTimer = rotTimer;
        initialStartRotation = startRotation;
        initialTargetRotation = targetRotation;
        hasCapturedInitialState = true;
    }

    public void ResetToRespawnState()
    {
        if (!hasCapturedInitialState)
        {
            CaptureInitialState();
        }

        CleanupActiveSession();
        RestoreStoredPlayerState();
        CleanupFlightMonitors();
        RestoreTransformAndMovementState();
        RestoreRotationState();
        fireTimer = 0f;
        enabled = initialEnabledState;
        ClearRuntimeReferences();
    }

    private void CleanupActiveSession()
    {
        // 外部制御セッションが残っている場合は明示終了する
        if (!activeSession.IsValid)
        {
            return;
        }

        activeSession.EndControl();
        activeSession = PlayerExternalControlSession.Invalid;
    }

    private void RestoreStoredPlayerState()
    {
        // プレイヤー保持中の途中状態を必ず通常状態へ戻す
        if (storedPlayerRb != null)
        {
            storedPlayerRb.isKinematic = false;
        }

        for (int i = 0; i < storedRenderers.Count; i++)
        {
            Renderer renderer = storedRenderers[i];
            if (renderer != null)
            {
                renderer.enabled = true;
            }
        }

        if (myCollider != null && storedPlayerCollider != null)
        {
            Physics.IgnoreCollision(myCollider, storedPlayerCollider, false);
        }
    }

    private void CleanupFlightMonitors()
    {
        // この大砲が発射時に生成したフライト監視を残さない
        for (int i = spawnedFlightMonitors.Count - 1; i >= 0; i--)
        {
            CannonFlightMonitor monitor = spawnedFlightMonitors[i];
            if (monitor == null)
            {
                spawnedFlightMonitors.RemoveAt(i);
                continue;
            }

            Destroy(monitor);
            spawnedFlightMonitors.RemoveAt(i);
        }
    }

    private void RestoreTransformAndMovementState()
    {
        // 位置と移動ステートを初期値へ戻す
        transform.position = initialTransformPosition;
        originPos = initialOriginPos;
        destinationPos = initialDestinationPos;
        currentMoveState = initialMoveState;
        moveTimer = initialMoveTimer;
    }

    private void RestoreRotationState()
    {
        // 回転と回転ステートを初期値へ戻す
        transform.rotation = initialTransformRotation;
        currentRotState = initialRotState;
        rotTimer = initialRotTimer;
        startRotation = initialStartRotation;
        targetRotation = initialTargetRotation;
    }

    private void ClearRuntimeReferences()
    {
        // 参照をクリアして次回動作に持ち越さない
        hasPlayer = false;
        storedPlayer = null;
        storedPlayerFacade = null;
        storedPlayerRb = null;
        storedPlayerCollider = null;
        storedRenderers.Clear();
        rawInputSource = null;
    }
}
