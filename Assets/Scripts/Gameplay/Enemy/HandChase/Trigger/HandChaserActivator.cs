using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public sealed class HandChaserActivator : MonoBehaviour, IRespawnResettable
{

    [Header("起動対象の敵")]
    [Tooltip("起動対象の HandChaserEnemy です。")]
    [SerializeField] private HandChaserEnemy targetEnemy;

    [Header("ゲーム進行")]
    [Tooltip("初回有効発動時に経過時間計測の開始通知を送る GameRoot です。")]
    [SerializeField] private GameRoot gameRoot;

    [Header("判定設定")]
    [Tooltip("プレイヤーとして判定するタグ名です。")]
    [SerializeField] private string playerTag = "Player";

    [Header("セーフゾーン設定")]
    [Tooltip("プレイヤーがこのセーフゾーンから出てから、敵がスポーンするまでの遅延時間です。0なら出た瞬間にスポーンします。")]
    [SerializeField] private float spawnDelay = 0.0f;

    [Tooltip("このセーフゾーンが属する Room です。未設定時は親階層から自動検索します。")]
    [SerializeField] private Room parentRoom;

    [Header("セーフゾーン視覚化")]
    [Tooltip("セーフゾーンをワイヤーフレームで視覚化します。")]
    [SerializeField] private bool visualizeSafeZone = true;

    [Tooltip("ワイヤーフレームの表示色です。")]
    [SerializeField] private Color safeZoneColor = new Color(0f, 1f, 0f, 0.8f);

    [Header("出現予告UI")]
    [Tooltip("HandEnemy 出現前に画面端へ表示する警告UIです。")]
    [SerializeField] private HandEnemySpawnWarningView spawnWarningView;

    [Tooltip("出現方向の判定に使う HandChaserMovement です。未設定時は targetEnemy から自動取得します。")]
    [SerializeField] private HandChaserMovement targetMovement;

    [Tooltip("出現予告UIを使うかどうかです。")]
    [SerializeField] private bool useSpawnWarning = true;

    private Material lineMaterial;
    private Collider safeZoneCollider;
    private RoomManager roomManager;
    private Coroutine spawnCoroutine;

    private bool isRoomActive;
    private bool hasStartedSpawn;

    private bool hasCapturedInitialState;
    private bool initialEnabled;
    private bool initialColliderEnabled;
    private bool initialHasStartedSpawn;

    private bool isPlayerInsideSafeZone;

    private const float AxisThreshold = 0.0001f;
    private const int WireframeSegments = 24;
    private const int CapsuleEdgeCount = 4;

    private void Awake()
    {
        EnsureSafeZoneCollider();

        if (parentRoom == null)
        {
            parentRoom = GetComponentInParent<Room>();
        }

        roomManager = FindFirstObjectByType<RoomManager>();

        if (targetMovement == null && targetEnemy != null)
        {
            targetMovement = targetEnemy.GetComponent<HandChaserMovement>();
        }

        if (visualizeSafeZone)
        {
            CreateLineMaterial();
        }
    }

    private void Start()
    {
        if (roomManager != null)
        {
            roomManager.OnRoomTransitionComplete += OnRoomTransitionComplete;
        }

        RefreshRoomActiveState();
    }

    private void OnDestroy()
    {
        if (roomManager != null)
        {
            roomManager.OnRoomTransitionComplete -= OnRoomTransitionComplete;
        }

        if (lineMaterial != null)
        {
            if (Application.isPlaying)
            {
                Destroy(lineMaterial);
            }
            else
            {
                DestroyImmediate(lineMaterial);
            }
        }
    }

    private void OnRoomTransitionComplete(Room newRoom)
    {
        if (parentRoom == null)
        {
            isRoomActive = true;
            return;
        }

        isRoomActive = newRoom == parentRoom;

        if (isRoomActive)
        {
            StopSpawnCoroutine();
            hasStartedSpawn = false;
            isPlayerInsideSafeZone = false;
        }
        else
        {
            StopSpawnCoroutine();
            StopSpawnWarning();
            isPlayerInsideSafeZone = false;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        TryHandlePlayerInsideSafeZone(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TryHandlePlayerInsideSafeZone(other);
    }

    private void TryHandlePlayerInsideSafeZone(Collider other)
    {
        if (!CanHandlePlayerTrigger(other))
        {
            return;
        }

        isPlayerInsideSafeZone = true;
        StartSpawnWarningIfNeeded();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!CanHandlePlayerTrigger(other) || targetEnemy == null)
        {
            return;
        }

        isPlayerInsideSafeZone = false;
        StopSpawnWarning();

        StartSpawnAfterSafeZoneExit();
    }

    private void StartSpawnAfterSafeZoneExit()
    {
        hasStartedSpawn = true;

        if (gameRoot != null)
        {
            gameRoot.StartElapsedTimeIfNeeded();
        }

        if (spawnDelay > 0f)
        {
            StopSpawnCoroutine();
            spawnCoroutine = StartCoroutine(DelayedSpawnCoroutine());
        }
        else
        {
            SpawnEnemy();
        }
    }

    private IEnumerator DelayedSpawnCoroutine()
    {
        yield return new WaitForSeconds(spawnDelay);

        SpawnEnemy();

        spawnCoroutine = null;
    }

    private SpawnWarningScreenEdge ResolveSpawnWarningEdge()
    {
        if (targetMovement == null)
        {
            return SpawnWarningScreenEdge.Left;
        }

        switch (targetMovement.Direction)
        {
            case MoveDirection.Right:
                return SpawnWarningScreenEdge.Left;

            case MoveDirection.Left:
                return SpawnWarningScreenEdge.Right;

            case MoveDirection.Up:
                return SpawnWarningScreenEdge.Bottom;

            case MoveDirection.Down:
                return SpawnWarningScreenEdge.Top;

            case MoveDirection.Custom:
                return ResolveSpawnWarningEdgeFromAxis(targetMovement.CustomMoveAxis);

            default:
                return SpawnWarningScreenEdge.Left;
        }
    }

    private SpawnWarningScreenEdge ResolveSpawnWarningEdgeFromAxis(Vector3 axis)
    {
        if (axis.sqrMagnitude <= AxisThreshold)
        {
            return SpawnWarningScreenEdge.Left;
        }

        Vector3 normalizedAxis = axis.normalized;

        if (Mathf.Abs(normalizedAxis.x) >= Mathf.Abs(normalizedAxis.y))
        {
            return normalizedAxis.x >= 0f
                ? SpawnWarningScreenEdge.Left
                : SpawnWarningScreenEdge.Right;
        }

        return normalizedAxis.y >= 0f
            ? SpawnWarningScreenEdge.Bottom
            : SpawnWarningScreenEdge.Top;
    }

    private void SpawnEnemy()
    {
        if (targetEnemy == null)
        {
            return;
        }

        targetEnemy.BeginChase();
    }

    private void StartSpawnWarningIfNeeded()
    {
        if (!useSpawnWarning || spawnWarningView == null)
        {
            return;
        }

        if (spawnWarningView.IsPlaying)
        {
            return;
        }

        SpawnWarningScreenEdge edge = ResolveSpawnWarningEdge();
        spawnWarningView.PlayLoop(edge);
    }

    private void StopSpawnWarning()
    {
        if (spawnWarningView == null)
        {
            return;
        }

        spawnWarningView.StopAndHide();
    }

    public void CaptureInitialState()
    {
        if (hasCapturedInitialState)
        {
            return;
        }

        EnsureSafeZoneCollider();

        initialEnabled = enabled;
        initialColliderEnabled = safeZoneCollider != null && safeZoneCollider.enabled;
        initialHasStartedSpawn = hasStartedSpawn;

        hasCapturedInitialState = true;
    }

    public void ResetToRespawnState()
    {
        EnsureSafeZoneCollider();

        StopSpawnCoroutine();
        StopSpawnWarning();
        isPlayerInsideSafeZone = false;

        if (hasCapturedInitialState)
        {
            RestoreCapturedState();
        }
        else
        {
            RestoreDefaultState();
        }

        if (targetEnemy != null)
        {
            targetEnemy.ResetEncounterForRespawn();
        }

        RefreshRoomActiveState();
    }

    private void RefreshRoomActiveState()
    {
        if (roomManager == null || parentRoom == null)
        {
            isRoomActive = true;
            return;
        }

        isRoomActive = roomManager.CurrentRoom == parentRoom && !roomManager.IsTransitioning;
    }

    private void StopSpawnCoroutine()
    {
        bool hadSpawnCoroutine = spawnCoroutine != null;

        if (spawnCoroutine != null)
        {
            StopCoroutine(spawnCoroutine);
            spawnCoroutine = null;
        }

        if (hadSpawnCoroutine && spawnWarningView != null)
        {
            spawnWarningView.StopAndHide();
        }
    }

    private void OnDisable()
    {
        StopSpawnCoroutine();
        StopSpawnWarning();
    }

    private void CreateLineMaterial()
    {
        Shader shader = Shader.Find("Hidden/Internal-Colored");
        lineMaterial = new Material(shader);
        lineMaterial.hideFlags = HideFlags.HideAndDontSave;
        lineMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        lineMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        lineMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
        lineMaterial.SetInt("_ZWrite", 0);
    }

    private void OnRenderObject()
    {
        if (!visualizeSafeZone || lineMaterial == null || safeZoneCollider == null)
        {
            return;
        }

        lineMaterial.SetPass(0);

        GL.PushMatrix();
        GL.MultMatrix(transform.localToWorldMatrix);
        GL.Begin(GL.LINES);
        GL.Color(safeZoneColor);

        if (safeZoneCollider is BoxCollider boxCollider)
        {
            DrawBoxColliderWireframe(boxCollider);
        }
        else if (safeZoneCollider is SphereCollider sphereCollider)
        {
            DrawSphereColliderWireframe(sphereCollider);
        }
        else if (safeZoneCollider is CapsuleCollider capsuleCollider)
        {
            DrawCapsuleColliderWireframe(capsuleCollider);
        }

        GL.End();
        GL.PopMatrix();
    }

    private void DrawBoxColliderWireframe(BoxCollider boxCollider)
    {
        Vector3 center = boxCollider.center;
        Vector3 size = boxCollider.size;
        Vector3 halfSize = size * 0.5f;

        // 8つの頂点を計算
        Vector3 v0 = center + new Vector3(-halfSize.x, -halfSize.y, -halfSize.z);
        Vector3 v1 = center + new Vector3(halfSize.x, -halfSize.y, -halfSize.z);
        Vector3 v2 = center + new Vector3(halfSize.x, halfSize.y, -halfSize.z);
        Vector3 v3 = center + new Vector3(-halfSize.x, halfSize.y, -halfSize.z);
        Vector3 v4 = center + new Vector3(-halfSize.x, -halfSize.y, halfSize.z);
        Vector3 v5 = center + new Vector3(halfSize.x, -halfSize.y, halfSize.z);
        Vector3 v6 = center + new Vector3(halfSize.x, halfSize.y, halfSize.z);
        Vector3 v7 = center + new Vector3(-halfSize.x, halfSize.y, halfSize.z);

        // 底面
        GL.Vertex(v0); GL.Vertex(v1);
        GL.Vertex(v1); GL.Vertex(v5);
        GL.Vertex(v5); GL.Vertex(v4);
        GL.Vertex(v4); GL.Vertex(v0);

        // 上面
        GL.Vertex(v3); GL.Vertex(v2);
        GL.Vertex(v2); GL.Vertex(v6);
        GL.Vertex(v6); GL.Vertex(v7);
        GL.Vertex(v7); GL.Vertex(v3);

        // 縦のエッジ
        GL.Vertex(v0); GL.Vertex(v3);
        GL.Vertex(v1); GL.Vertex(v2);
        GL.Vertex(v5); GL.Vertex(v6);
        GL.Vertex(v4); GL.Vertex(v7);
    }

    private void DrawSphereColliderWireframe(SphereCollider sphereCollider)
    {
        Vector3 center = sphereCollider.center;
        float radius = sphereCollider.radius;

        // XY平面の円
        for (int i = 0; i < WireframeSegments; i++)
        {
            float angle1 = (i / (float)WireframeSegments) * Mathf.PI * 2f;
            float angle2 = ((i + 1) / (float)WireframeSegments) * Mathf.PI * 2f;

            Vector3 p1 = center + new Vector3(Mathf.Cos(angle1) * radius, Mathf.Sin(angle1) * radius, 0f);
            Vector3 p2 = center + new Vector3(Mathf.Cos(angle2) * radius, Mathf.Sin(angle2) * radius, 0f);

            GL.Vertex(p1);
            GL.Vertex(p2);
        }

        // XZ平面の円
        for (int i = 0; i < WireframeSegments; i++)
        {
            float angle1 = (i / (float)WireframeSegments) * Mathf.PI * 2f;
            float angle2 = ((i + 1) / (float)WireframeSegments) * Mathf.PI * 2f;

            Vector3 p1 = center + new Vector3(Mathf.Cos(angle1) * radius, 0f, Mathf.Sin(angle1) * radius);
            Vector3 p2 = center + new Vector3(Mathf.Cos(angle2) * radius, 0f, Mathf.Sin(angle2) * radius);

            GL.Vertex(p1);
            GL.Vertex(p2);
        }

        // YZ平面の円
        for (int i = 0; i < WireframeSegments; i++)
        {
            float angle1 = (i / (float)WireframeSegments) * Mathf.PI * 2f;
            float angle2 = ((i + 1) / (float)WireframeSegments) * Mathf.PI * 2f;

            Vector3 p1 = center + new Vector3(0f, Mathf.Cos(angle1) * radius, Mathf.Sin(angle1) * radius);
            Vector3 p2 = center + new Vector3(0f, Mathf.Cos(angle2) * radius, Mathf.Sin(angle2) * radius);

            GL.Vertex(p1);
            GL.Vertex(p2);
        }
    }

    private void DrawCapsuleColliderWireframe(CapsuleCollider capsuleCollider)
    {
        Vector3 center = capsuleCollider.center;
        float radius = capsuleCollider.radius;
        float height = capsuleCollider.height;
        int direction = capsuleCollider.direction; // 0:X, 1:Y, 2:Z

        float cylinderHeight = Mathf.Max(0f, height - radius * 2f);
        Vector3 offset = Vector3.zero;

        switch (direction)
        {
            case 0: offset = new Vector3(cylinderHeight * 0.5f, 0f, 0f); break;
            case 1: offset = new Vector3(0f, cylinderHeight * 0.5f, 0f); break;
            case 2: offset = new Vector3(0f, 0f, cylinderHeight * 0.5f); break;
        }

        Vector3 top = center + offset;
        Vector3 bottom = center - offset;

        // 円周の描画（上下）
        for (int i = 0; i < WireframeSegments; i++)
        {
            float angle1 = (i / (float)WireframeSegments) * Mathf.PI * 2f;
            float angle2 = ((i + 1) / (float)WireframeSegments) * Mathf.PI * 2f;

            Vector3 p1, p2;

            if (direction == 0) // X軸
            {
                p1 = new Vector3(0f, Mathf.Cos(angle1) * radius, Mathf.Sin(angle1) * radius);
                p2 = new Vector3(0f, Mathf.Cos(angle2) * radius, Mathf.Sin(angle2) * radius);
            }
            else if (direction == 1) // Y軸
            {
                p1 = new Vector3(Mathf.Cos(angle1) * radius, 0f, Mathf.Sin(angle1) * radius);
                p2 = new Vector3(Mathf.Cos(angle2) * radius, 0f, Mathf.Sin(angle2) * radius);
            }
            else // Z軸
            {
                p1 = new Vector3(Mathf.Cos(angle1) * radius, Mathf.Sin(angle1) * radius, 0f);
                p2 = new Vector3(Mathf.Cos(angle2) * radius, Mathf.Sin(angle2) * radius, 0f);
            }

            GL.Vertex(top + p1);
            GL.Vertex(top + p2);
            GL.Vertex(bottom + p1);
            GL.Vertex(bottom + p2);
        }

        // 縦のエッジ（4本）
        for (int i = 0; i < CapsuleEdgeCount; i++)
        {
            float angle = (i / (float)CapsuleEdgeCount) * Mathf.PI * 2f;
            Vector3 edgeOffset;

            if (direction == 0) // X軸
            {
                edgeOffset = new Vector3(0f, Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
            }
            else if (direction == 1) // Y軸
            {
                edgeOffset = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            }
            else // Z軸
            {
                edgeOffset = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
            }

            GL.Vertex(top + edgeOffset);
            GL.Vertex(bottom + edgeOffset);
        }
    }

    private void EnsureSafeZoneCollider()
    {
        if (safeZoneCollider == null)
        {
            safeZoneCollider = GetComponent<Collider>();
        }

        if (safeZoneCollider != null)
        {
            safeZoneCollider.isTrigger = true;
        }
    }

    private bool CanHandlePlayerTrigger(Collider other)
    {
        return isRoomActive
            && !(roomManager != null && roomManager.IsTransitioning)
            && !hasStartedSpawn
            && other.CompareTag(playerTag);
    }

    private void RestoreCapturedState()
    {
        enabled = initialEnabled;
        hasStartedSpawn = initialHasStartedSpawn;

        if (safeZoneCollider != null)
        {
            safeZoneCollider.enabled = initialColliderEnabled;
            safeZoneCollider.isTrigger = true;
        }
    }

    private void RestoreDefaultState()
    {
        enabled = true;
        hasStartedSpawn = false;

        if (safeZoneCollider != null)
        {
            safeZoneCollider.enabled = true;
            safeZoneCollider.isTrigger = true;
        }
    }
}