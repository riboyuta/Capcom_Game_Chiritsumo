using UnityEngine;

// セーフゾーン内で「部屋確認カメラモード」を使えるようにするための判定コンポーネント。
// 敵スポーン処理は持たず、プレイヤーが今このゾーン内にいるかだけを管理する。
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public sealed class RoomLookZone : MonoBehaviour, IRespawnResettable
{
    // 現在プレイヤーが入っている RoomLookZone。
    // 1人プレイ前提なので、最もシンプルな static 管理にする。
    public static RoomLookZone CurrentZone { get; private set; }

    [Header("所属Room")]
    [Tooltip("このセーフゾーンが属する Room です。未設定時は親階層から自動検索します。")]
    [SerializeField] private Room parentRoom;

    [Header("判定設定")]
    [Tooltip("Player タグで判定するかです。")]
    [SerializeField] private bool usePlayerTag = true;

    [Tooltip("Player タグ判定に使うタグ名です。")]
    [SerializeField] private string playerTag = "Player";

    private Collider zoneCollider;
    private RoomManager roomManager;

    // プレイヤーが現在このゾーン内にいるか。
    private bool isPlayerInside;

    // 一度でもセーフゾーンから出たか。
    // true になると、再入場してもルーム確認モードは使えない。
    // 理由: ゾーン退出後は敵がスポーンする可能性があり、
    // 再度カメラモードを使うと不自然な挙動になるため。
    private bool hasExitedOnce;

    private bool hasCapturedInitialState;
    private bool initialEnabled;
    private bool initialColliderEnabled;
    private bool initialIsPlayerInside;
    private bool initialHasExitedOnce;

    public Room ParentRoom => parentRoom;

    public bool IsPlayerInside => isPlayerInside;

    public bool HasExitedOnce => hasExitedOnce;

    // このゾーンが見るべき Room。
    // parentRoom が未設定なら現在部屋を使う。
    public Room LookRoom
    {
        get
        {
            if (parentRoom != null)
            {
                return parentRoom;
            }

            return roomManager != null ? roomManager.CurrentRoom : null;
        }
    }

    // 今このゾーンでルーム確認モードを開始できるか。
    public bool IsAvailable
    {
        get
        {
            if (hasExitedOnce)
            {
                return false;
            }

            if (!isPlayerInside)
            {
                return false;
            }

            if (roomManager != null && roomManager.IsTransitioning)
            {
                return false;
            }

            Room lookRoom = LookRoom;
            if (lookRoom == null || lookRoom.RoomBounds == null)
            {
                return false;
            }

            // 所属Roomが設定されている場合は、現在部屋と一致している時だけ有効。
            if (parentRoom != null && roomManager != null && roomManager.CurrentRoom != parentRoom)
            {
                return false;
            }

            return true;
        }
    }

    private void Awake()
    {
        zoneCollider = GetComponent<Collider>();
        zoneCollider.isTrigger = true;

        if (parentRoom == null)
        {
            parentRoom = GetComponentInParent<Room>();
        }

        roomManager = FindFirstObjectByType<RoomManager>();
    }

    private void OnTriggerEnter(Collider other)
    {
        TryRegisterPlayer(other);
    }

    private void OnTriggerStay(Collider other)
    {
        // シーン開始時点で既にセーフゾーン内にいる場合の保険。
        TryRegisterPlayer(other);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsPlayer(other))
        {
            return;
        }

        isPlayerInside = false;

        // 現在の有効ゾーンがこのゾーンなら解除する。
        if (CurrentZone == this)
        {
            CurrentZone = null;
        }

        // 一度でもセーフゾーンから出たら、このRoomLookZoneは使用済みにする。
        // 以後、同じセーフゾーンへ戻ってもカメラモードは使えない。
        // これにより、敵スポーン後の再侵入時にカメラモードが使えてしまう問題を防ぐ。
        hasExitedOnce = true;
    }

    private void OnDisable()
    {
        isPlayerInside = false;

        if (CurrentZone == this)
        {
            CurrentZone = null;
        }
    }

    private void TryRegisterPlayer(Collider other)
    {
        if (!IsPlayer(other))
        {
            return;
        }

        // 一度セーフゾーンを出た後は、再入場しても有効ゾーンとして登録しない。
        // 敵スポーン後の再侵入時にカメラモードが使えてしまう問題を防ぐ。
        if (hasExitedOnce)
        {
            return;
        }

        isPlayerInside = true;
        CurrentZone = this;
    }

    public void CaptureInitialState()
    {
        if (hasCapturedInitialState)
        {
            return;
        }

        if (zoneCollider == null)
        {
            zoneCollider = GetComponent<Collider>();
        }

        initialEnabled = enabled;
        initialColliderEnabled = zoneCollider != null && zoneCollider.enabled;
        initialIsPlayerInside = isPlayerInside;
        initialHasExitedOnce = hasExitedOnce;

        hasCapturedInitialState = true;
    }

    public void ResetToRespawnState()
    {
        if (zoneCollider == null)
        {
            zoneCollider = GetComponent<Collider>();
        }

        if (hasCapturedInitialState)
        {
            enabled = initialEnabled;
            isPlayerInside = initialIsPlayerInside;
            hasExitedOnce = initialHasExitedOnce;

            if (zoneCollider != null)
            {
                zoneCollider.enabled = initialColliderEnabled;
                zoneCollider.isTrigger = true;
            }
        }
        else
        {
            enabled = true;
            isPlayerInside = false;
            hasExitedOnce = false;

            if (zoneCollider != null)
            {
                zoneCollider.enabled = true;
                zoneCollider.isTrigger = true;
            }
        }

        if (CurrentZone == this)
        {
            CurrentZone = null;
        }
    }

    private bool IsPlayer(Collider other)
    {
        if (other == null)
        {
            return false;
        }

        if (usePlayerTag && other.CompareTag(playerTag))
        {
            return true;
        }

        PlayerController player = other.GetComponentInParent<PlayerController>();
        return player != null;
    }
}