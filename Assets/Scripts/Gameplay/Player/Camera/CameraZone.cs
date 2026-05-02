using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CameraZone : MonoBehaviour, IRespawnResettable
{
    [Header("対象プレイヤーカメラ")]
    [Tooltip("この Zone の境界を適用する PlayerCameraController です。未設定時は MainCamera から自動探索します。")]
    [SerializeField] private PlayerCameraController cameraController;

    [Header("プレイヤー所属判定 Volume")]
    [Tooltip("プレイヤーがこの Zone に属しているかを判定する Trigger 用 BoxCollider です。通常は子オブジェクト ZoneVolume を指定します。")]
    [SerializeField] private BoxCollider zoneVolume;

    [Header("カメラ境界")]
    [Tooltip("この Zone で使う RoomBounds です。通常は子オブジェクト RoomBounds を指定します。")]
    [SerializeField] private RoomBounds zoneBounds;

    [Header("Zone 優先度")]
    [Tooltip("Zone が重なったときの優先度です。値が大きい Zone ほど優先されます。同値の場合は後から入った Zone が優先されます。")]
    [SerializeField] private int priority = 0;

    [Header("Orthographic Size 上書き")]
    [Tooltip("有効にすると、この Zone に入った間だけ PlayerCameraController の orthographicSize を上書きします。")]
    [SerializeField] private bool overrideOrthographicSize = false;

    [Header("Orthographic Size 設定値")]
    [Tooltip("overrideOrthographicSize が有効なときに、この Zone に入っている間だけ適用する orthographicSize です。値が大きいほど広い範囲が映り、値が小さいほど対象を大きく映します。")]
    [SerializeField, Min(0.01f)] private float orthographicSize = 7f;

    [Header("Follow Smoothing 上書き")]
    [Tooltip("有効にすると、この Zone に入った間だけ PlayerCameraController の追従 smoothTimeX / smoothTimeY を上書きします。")]
    [SerializeField] private bool overrideFollowSmoothing = false;

    [Tooltip("overrideFollowSmoothing が有効なときに使用する X 軸追従スムーズ時間です。0 で即時追従扱いになります。")]
    [SerializeField] private float smoothTimeX = 0.08f;

    [Tooltip("overrideFollowSmoothing が有効なときに使用する Y 軸追従スムーズ時間です。0 で即時追従扱いになります。")]
    [SerializeField] private float smoothTimeY = 0.12f;

    [Header("Orthographic Size 補間時間 上書き")]
    [Tooltip("有効にすると、この Zone に入った間だけ PlayerCameraController の orthographicSize 補間時間を上書きします。")]
    [SerializeField] private bool overrideOrthographicSizeSmoothTime = false;

    [Tooltip("overrideOrthographicSizeSmoothTime が有効なときに使用する補間時間です。0 で即時切り替えになります。")]
    [SerializeField] private float orthographicSizeSmoothTime = 0.10f;

    [Header("Activation Rules")]
    [Tooltip("有効にすると、この Zone の効果は activeDuration 秒経過で自動解除されます。")]
    [SerializeField] private bool enableTimeLimit = false;

    [Tooltip("enableTimeLimit が有効なときに使う Zone 有効時間 (秒) です。")]
    [SerializeField, Min(0.01f)] private float activeDuration = 1f;

    [Tooltip("有効にすると、この Zone は最初に発動した 1 回のみ有効になります。")]
    [SerializeField] private bool activateOnlyOnce = false;


    [Header("プレイヤー判定タグ")]
    [Tooltip("この Zone の侵入対象として扱うタグです。通常は Player を指定します。")]
    [SerializeField] private string playerTag = "Player";

    [Header("デバッグ表示")]
    [Tooltip("有効にすると、ZoneVolume を Scene 上に描画します。")]
    [SerializeField] private bool drawDebugGizmos = true;

    // プレイヤーが複数 Collider を持つ場合でも安定して所属判定するための集合。
    private readonly HashSet<int> insidePlayerColliderIds = new HashSet<int>();
    private bool activationConsumed = false;
    private bool isTimedActivationRunning = false;
    private float activationExpireTime = 0f;

    public RoomBounds ZoneBounds => zoneBounds;
    public bool HasOrthographicSizeOverride => overrideOrthographicSize;
    public float OrthographicSizeOverride => orthographicSize;
    public bool HasFollowSmoothingOverride => overrideFollowSmoothing;
    public float SmoothTimeXOverride => smoothTimeX;
    public float SmoothTimeYOverride => smoothTimeY;
    public bool HasOrthographicSizeSmoothTimeOverride => overrideOrthographicSizeSmoothTime;
    public float OrthographicSizeSmoothTimeOverride => orthographicSizeSmoothTime;
    public int Priority => priority;
    public Bounds WorldBounds => zoneBounds != null ? zoneBounds.WorldBounds : new Bounds(transform.position, Vector3.zero);
    private void Reset()
    {
        AutoResolveReferences();
        EnsureZoneVolumeIsTrigger();
    }

    private void Awake()
    {
        AutoResolveReferences();
        EnsureZoneVolumeIsTrigger();
    }
    private void Update()
    {
        if (!isTimedActivationRunning)
        {
            return;
        }

        if (Time.time < activationExpireTime)
        {
            return;
        }

        isTimedActivationRunning = false;
    }

    private void OnValidate()
    {
        AutoResolveReferences();
        EnsureZoneVolumeIsTrigger();
    }

    private void AutoResolveReferences()
    {
        // Camera 未設定なら MainCamera またはその親から補完。
        if (cameraController == null && Camera.main != null)
        {
            cameraController = Camera.main.GetComponentInParent<PlayerCameraController>();
        }

        // ZoneVolume 未設定なら子名から補完。
        if (zoneVolume == null)
        {
            Transform volume = transform.Find("ZoneVolume");
            if (volume != null)
            {
                zoneVolume = volume.GetComponent<BoxCollider>();
            }
        }

        // RoomBounds 未設定なら子名から補完。
        if (zoneBounds == null)
        {
            Transform bounds = transform.Find("RoomBounds");
            if (bounds != null)
            {
                zoneBounds = bounds.GetComponent<RoomBounds>();
            }
        }
    }

    private void EnsureZoneVolumeIsTrigger()
    {
        if (zoneVolume != null)
        {
            zoneVolume.isTrigger = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // ZoneVolume 自身に Collider があるわけではないので、
        // このメソッドを使うには CameraZone を ZoneVolume に付ける必要がある。
    }

    private void OnTriggerExit(Collider other)
    {
        // 上と同じ理由で未使用。
    }

    private void OnTriggerEnterFromVolume(Collider other)
    {
        if (!IsPlayerCollider(other))
        {
            return;
        }

        int colliderId = other.GetInstanceID();
        bool added = insidePlayerColliderIds.Add(colliderId);

        if (!added)
        {
            return;
        }

        if (insidePlayerColliderIds.Count == 1)
        {
            ActivateZone();
        }
    }

    private void OnTriggerExitFromVolume(Collider other)
    {
        if (!IsPlayerCollider(other))
        {
            return;
        }

        int colliderId = other.GetInstanceID();
        insidePlayerColliderIds.Remove(colliderId);

        if (insidePlayerColliderIds.Count > 0)
        {
            return;
        }

        DeactivateZone();
    }

    private bool IsPlayerCollider(Collider other)
    {
        if (other == null)
        {
            return false;
        }

        GameObject owner = other.attachedRigidbody != null
            ? other.attachedRigidbody.gameObject
            : other.transform.root.gameObject;

        return owner != null && owner.CompareTag(playerTag);
    }

    private void ActivateZone()
    {
        if (zoneBounds == null)
        {
            Debug.LogWarning("CameraZone: RoomBounds reference is missing.", this);
            return;
        }

        if (activateOnlyOnce && activationConsumed)
        {
            return;
        }
        activationConsumed = true;

        if (enableTimeLimit)
        {
            activationExpireTime = Time.time + activeDuration;
            isTimedActivationRunning = true;
            return;
        }

        isTimedActivationRunning = false;
    }
    

    private void DeactivateZone()
    {
        isTimedActivationRunning = false;
    }

    public void CaptureInitialState()
    {
    }

    public void ResetToRespawnState()
    {
        activationConsumed = false;
        isTimedActivationRunning = false;
        activationExpireTime = 0f;
        insidePlayerColliderIds.Clear();
    }


#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!drawDebugGizmos)
        {
            return;
        }

        // ZoneVolume 描画
        if (zoneVolume != null)
        {
            Gizmos.matrix = zoneVolume.transform.localToWorldMatrix;
            Gizmos.color = new Color(1f, 1f, 0f, 0.10f);
            Gizmos.DrawCube(zoneVolume.center, zoneVolume.size);

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(zoneVolume.center, zoneVolume.size);
        }

    }
#endif

    // ZoneVolume 側から中継で呼ばせる用。
    public void NotifyEnter(Collider other)
    {
        OnTriggerEnterFromVolume(other);
    }

    public void NotifyExit(Collider other)
    {
        OnTriggerExitFromVolume(other);
    }
}