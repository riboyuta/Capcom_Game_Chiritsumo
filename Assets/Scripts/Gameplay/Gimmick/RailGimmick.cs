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

        var playerFacade = other.GetComponentInParent<PlayerFacade>();
        if (playerFacade != null)
        {

            rail.OnPlayerEnterRail(playerFacade, segmentIndex);
        }
    }
}

[RequireComponent(typeof(LineRenderer))]
public class RailGimmick : MonoBehaviour, IRespawnResettable
{
    [Header("Rail Waypoints (Local Position)")]
    [Tooltip("レールの節点（このオブジェクトからの相対座標）。エディタのSceneビューで編集できます。")]
    public List<Vector3> localWaypoints = new List<Vector3>() { Vector3.zero, new Vector3(3f, 0f, 0f) };

    [Header("Spline Resolution")]
    [Tooltip("節点間の分割数。大きいほど滑らかな曲線になります。")]
    [Range(2, 20)] public int resolution = 10;

    [Header("レール挙動設定")]
    [Tooltip("レール滑走とレールジャンプ関連の設定です。")]
    [Min(0f)]
    [SerializeField] private float grindSpeed = 15f;

    [Tooltip("レールジャンプ時に与える上方向速度です。")]
    [SerializeField] private float grindJumpVerticalVelocity = 12f;

    [Tooltip("レール離脱後に再搭乗できるまでのロック時間です。")]
    [Min(0f)]
    [SerializeField] private float reattachLockTime = 0.2f;

    [Tooltip("レール搭乗を許可する最大傾斜角度です。")]
    [Range(0f, 90f)]
    [SerializeField] private float maxAttachSlopeAngle = 45f;


    [Header("Visual")]
    [Tooltip("線の幅")]
    public float lineWidth = 0.5f;

    [Header("Trigger Size")]
    [Tooltip("吸着判定の太さ")]
    public float triggerRadius = 0.8f;

    // 生成されたスプライン上の全座標（ワールド座標）
    private List<Vector3> railPath = new List<Vector3>();
    // 現在このレールで保持中の外部制御セッション。
    private PlayerExternalControlSession activeSession = PlayerExternalControlSession.Invalid;

    // 現在乗車中のプレイヤー窓口。
    private PlayerFacade activePlayerFacade;

    // 現在乗車中のプレイヤー Rigidbody。
    private Rigidbody activePlayerRigidbody;

    // 現在乗車中プレイヤーのカプセル。
    private CapsuleCollider activePlayerCapsule;

    // レール上の現在セグメント。
    private int activeSegmentIndex;

    // 現在セグメント上の進行距離。
    private float distanceOnSegment;

    // 進行方向 (+1 / -1)。
    private int grindDirection = 1;

    // レール離脱後の再搭乗クールダウンタイマー。
    private float reattachCooldownTimer;

    // 初期状態を一度だけ保存したかどうか。
    private bool hasCapturedInitialState;

    // 初期位置。
    private Vector3 initialPosition;

    // 初期回転。
    private Quaternion initialRotation;

    // 初期クールダウン値。
    private float initialReattachCooldownTimer;

    // 外部から補間ポイント群を参照するためのプロパティ
    public IReadOnlyList<Vector3> RailPath => railPath;

    private void Awake()
    {

        GenerateSpline();
        GenerateVisual();
        GenerateColliders();
    }

    private void FixedUpdate()
    {
        if (reattachCooldownTimer > 0f)
        {
            reattachCooldownTimer = Mathf.Max(0f, reattachCooldownTimer - Time.fixedDeltaTime);
        }

        if (!activeSession.IsValid)
        {
            ClearRideState();
            return;
        }

        UpdateActiveRide(Time.fixedDeltaTime);
    }

    private void OnDisable()
    {
        // 無効化時は外部制御を閉じて搭乗状態をクリアします。
        EndActiveRideSessionIfNeeded();
        ClearRideState();
    }

    public void CaptureInitialState()
    {
        // 初期状態は一度だけ保存し、搭乗中の途中状態では保存しません。
        if (hasCapturedInitialState)
        {
            return;
        }

        if (activeSession.IsValid || activePlayerFacade != null || activePlayerRigidbody != null)
        {
            return;
        }

        initialPosition = transform.position;
        initialRotation = transform.rotation;
        initialReattachCooldownTimer = reattachCooldownTimer;
        hasCapturedInitialState = true;
    }

    public void ResetToRespawnState()
    {
        // 初期状態が未保存なら可能な範囲で保存してからリセットします。
        if (!hasCapturedInitialState)
        {
            CaptureInitialState();
        }

        EndActiveRideSessionIfNeeded();
        RestoreActivePlayerPhysicsIfNeeded();
        ClearRideRuntimeState();
        RestoreInitialTransformAndCooldown();
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

    public void OnPlayerEnterRail(PlayerFacade facade, int segmentIndex)
    {

        if (facade == null || railPath.Count < 2)
        {
            return;
        }

        if (activeSession.IsValid)
        {
            return;
        }

        if (segmentIndex < 0 || segmentIndex >= railPath.Count - 1)
        {
            return;
        }

        if (IsReattachCooldownActive())
        {
            return;
        }

        Transform playerTransform = facade.transform;

        Vector3 startP = railPath[segmentIndex];
        Vector3 endP = railPath[segmentIndex + 1];
        Vector3 segDir = (endP - startP).normalized;

        // レール乗車に対する傾き制限。
        float slopeDot = Mathf.Abs(Vector3.Dot(segDir, Vector3.up));
        float limitDot = Mathf.Sin(maxAttachSlopeAngle * Mathf.Deg2Rad);

        if (slopeDot > limitDot)
        {
            // 垂直よりすぎるので弾く
            return;
        }

        Vector3 toPlayer = playerTransform.position - startP;
        float initialDistance = Mathf.Clamp(Vector3.Dot(toPlayer, segDir), 0f, Vector3.Distance(startP, endP));

        Rigidbody rb = facade.GetComponent<Rigidbody>();
        if (rb == null)
        {
            return;
        }

        float dotVelocity = Vector3.Dot(rb.linearVelocity.normalized, segDir);
        int initialDirection = dotVelocity >= 0 ? 1 : -1;

        PlayerExternalControlRequest request = new PlayerExternalControlRequest
        {
            Owner = this,
            Mode = ExternalControlMode.PathDriven,
            InputBlockFlags = PlayerController.InputBlockFlags.Move | PlayerController.InputBlockFlags.Dash | PlayerController.InputBlockFlags.Grab,
            PhysicsPolicy = ExternalPhysicsPolicy.ExternalDriven,
            GravityPolicy = ExternalGravityPolicy.ForceOff,
            VisualPolicy = ExternalVisualPolicy.Keep
        };

        if (!facade.CanAcceptExternalControl(request))
        {
            return;
        }

        if (!facade.TryBeginExternalControl(request, out PlayerExternalControlSession session) || !session.IsValid)
        {
            return;
        }

        // ギミック接触によるダッシュ（ステップ）回復
        facade.TryRefillDash(DashRefillReason.Gimmick);

        activeSession = session;
        activePlayerFacade = facade;
        activePlayerRigidbody = rb;
        activePlayerCapsule = facade.GetComponent<CapsuleCollider>();
        activeSegmentIndex = segmentIndex;
        distanceOnSegment = initialDistance;
        grindDirection = initialDirection;

        // レール搭乗開始時は余分な慣性を切る。
        activePlayerRigidbody.linearVelocity = Vector3.zero;

        // 物理干渉を排除するため kinematic 化する。
        activePlayerRigidbody.isKinematic = true;
    }

    private void UpdateActiveRide(float deltaTime)
    {
        if (!activeSession.IsValid || activePlayerFacade == null)
        {
            ClearRideState();
            return;
        }

        if (activePlayerRigidbody == null || railPath.Count < 2)
        {
            ExitRideWithoutLaunch();
            return;
        }

        if (activeSession.ConsumeJumpRequestThisFrame())
        {
            Vector3 tangent = GetCurrentSegmentDirection();
            Vector3 jumpVelocity = tangent * grindSpeed
                + Vector3.up * grindJumpVerticalVelocity;
            ExitRideWithLaunch(jumpVelocity);
            return;
        }

        float moveDelta = grindSpeed * deltaTime;
        while (moveDelta > 0f)
        {
            Vector3 startP = railPath[activeSegmentIndex];
            Vector3 endP = railPath[activeSegmentIndex + 1];
            float segmentLength = Vector3.Distance(startP, endP);

            if (grindDirection > 0)
            {
                float remaining = segmentLength - distanceOnSegment;
                if (moveDelta <= remaining)
                {
                    distanceOnSegment += moveDelta;
                    moveDelta = 0f;
                }
                else
                {
                    moveDelta -= remaining;
                    activeSegmentIndex++;
                    distanceOnSegment = 0f;

                    if (activeSegmentIndex >= railPath.Count - 1)
                    {
                        Vector3 releaseVelocity = (endP - startP).normalized * grindSpeed;
                        ExitRideWithLaunch(releaseVelocity);
                        return;
                    }
                }
            }
            else
            {
                float remaining = distanceOnSegment;
                if (moveDelta <= remaining)
                {
                    distanceOnSegment -= moveDelta;
                    moveDelta = 0f;
                }
                else
                {
                    moveDelta -= remaining;
                    activeSegmentIndex--;

                    if (activeSegmentIndex < 0)
                    {
                        Vector3 releaseVelocity = (startP - endP).normalized * grindSpeed;
                        ExitRideWithLaunch(releaseVelocity);
                        return;
                    }

                    Vector3 nextStart = railPath[activeSegmentIndex];
                    Vector3 nextEnd = railPath[activeSegmentIndex + 1];
                    distanceOnSegment = Vector3.Distance(nextStart, nextEnd);
                }
            }
        }

        Vector3 finalStart = railPath[activeSegmentIndex];
        Vector3 finalEnd = railPath[activeSegmentIndex + 1];
        Vector3 segDir = (finalEnd - finalStart).normalized;
        Vector3 targetPos = finalStart + segDir * distanceOnSegment;
        targetPos += Vector3.up * GetFeetOffset();

        // kinematic の Rigidbody に直接位置を反映する。
        activePlayerRigidbody.MovePosition(targetPos);
        activeSession.RequestFacingThisFrame(segDir.x >= 0f ? 1 : -1);
    }

    private Vector3 GetCurrentSegmentDirection()
    {
        int segment = Mathf.Clamp(activeSegmentIndex, 0, railPath.Count - 2);
        Vector3 start = railPath[segment];
        Vector3 end = railPath[segment + 1];
        return (end - start).normalized * grindDirection;
    }

    private float GetFeetOffset()
    {
        if (activePlayerCapsule == null || activePlayerFacade == null)
        {
            return 0f;
        }
        float worldHalfHeight = (activePlayerCapsule.height * 0.5f) * Mathf.Abs(activePlayerFacade.transform.lossyScale.y);
        Vector3 centerOffset = activePlayerFacade.transform.TransformVector(activePlayerCapsule.center);
        return worldHalfHeight - centerOffset.y;
    }

    private void ExitRideWithLaunch(Vector3 releaseVelocity)
    {

        StartReattachCooldown(activePlayerFacade);

        if (activeSession.IsValid)
        {
            float speed = releaseVelocity.magnitude;
            Vector3 direction = speed > 0.0001f ? releaseVelocity / speed : Vector3.zero;
            activeSession.RequestLaunch(direction, speed, 0f, default);
        }

        ClearRideState();
    }

    private void ExitRideWithoutLaunch()
    {
        // ジャンプなし離脱時は制御を終了して搭乗状態をクリアします。
        EndActiveRideSessionIfNeeded();
        ClearRideState();
    }

    private void ClearRideState()
    {
        // 通常離脱時は物理を戻して搭乗状態を初期化します。
        RestoreActivePlayerPhysicsIfNeeded();
        ClearRideRuntimeState();
    }

    private void EndActiveRideSessionIfNeeded()
    {
        // 外部制御セッションが残っている場合だけ安全に終了します。
        if (!activeSession.IsValid)
        {
            return;
        }

        activeSession.EndControl();
    }

    private void RestoreActivePlayerPhysicsIfNeeded()
    {
        // レール搭乗中に kinematic 化した Rigidbody を通常状態へ戻します。
        if (activePlayerRigidbody == null)
        {
            return;
        }

        activePlayerRigidbody.isKinematic = false;
    }

    private void ClearRideRuntimeState()
    {
        // 搭乗中プレイヤー参照とレール進行状態を未搭乗の初期状態へ戻します。
        activeSession = PlayerExternalControlSession.Invalid;
        activePlayerFacade = null;
        activePlayerRigidbody = null;
        activePlayerCapsule = null;
        activeSegmentIndex = 0;
        distanceOnSegment = 0f;
        grindDirection = 1;
    }

    private void RestoreInitialTransformAndCooldown()
    {
        // レール本体の transform と再搭乗クールダウンを初期値へ戻します。
        if (hasCapturedInitialState)
        {
            transform.position = initialPosition;
            transform.rotation = initialRotation;
            reattachCooldownTimer = initialReattachCooldownTimer;
            return;
        }

        reattachCooldownTimer = 0f;
    }
    private bool IsReattachCooldownActive()
    {
        return reattachCooldownTimer > 0f;
    }

    private void StartReattachCooldown(PlayerFacade playerFacade)
    {
        if (playerFacade == null)
        {
            return;
        }

        float cooldown = reattachLockTime;
        reattachCooldownTimer = Mathf.Max(reattachCooldownTimer, cooldown);
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
