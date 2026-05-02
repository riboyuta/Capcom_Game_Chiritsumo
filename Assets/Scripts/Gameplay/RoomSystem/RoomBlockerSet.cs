using UnityEngine;

[DisallowMultipleComponent]
public sealed class RoomBlockerSet : MonoBehaviour
{
    [Header("Blocker 設定")]
    [Tooltip("部屋境界の外周に置く Blocker の厚みです。XY 平面での侵入を防ぐために使います。")]
    [SerializeField] private float blockerThickness = 2.0f;

    private float blockerDepth = 2f;
    private const string BlockerLeftName = "Blocker_Left";
    private const string BlockerRightName = "Blocker_Right";
    private const string BlockerUpName = "Blocker_Up";
    private const string BlockerDownName = "Blocker_Down";

    public BoxCollider GetBlocker(RoomManager.RoomDirection blockedDirection)
    {
        // 指定された方向に対応する Blocker の Collider を返す。
        return blockedDirection switch
        {
            RoomManager.RoomDirection.Left => GetOrCreateBlocker(BlockerLeftName),
            RoomManager.RoomDirection.Right => GetOrCreateBlocker(BlockerRightName),
            RoomManager.RoomDirection.Up => GetOrCreateBlocker(BlockerUpName),
            RoomManager.RoomDirection.Down => GetOrCreateBlocker(BlockerDownName),
            _ => null,
        };
    }

    public void EnsureAllBlockersCreated()
    {
        // 全方向の Blocker を用意して重複生成を防ぐ。
        GetOrCreateBlocker(BlockerLeftName);
        GetOrCreateBlocker(BlockerRightName);
        GetOrCreateBlocker(BlockerUpName);
        GetOrCreateBlocker(BlockerDownName);
    }

    public void RebuildFromBounds(Bounds bounds)
    {
        // RoomBounds 全面を塞ぐサイズ・位置で各 Blocker を更新する。
        ConfigureBlocker(GetOrCreateBlocker(BlockerLeftName), RoomManager.RoomDirection.Left, bounds);
        ConfigureBlocker(GetOrCreateBlocker(BlockerRightName), RoomManager.RoomDirection.Right, bounds);
        ConfigureBlocker(GetOrCreateBlocker(BlockerDownName), RoomManager.RoomDirection.Down, bounds);
        ConfigureBlocker(GetOrCreateBlocker(BlockerUpName), RoomManager.RoomDirection.Up, bounds);
    }

    private BoxCollider GetOrCreateBlocker(string blockerName)
    {
        Transform child = transform.Find(blockerName);
        GameObject blockerObject;

        if (child == null)
        {
            // 既存が無い場合のみ新規生成する。
            blockerObject = new GameObject(blockerName);
            blockerObject.transform.SetParent(transform, false);
        }
        else
        {
            blockerObject = child.gameObject;
        }

        BoxCollider collider = blockerObject.GetComponent<BoxCollider>();
        if (collider == null)
        {
            collider = blockerObject.AddComponent<BoxCollider>();
        }

        collider.isTrigger = false;
        collider.enabled = false;

        return collider;
    }

    private void ConfigureBlocker(BoxCollider collider, RoomManager.RoomDirection blockedDirection, Bounds bounds)
    {
        // 方向ごとの境界面に沿って XY サイズと中心を設定する。
        Vector3 center = Vector3.zero;
        Vector3 size = Vector3.one;

        switch (blockedDirection)
        {
            case RoomManager.RoomDirection.Left:
                center = new Vector3(bounds.min.x - blockerThickness * 0.5f, bounds.center.y, bounds.center.z);
                size = new Vector3(blockerThickness, bounds.size.y, blockerDepth);
                break;
            case RoomManager.RoomDirection.Right:
                center = new Vector3(bounds.max.x + blockerThickness * 0.5f, bounds.center.y, bounds.center.z);
                size = new Vector3(blockerThickness, bounds.size.y, blockerDepth);
                break;
            case RoomManager.RoomDirection.Down:
                center = new Vector3(bounds.center.x, bounds.min.y - blockerThickness * 0.5f, bounds.center.z);
                size = new Vector3(bounds.size.x, blockerThickness, blockerDepth);
                break;
            case RoomManager.RoomDirection.Up:
                center = new Vector3(bounds.center.x, bounds.max.y + blockerThickness * 0.5f, bounds.center.z);
                size = new Vector3(bounds.size.x, blockerThickness, blockerDepth);
                break;
        }

        collider.transform.position = center;
        collider.transform.rotation = Quaternion.identity;
        collider.size = size;
    }
}