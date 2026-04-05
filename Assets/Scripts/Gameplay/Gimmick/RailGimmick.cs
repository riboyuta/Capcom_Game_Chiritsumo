using UnityEngine;
using System.Collections.Generic;

// レールを構成する各セグメントの当たり判定につけるヘルパー
[RequireComponent(typeof(CapsuleCollider))]
public class RailTriggerNode : MonoBehaviour
{
    public RailGimmick rail;
    public int segmentIndex;

    private void OnTriggerEnter(Collider other)
    {
        if (rail == null) return;

        var player = other.GetComponent<PlayerController>();
        if (player != null)
        {
            rail.OnPlayerEnterRail(player, segmentIndex);
        }
    }
}

[RequireComponent(typeof(LineRenderer))]
public class RailGimmick : MonoBehaviour
{
    [Header("Rail Waypoints (Local Position)")]
    [Tooltip("レールの節点（このオブジェクトからの相対座標）。エディタのSceneビューで編集できます。")]
    public List<Vector3> localWaypoints = new List<Vector3>() { Vector3.zero, new Vector3(3f, 0f, 0f) };

    [Header("Spline Resolution")]
    [Tooltip("節点間の分割数。大きいほど滑らかな曲線になります。")]
    [Range(2, 20)] public int resolution = 10;
    


    [Header("Visual")]
    [Tooltip("線の幅")]
    public float lineWidth = 0.5f;

    [Header("Trigger Size")]
    [Tooltip("吸着判定の太さ")]
    public float triggerRadius = 0.8f;

    // 生成されたスプライン上の全座標（ワールド座標）
    private List<Vector3> railPath = new List<Vector3>();

    // 外部から補間ポイント群を参照するためのプロパティ
    public IReadOnlyList<Vector3> RailPath => railPath;

    private void Awake()
    {
        GenerateSpline();
        GenerateVisual();
        GenerateColliders();
    }

    public List<Vector3> GetWorldSplinePath()
    {
        if (localWaypoints == null || localWaypoints.Count < 2) return new List<Vector3>();

        List<Vector3> path = new List<Vector3>();
        int pointCount = localWaypoints.Count;

        Vector3[] wpWorld = new Vector3[pointCount];
        for (int i = 0; i < pointCount; i++) 
        {
            wpWorld[i] = transform.TransformPoint(localWaypoints[i]);
        }

        // 通常のパス（α型のクロスや一本線）の生成
        Vector3[] p = new Vector3[pointCount + 2];
        for (int i = 0; i < pointCount; i++) p[i + 1] = wpWorld[i];
        
        p[0] = p[1] + (p[1] - p[2]);
        p[p.Length - 1] = p[p.Length - 2] + (p[p.Length - 2] - p[p.Length - 3]);

        for (int i = 0; i < pointCount - 1; i++)
        {
            Vector3 p0 = p[i];
            Vector3 p1 = p[i + 1];
            Vector3 p2 = p[i + 2];
            Vector3 p3 = p[i + 3];

            for (int j = 0; j < resolution; j++)
            {
                float t = j / (float)resolution;
                path.Add(GetCatmullRomPosition(t, p0, p1, p2, p3));
            }
        }
        path.Add(p[p.Length - 2]);
        
        return path;
    }

    private void GenerateSpline()
    {
        railPath = GetWorldSplinePath();
    }

    private void GenerateVisual()
    {
        if (railPath.Count < 2) return;
        var lr = GetComponent<LineRenderer>();
        lr.positionCount = railPath.Count;
        lr.SetPositions(railPath.ToArray());
        lr.startWidth = lineWidth;
        lr.endWidth = lineWidth;
        lr.useWorldSpace = true;
    }

    private void GenerateColliders()
    {
        if (railPath.Count < 2) return;

        for (int i = 0; i < railPath.Count - 1; i++)
        {
            Vector3 startP = railPath[i];
            Vector3 endP = railPath[i + 1];
            Vector3 center = (startP + endP) * 0.5f;
            float length = Vector3.Distance(startP, endP);

            GameObject child = new GameObject($"RailSegment_{i}");
            child.transform.parent = this.transform;
            child.transform.position = center;
            if (length > 0.001f)
            {
                child.transform.LookAt(endP);
            }

            CapsuleCollider col = child.AddComponent<CapsuleCollider>();
            col.isTrigger = true;
            col.radius = triggerRadius;
            col.direction = 2; // 0:X, 1:Y, 2:Z
            col.height = length + triggerRadius * 2f; 

            var triggerNode = child.AddComponent<RailTriggerNode>();
            triggerNode.rail = this;
            triggerNode.segmentIndex = i;

            child.layer = gameObject.layer;
        }
    }

    private Vector3 GetCatmullRomPosition(float t, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
    {
        Vector3 a = 2f * p1;
        Vector3 b = p2 - p0;
        Vector3 c = 2f * p0 - 5f * p1 + 4f * p2 - p3;
        Vector3 d = -p0 + 3f * p1 - 3f * p2 + p3;

        return 0.5f * (a + (b * t) + (c * t * t) + (d * t * t * t));
    }

    public void OnPlayerEnterRail(PlayerController player, int segmentIndex)
    {
        Vector3 startP = railPath[segmentIndex];
        Vector3 endP = railPath[segmentIndex + 1];
        Vector3 segDir = (endP - startP).normalized;

        // 【新機能】レール乗車に対する傾き制限
        // PlayerMovementSettingsの乗車可能最大角度 (例: 45度) をチェックする。
        // 空中から直接垂直の壁に乗れないようにするための制御。
        float slopeDot = Mathf.Abs(Vector3.Dot(segDir, Vector3.up)); // Sin(傾斜角)
        float limitDot = Mathf.Sin(player.MovementSettings.maxAttachSlopeAngle * Mathf.Deg2Rad);
        
        if (slopeDot > limitDot)
        {
            // 垂直よりすぎるので弾く
            return;
        }

        Vector3 toPlayer = player.transform.position - startP;
        float distanceOnSegment = Vector3.Dot(toPlayer, segDir);
        distanceOnSegment = Mathf.Clamp(distanceOnSegment, 0f, Vector3.Distance(startP, endP));

        float dotVelocity = Vector3.Dot(player.GetComponent<Rigidbody>().linearVelocity.normalized, segDir);
        int direction = dotVelocity >= 0 ? 1 : -1;

        player.StartGrind(this, segmentIndex, distanceOnSegment, direction);
    }
    
#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (Application.isPlaying) return;
        if (localWaypoints == null || localWaypoints.Count < 2) return;

        var path = GetWorldSplinePath();
        if (path.Count < 2) return;

        Gizmos.color = Color.cyan;
        for (int i = 0; i < path.Count - 1; i++)
        {
            Gizmos.DrawLine(path[i], path[i+1]);
        }
    }
#endif
}
