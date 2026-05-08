using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class RoomBlockerSet : MonoBehaviour
{
    internal readonly struct GateHandle
    {
        internal readonly RoomBlockerSet ownerSet;
        internal readonly int index;
        internal bool IsValid => ownerSet != null && index >= 0;

        internal GateHandle(RoomBlockerSet ownerSet, int index)
        {
            this.ownerSet = ownerSet;
            this.index = index;
        }
    }

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
    private static readonly string[] LegacyBlockerNames = { BlockerLeftName, BlockerRightName, BlockerUpName, BlockerDownName };


    private struct GateSegment
    {
        public Room ownerRoom;
        public Room targetRoom;
        public RoomManager.RoomDirection side;
        public float intervalMin;
        public float intervalMax;
        public BoxCollider blockerCollider;
        public bool isBlocked;
        public bool isAmbiguous;
        public int reverseGateIndex;
        public string debugName;
    }

    private readonly List<GateSegment> gateSegments = new();
    internal IReadOnlyList<(Room ownerRoom, Room targetRoom, RoomManager.RoomDirection side, float intervalMin, float intervalMax, bool isAmbiguous, string debugName)> GateSegmentsForDebug
        => gateSegments.ConvertAll(segment => (segment.ownerRoom, segment.targetRoom, segment.side, segment.intervalMin, segment.intervalMax, segment.isAmbiguous, segment.debugName));

    private void Awake()
    {
        DisableLegacyDirectionBlockers();
    }

    public BoxCollider GetBlocker(RoomManager.RoomDirection blockedDirection)
    {
        // Legacy API: Step3 で Gate Collider 生成 API へ置き換える予定。
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
        // Legacy API: Step3 で Gate Collider 生成 API へ置き換える予定。
        // 全方向の Blocker を用意して重複生成を防ぐ。
        GetOrCreateBlocker(BlockerLeftName);
        GetOrCreateBlocker(BlockerRightName);
        GetOrCreateBlocker(BlockerUpName);
        GetOrCreateBlocker(BlockerDownName);
    }

    public void RebuildFromBounds(Bounds bounds)
    {
        // Legacy API: Step3 で Gate Collider 生成 API へ置き換える予定。
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

    private void DisableLegacyDirectionBlockers()
    {
        // 旧仕様の方向単位 Blocker が Scene に残っていても有効化しない。
        for (int i = 0; i < LegacyBlockerNames.Length; i++)
        {
            string blockerName = LegacyBlockerNames[i];

            Transform directChild = transform.Find(blockerName);
            DisableColliderOnTransform(directChild);

            Transform roomRoot = transform.parent;
            Transform roomChild = roomRoot != null ? roomRoot.Find(blockerName) : null;
            DisableColliderOnTransform(roomChild);
        }
    }

    private static void DisableColliderOnTransform(Transform target)
    {
        if (target == null)
        {
            return;
        }

        BoxCollider blocker = target.GetComponent<BoxCollider>();
        if (blocker != null)
        {
            blocker.enabled = false;
            blocker.isTrigger = false;
        }
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
        RebuildGateBlockerColliders(ownerRoom);
        BuildReverseGateLinks();

        if (enableGateDebugLog && gateSegments.Count > 0)
        {
            for (int i = 0; i < gateSegments.Count; i++)
            {
                GateSegment segment = gateSegments[i];
                Debug.Log($"RoomBlockerSet: Gate candidate '{segment.debugName}' side={segment.side} interval=({segment.intervalMin:F2}, {segment.intervalMax:F2}) ambiguous={segment.isAmbiguous}", this);
            }
        }
    }

    private void RebuildGateBlockerColliders(Room ownerRoom)
    {
        DisableLegacyDirectionBlockers();

        HashSet<string> usedGateBlockerNames = new();

        for (int i = 0; i < gateSegments.Count; i++)
        {
            GateSegment segment = gateSegments[i];
            string targetName = segment.targetRoom != null ? segment.targetRoom.name : "NullTarget";
            string blockerName = $"GateBlocker_{i}_{segment.side}_{targetName}";
            usedGateBlockerNames.Add(blockerName);

            BoxCollider collider = GetOrCreateGateBlockerCollider(blockerName);
            ConfigureGateBlocker(collider, segment, ownerRoom);

            segment.blockerCollider = collider;
            segment.isBlocked = false;
            gateSegments[i] = segment;

            if (enableGateDebugLog)
            {
                Debug.Log($"RoomBlockerSet: Gate collider '{collider.name}' side={segment.side} target={targetName} interval=({segment.intervalMin:F2}, {segment.intervalMax:F2})", this);
            }
        }

        DisableUnusedGateBlockers(usedGateBlockerNames);
    }

    private BoxCollider GetOrCreateGateBlockerCollider(string blockerName)
    {
        Transform child = transform.Find(blockerName);
        GameObject blockerObject;

        if (child == null)
        {
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
        collider.center = Vector3.zero;
        collider.transform.rotation = Quaternion.identity;
        return collider;
    }

    private void ConfigureGateBlocker(BoxCollider collider, GateSegment segment, Room ownerRoom)
    {
        if (ownerRoom == null || ownerRoom.RoomBounds == null)
        {
            return;
        }

        Bounds ownerBounds = ownerRoom.RoomBounds.WorldBounds;
        float intervalCenter = (segment.intervalMin + segment.intervalMax) * 0.5f;
        float intervalLength = Mathf.Max(0f, segment.intervalMax - segment.intervalMin);

        Vector3 center;
        Vector3 size;

        switch (segment.side)
        {
            case RoomManager.RoomDirection.Left:
                center = new Vector3(ownerBounds.min.x - blockerThickness * 0.5f, intervalCenter, ownerBounds.center.z);
                size = new Vector3(blockerThickness, intervalLength, blockerDepth);
                break;
            case RoomManager.RoomDirection.Right:
                center = new Vector3(ownerBounds.max.x + blockerThickness * 0.5f, intervalCenter, ownerBounds.center.z);
                size = new Vector3(blockerThickness, intervalLength, blockerDepth);
                break;
            case RoomManager.RoomDirection.Up:
                center = new Vector3(intervalCenter, ownerBounds.max.y + blockerThickness * 0.5f, ownerBounds.center.z);
                size = new Vector3(intervalLength, blockerThickness, blockerDepth);
                break;
            case RoomManager.RoomDirection.Down:
                center = new Vector3(intervalCenter, ownerBounds.min.y - blockerThickness * 0.5f, ownerBounds.center.z);
                size = new Vector3(intervalLength, blockerThickness, blockerDepth);
                break;
            default:
                return;
        }

        collider.transform.position = center;
        collider.transform.rotation = Quaternion.identity;
        collider.size = size;
        collider.isTrigger = false;
        collider.enabled = false;
        collider.center = Vector3.zero;
    }

    private void DisableUnusedGateBlockers(HashSet<string> usedGateBlockerNames)
    {
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (!child.name.StartsWith("GateBlocker_"))
            {
                continue;
            }

            if (usedGateBlockerNames.Contains(child.name))
            {
                continue;
            }

            BoxCollider collider = child.GetComponent<BoxCollider>();
            if (collider != null)
            {
                collider.enabled = false;
                collider.isTrigger = false;
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
            reverseGateIndex = -1,
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
            reverseGateIndex = -1,
        });
    }

    internal bool TryFindGate(
        RoomManager.RoomDirection side,
        Vector3 worldPosition,
        out GateHandle gate,
        out Room targetRoom,
        out bool blockedByAmbiguous,
        out bool blockedByOneWay)
    {
        gate = default;
        targetRoom = null;
        blockedByAmbiguous = false;
        blockedByOneWay = false;

        float axisPosition = side == RoomManager.RoomDirection.Left || side == RoomManager.RoomDirection.Right
            ? worldPosition.y
            : worldPosition.x;

        int matchedCount = 0;
        GateSegment matchedSegment = default;
        int matchedIndex = -1;

        for (int i = 0; i < gateSegments.Count; i++)
        {
            GateSegment segment = gateSegments[i];
            if (segment.side != side)
            {
                continue;
            }

            if (axisPosition < segment.intervalMin || axisPosition > segment.intervalMax)
            {
                continue;
            }

            matchedCount++;
            matchedSegment = segment;
            matchedIndex = i;

            if (segment.isAmbiguous)
            {
                blockedByAmbiguous = true;
            }
        }

        if (matchedCount == 0)
        {
            return false;
        }

        if (matchedCount > 1 || blockedByAmbiguous)
        {
            blockedByAmbiguous = true;
            Debug.LogWarning($"RoomBlockerSet: side={side} axis={axisPosition:F2} に対する Gate が曖昧です。matchCount={matchedCount}", this);
            return false;
        }

        if (matchedSegment.isBlocked)
        {
            blockedByOneWay = true;
            return false;
        }

        gate = new GateHandle(this, matchedIndex);
        targetRoom = matchedSegment.targetRoom;
        return targetRoom != null;
    }

    internal bool TryGetReverseGate(GateHandle gate, out GateHandle reverseGate)
    {
        reverseGate = default;
        if (gate.ownerSet == null || !gate.ownerSet.IsValidGateHandle(gate))
        {
            return false;
        }

        GateSegment segment = gate.ownerSet.gateSegments[gate.index];
        RoomBlockerSet reverseOwnerSet = GetBlockerSetForRoom(segment.targetRoom);
        if (reverseOwnerSet == null)
        {
            return false;
        }

        if (segment.reverseGateIndex < 0 || segment.reverseGateIndex >= reverseOwnerSet.gateSegments.Count)
        {
            return false;
        }

        reverseGate = new GateHandle(reverseOwnerSet, segment.reverseGateIndex);
        return true;
    }

    internal void SetGateBlocked(GateHandle gate, bool blocked)
    {
        if (!IsValidGateHandle(gate))
        {
            return;
        }

        GateSegment segment = gateSegments[gate.index];
        if (segment.isAmbiguous && blocked)
        {
            Debug.LogWarning($"RoomBlockerSet: Ambiguous gate '{segment.debugName}' は blocked にできません。", this);
            return;
        }

        segment.isBlocked = blocked;
        if (segment.blockerCollider != null)
        {
            segment.blockerCollider.isTrigger = false;
            segment.blockerCollider.enabled = blocked;
        }

        gateSegments[gate.index] = segment;
        if (blocked && enableGateDebugLog)
        {
            Debug.Log($"RoomBlockerSet: Gate blocked '{segment.debugName}'", this);
        }
    }

    internal bool IsGateBlocked(GateHandle gate)
    {
        return IsValidGateHandle(gate) && gateSegments[gate.index].isBlocked;
    }

    internal Room GetGateTargetRoom(GateHandle gate)
    {
        return IsValidGateHandle(gate) ? gateSegments[gate.index].targetRoom : null;
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

    private void BuildReverseGateLinks()
    {
        for (int i = 0; i < gateSegments.Count; i++)
        {
            GateSegment resetSegment = gateSegments[i];
            resetSegment.reverseGateIndex = -1;
            gateSegments[i] = resetSegment;
        }

        const float intervalTolerance = 0.01f;
        for (int i = 0; i < gateSegments.Count; i++)
        {
            GateSegment segment = gateSegments[i];
            if (segment.isAmbiguous)
            {
                continue;
            }

            RoomBlockerSet reverseOwnerSet = GetBlockerSetForRoom(segment.targetRoom);
            if (reverseOwnerSet == null)
            {
                Debug.LogWarning($"RoomBlockerSet: ReverseGate owner set が見つかりません。gate='{segment.debugName}'", this);
                continue;
            }

            int matchedIndex = -1;
            for (int j = 0; j < reverseOwnerSet.gateSegments.Count; j++)
            {
                GateSegment other = reverseOwnerSet.gateSegments[j];
                if (other.isAmbiguous)
                {
                    continue;
                }

                bool isReversePair =
                    segment.ownerRoom == other.targetRoom &&
                    segment.targetRoom == other.ownerRoom &&
                    segment.side == GetOppositeSide(other.side) &&
                    Mathf.Abs(segment.intervalMin - other.intervalMin) <= intervalTolerance &&
                    Mathf.Abs(segment.intervalMax - other.intervalMax) <= intervalTolerance;

                if (!isReversePair)
                {
                    continue;
                }

                if (matchedIndex != -1)
                {
                    matchedIndex = -1;
                    break;
                }

                matchedIndex = j;
            }

            if (matchedIndex == -1)
            {
                Debug.LogWarning($"RoomBlockerSet: ReverseGate が見つかりません。gate='{segment.debugName}'", this);
                continue;
            }

            segment.reverseGateIndex = matchedIndex;
            gateSegments[i] = segment;
        }
    }

    private static RoomManager.RoomDirection GetOppositeSide(RoomManager.RoomDirection side)
    {
        return side switch
        {
            RoomManager.RoomDirection.Left => RoomManager.RoomDirection.Right,
            RoomManager.RoomDirection.Right => RoomManager.RoomDirection.Left,
            RoomManager.RoomDirection.Up => RoomManager.RoomDirection.Down,
            RoomManager.RoomDirection.Down => RoomManager.RoomDirection.Up,
            _ => RoomManager.RoomDirection.None,
        };
    }

    private bool IsValidGateHandle(GateHandle gate)
    {
        return gate.IsValid && gate.ownerSet == this && gate.index < gateSegments.Count;
    }

    private static RoomBlockerSet GetBlockerSetForRoom(Room room)
    {
        if (room == null)
        {
            return null;
        }

        return room.GetComponentInChildren<RoomBlockerSet>(true);
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