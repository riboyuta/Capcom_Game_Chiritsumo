using UnityEngine;
using Game.Input;
using System.Collections.Generic;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Collider))]
public class CannonGimmick : MonoBehaviour
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

    [Tooltip("発射方向（自身の上方向ローカルY軸）に向けて撃ち出す速度")]
    public float fireSpeed = 25f;
    
    [Tooltip("最大飛行距離")]
    public float maxFlightDistance = 50f;
    
    [Tooltip("何かにぶつかったら飛行を終了するレイヤー指定")]
    public LayerMask collisionLayers;

    [Header("回転設定")]
    [Tooltip("Continuous: 常に回転, Stepped: 一定角度ずつカクカク回転")]
    public RotationMode rotationMode = RotationMode.Continuous;

    [Tooltip("時計回りに回転するかどうか（オフなら反時計回り）")]
    public bool isClockwise = false;

    private int GetRotationSign()
    {
        return isClockwise ? -1 : 1;
    }

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
    private PlayerController storedPlayerController;
    private Rigidbody storedPlayerRb;
    // プレイヤーの透明化状態を復帰するためのリスト
    private List<Renderer> storedRenderers = new List<Renderer>();
    
    private float fireTimer = 0f;
    private RawInputSource rawInputSource;

    private Collider myCollider;

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
            // プレイヤーを大砲の中心位置に固定しておく
            storedPlayer.transform.position = transform.position;

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
        var playerCtrl = playerObj.GetComponent<PlayerController>();
        if (playerCtrl != null)
        {
            hasPlayer = true;
            storedPlayer = playerObj;
            storedPlayerController = playerCtrl;
            storedPlayerRb = playerRb;
            storedPlayerCollider = playerCol;

            // 互いの当たり判定を無視してガタつき（物理演算の反発）を防ぐ
            if (myCollider != null && storedPlayerCollider != null)
            {
                Physics.IgnoreCollision(myCollider, storedPlayerCollider, true);
            }

            // 1. プレイヤーの操作(PlayerController)を無効化
            storedPlayerController.enabled = false;

            // 2. 物理挙動を無効化(重力で落ちないようにする)
            if (storedPlayerRb != null)
            {
                storedPlayerRb.linearVelocity = Vector3.zero;
                storedPlayerRb.isKinematic = true;
            }

            // 3. 見た目を透明にする
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
    }


    private void FirePlayer()
    {
        if (!hasPlayer) return;
        hasPlayer = false;

        if (storedPlayer != null)
        {
            // 回転や移動の固定解除のため、ここで元の物理設定に戻す準備をする
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

            // コールバック内で null にされたインスタンス変数を参照しないようにローカル変数に退避する
            var ctrlToRestore = storedPlayerController;

            // フライト監視用の一時的なスクリプトをアタッチして飛ばす
            var monitor = storedPlayer.AddComponent<CannonFlightMonitor>();
            monitor.Initialize(
                transform.up, 
                fireSpeed, 
                maxFlightDistance, 
                collisionLayers, 
                null, // 発射時に既に復帰させたためnullを渡す
                storedPlayerCollider,
                myCollider,
                () => {
                    // 飛行終了時にプレイヤーのコントロールを復帰
                    if (ctrlToRestore != null)
                    {
                        ctrlToRestore.enabled = true;
                    }
                }
            );

            // 参照のクリア処理
            storedPlayer = null;
            storedPlayerController = null;
            storedPlayerRb = null;
            storedPlayerCollider = null;
            storedRenderers.Clear();
        }
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
}
