using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CameraZone : MonoBehaviour
{
    [Header("対象プレイヤーカメラ")]
    [Tooltip("この Zone の境界を適用する PlayerCameraController です。未設定時は MainCamera から自動探索します。")]
    [SerializeField] private PlayerCameraController cameraController;

    [Header("プレイヤー所属判定 Volume")]
    [Tooltip("プレイヤーがこの Zone に属しているかを判定する Trigger 用 BoxCollider です。通常は子オブジェクト ZoneVolume を指定します。")]
    [SerializeField] private BoxCollider zoneVolume;

    [Header("カメラ境界: 最小点")]
    [Tooltip("この Zone で使うカメラ境界の最小点です。X/Y の小さい側として扱います。通常は子オブジェクト Bounds_Min を指定します。")]
    [SerializeField] private Transform boundsMin;

    [Header("カメラ境界: 最大点")]
    [Tooltip("この Zone で使うカメラ境界の最大点です。X/Y の大きい側として扱います。通常は子オブジェクト Bounds_Max を指定します。")]
    [SerializeField] private Transform boundsMax;

    [Header("プレイヤー判定タグ")]
    [Tooltip("この Zone の侵入対象として扱うタグです。通常は Player を指定します。")]
    [SerializeField] private string playerTag = "Player";

    [Header("デバッグ表示")]
    [Tooltip("有効にすると、ZoneVolume と Zone 境界を Scene 上に描画します。")]
    [SerializeField] private bool drawDebugGizmos = true;

    // プレイヤーが複数 Collider を持つ場合でも安定して所属判定するための集合。
    private readonly HashSet<int> insidePlayerColliderIds = new HashSet<int>();

    public Bounds WorldBounds => BuildWorldBounds();

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

    private void OnValidate()
    {
        AutoResolveReferences();
        EnsureZoneVolumeIsTrigger();
    }

    private void AutoResolveReferences()
    {
        // Camera 未設定なら MainCamera から補完。
        if (cameraController == null && Camera.main != null)
        {
            cameraController = Camera.main.GetComponent<PlayerCameraController>();
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

        // Bounds_Min 未設定なら子名から補完。
        if (boundsMin == null)
        {
            Transform min = transform.Find("Bounds_Min");
            if (min != null)
            {
                boundsMin = min;
            }
        }

        // Bounds_Max 未設定なら子名から補完。
        if (boundsMax == null)
        {
            Transform max = transform.Find("Bounds_Max");
            if (max != null)
            {
                boundsMax = max;
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
        if (cameraController == null)
        {
            Debug.LogWarning("CameraZone: PlayerCameraController reference is missing.", this);
            return;
        }

        if (boundsMin == null || boundsMax == null)
        {
            Debug.LogWarning("CameraZone: Bounds_Min / Bounds_Max reference is missing.", this);
            return;
        }

        cameraController.SetActiveBoundsOverride(BuildWorldBounds());
    }

    private void DeactivateZone()
    {
        if (cameraController == null)
        {
            Debug.LogWarning("CameraZone: PlayerCameraController reference is missing.", this);
            return;
        }

        cameraController.ClearActiveBoundsOverride();
    }

    private Bounds BuildWorldBounds()
    {
        if (boundsMin == null || boundsMax == null)
        {
            return new Bounds(transform.position, Vector3.zero);
        }

        Vector3 min = Vector3.Min(boundsMin.position, boundsMax.position);
        Vector3 max = Vector3.Max(boundsMin.position, boundsMax.position);

        Vector3 center = (min + max) * 0.5f;
        Vector3 size = max - min;

        return new Bounds(center, size);
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

        // Bounds 描画
        if (boundsMin != null && boundsMax != null)
        {
            Bounds bounds = BuildWorldBounds();

            Gizmos.matrix = Matrix4x4.identity;
            Gizmos.color = new Color(0f, 1f, 1f, 0.10f);
            Gizmos.DrawCube(bounds.center, bounds.size);

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(bounds.center, bounds.size);

            Gizmos.color = Color.blue;
            Gizmos.DrawSphere(boundsMin.position, 0.12f);

            Gizmos.color = Color.red;
            Gizmos.DrawSphere(boundsMax.position, 0.12f);
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