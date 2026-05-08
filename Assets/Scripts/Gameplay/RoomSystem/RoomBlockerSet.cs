using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class RoomBlockerSet : MonoBehaviour
{
    [Header("Blocker 設定")]
    [Tooltip("部屋境界の外周に置く Blocker の厚みです。XY 平面での侵入を防ぐために使います。")]
    [SerializeField] private float blockerThickness = 2.0f;


    [Header("Gate デバッグ設定")]
    [Tooltip("部屋境界同士が接触していると見なす許容誤差です。")]
    [SerializeField] private float contactTolerance = 0.05f;

    [Tooltip("Gate 候補として扱う区間の最小長です。")]
    [SerializeField] private float minGateLength = 0.5f;

    [Tooltip("Scene View に Gate 候補 Gizmo を描画します。")]
    [SerializeField] private bool enableGateDebugGizmos = true;

    [Tooltip("Gate 候補の生成結果をログ出力します。")]
    [SerializeField] private bool enableGateDebugLog = false;

    private float blockerDepth = 2f;
    private const string BlockerLeftName = "Blocker_Left";
    private const string BlockerRightName = "Blocker_Right";
    private const string BlockerUpName = "Blocker_Up";
    private const string BlockerDownName = "Blocker_Down";


    private struct GateSegment
    {
        public Room ownerRoom;
        public Room targetRoom;
        public RoomManager.RoomDirection side;
        public float intervalMin;
        public float intervalMax;
        public bool isAmbiguous;
        public string debugName;
    }

    private readonly List<GateSegment> gateSegments = new();
    internal IReadOnlyList<(Room ownerRoom, Room targetRoom, RoomManager.RoomDirection side, float intervalMin, float intervalMax, bool isAmbiguous, string debugName)> GateSegmentsForDebug
        => gateSegments.ConvertAll(segment => (segment.ownerRoom, segment.targetRoom, segment.side, segment.intervalMin, segment.intervalMax, segment.isAmbiguous, segment.debugName));

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
            Transform roomRoot = transform.parent;
            Transform legacyChild = roomRoot != null ? roomRoot.Find(blockerName) : null;

            if (legacyChild != null)
            {
                legacyChild.SetParent(transform, true);
                blockerObject = legacyChild.gameObject;
            }
            else
            {
                // 既存が無い場合のみ新規生成する。
                blockerObject = new GameObject(blockerName);
                blockerObject.transform.SetParent(transform, false);
            }
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

    internal void RebuildGateSegments(Room ownerRoom, IReadOnlyList<Room> allRooms)
    {
        gateSegments.Clear();

        if (ownerRoom == null || ownerRoom.RoomBounds == null || allRooms == null)
        {
            return;
        }

        Bounds ownerBounds = ownerRoom.RoomBounds.WorldBounds;

        for (int i = 0; i < allRooms.Count; i++)
        {
            Room targetRoom = allRooms[i];
            if (targetRoom == null || targetRoom == ownerRoom || targetRoom.RoomBounds == null)
            {
                continue;
            }

            Bounds targetBounds = targetRoom.RoomBounds.WorldBounds;
            TryAddVerticalGate(ownerRoom, targetRoom, RoomManager.RoomDirection.Left, ownerBounds.min.x, targetBounds.max.x, ownerBounds, targetBounds);
            TryAddVerticalGate(ownerRoom, targetRoom, RoomManager.RoomDirection.Right, ownerBounds.max.x, targetBounds.min.x, ownerBounds, targetBounds);
            TryAddHorizontalGate(ownerRoom, targetRoom, RoomManager.RoomDirection.Up, ownerBounds.max.y, targetBounds.min.y, ownerBounds, targetBounds);
            TryAddHorizontalGate(ownerRoom, targetRoom, RoomManager.RoomDirection.Down, ownerBounds.min.y, targetBounds.max.y, ownerBounds, targetBounds);
        }

        MarkAmbiguousSegments(ownerRoom);

        if (enableGateDebugLog && gateSegments.Count > 0)
        {
            for (int i = 0; i < gateSegments.Count; i++)
            {
                GateSegment segment = gateSegments[i];
                Debug.Log($"RoomBlockerSet: Gate candidate '{segment.debugName}' side={segment.side} interval=({segment.intervalMin:F2}, {segment.intervalMax:F2}) ambiguous={segment.isAmbiguous}", this);
            }
        }
    }

    private void TryAddVerticalGate(Room ownerRoom, Room targetRoom, RoomManager.RoomDirection side, float ownerEdge, float targetEdge, Bounds ownerBounds, Bounds targetBounds)
    {
        if (Mathf.Abs(ownerEdge - targetEdge) > contactTolerance)
        {
            return;
        }

        float intervalMin = Mathf.Max(ownerBounds.min.y, targetBounds.min.y);
        float intervalMax = Mathf.Min(ownerBounds.max.y, targetBounds.max.y);
        if (intervalMax - intervalMin < minGateLength)
        {
            return;
        }

        gateSegments.Add(new GateSegment
        {
            ownerRoom = ownerRoom,
            targetRoom = targetRoom,
            side = side,
            intervalMin = intervalMin,
            intervalMax = intervalMax,
            isAmbiguous = false,
            debugName = $"{ownerRoom.name}->{targetRoom.name}:{side}",
        });
    }

    private void TryAddHorizontalGate(Room ownerRoom, Room targetRoom, RoomManager.RoomDirection side, float ownerEdge, float targetEdge, Bounds ownerBounds, Bounds targetBounds)
    {
        if (Mathf.Abs(ownerEdge - targetEdge) > contactTolerance)
        {
            return;
        }

        float intervalMin = Mathf.Max(ownerBounds.min.x, targetBounds.min.x);
        float intervalMax = Mathf.Min(ownerBounds.max.x, targetBounds.max.x);
        if (intervalMax - intervalMin < minGateLength)
        {
            return;
        }

        gateSegments.Add(new GateSegment
        {
            ownerRoom = ownerRoom,
            targetRoom = targetRoom,
            side = side,
            intervalMin = intervalMin,
            intervalMax = intervalMax,
            isAmbiguous = false,
            debugName = $"{ownerRoom.name}->{targetRoom.name}:{side}",
        });
    }

    private void MarkAmbiguousSegments(Room ownerRoom)
    {
        for (int i = 0; i < gateSegments.Count; i++)
        {
            GateSegment a = gateSegments[i];
            for (int j = i + 1; j < gateSegments.Count; j++)
            {
                GateSegment b = gateSegments[j];
                if (a.ownerRoom != ownerRoom || b.ownerRoom != ownerRoom || a.side != b.side)
                {
                    continue;
                }

                bool overlaps = a.intervalMin < b.intervalMax && b.intervalMin < a.intervalMax;
                if (!overlaps)
                {
                    continue;
                }

                a.isAmbiguous = true;
                b.isAmbiguous = true;
                gateSegments[i] = a;
                gateSegments[j] = b;
            }
        }
    }

    private void OnDrawGizmos()
    {
        if (!enableGateDebugGizmos || gateSegments.Count == 0)
        {
            return;
        }

        for (int i = 0; i < gateSegments.Count; i++)
        {
            DrawGateGizmo(gateSegments[i]);
        }
    }

    private void DrawGateGizmo(GateSegment segment)
    {
        if (segment.ownerRoom == null || segment.ownerRoom.RoomBounds == null)
        {
            return;
        }

        Bounds ownerBounds = segment.ownerRoom.RoomBounds.WorldBounds;
        Gizmos.color = segment.isAmbiguous ? Color.yellow : Color.blue;

        const float thickness = 0.1f;
        const float depth = 0.05f;
        Vector3 center;
        Vector3 size;

        switch (segment.side)
        {
            case RoomManager.RoomDirection.Left:
                center = new Vector3(ownerBounds.min.x, (segment.intervalMin + segment.intervalMax) * 0.5f, ownerBounds.center.z);
                size = new Vector3(thickness, segment.intervalMax - segment.intervalMin, depth);
                break;
            case RoomManager.RoomDirection.Right:
                center = new Vector3(ownerBounds.max.x, (segment.intervalMin + segment.intervalMax) * 0.5f, ownerBounds.center.z);
                size = new Vector3(thickness, segment.intervalMax - segment.intervalMin, depth);
                break;
            case RoomManager.RoomDirection.Up:
                center = new Vector3((segment.intervalMin + segment.intervalMax) * 0.5f, ownerBounds.max.y, ownerBounds.center.z);
                size = new Vector3(segment.intervalMax - segment.intervalMin, thickness, depth);
                break;
            case RoomManager.RoomDirection.Down:
                center = new Vector3((segment.intervalMin + segment.intervalMax) * 0.5f, ownerBounds.min.y, ownerBounds.center.z);
                size = new Vector3(segment.intervalMax - segment.intervalMin, thickness, depth);
                break;
            default:
                return;
        }

        Gizmos.DrawCube(center, size);
    }
}