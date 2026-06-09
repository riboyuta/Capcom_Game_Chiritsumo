using System.Collections;
using UnityEngine;

// 各部屋のセーフゾーンからプレイヤーが出た時に ShadowChaserEnemy を有効化する。
// StageResetSystem からは IRespawnResettable 経由で未使用状態へ戻される。
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public sealed class ShadowChaserActivator : MonoBehaviour, IRespawnResettable
{
    private const int WireframeSegments = 24;
    private const int CapsuleEdgeCount  = 4;

    [Header("起動対象の敵")]
    [Tooltip("起動対象の ShadowChaserEnemy です。")]
    [SerializeField] private ShadowChaserEnemy targetEnemy;

    [Header("スポーン位置")]
    [Tooltip("セーフゾーン退出後に敵を出現させる位置です。未設定時はこの GameObject の Transform を使います。")]
    [SerializeField] private Transform spawnPoint;

    [Header("プレイヤータグ判定使用")]
    [Tooltip("Player タグで判定するかです。")]
    [SerializeField] private bool usePlayerTag = true;

    [Header("プレイヤータグ名")]
    [Tooltip("Player タグ判定に使うタグ名です。")]
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

    private void Awake()
    {
        safeZoneCollider = GetComponent<Collider>();
        safeZoneCollider.isTrigger = true;

        if (spawnPoint == null)
        {
            spawnPoint = transform;
        }

        if (parentRoom == null)
        {
            parentRoom = GetComponentInParent<Room>();
        }

        roomManager = FindFirstObjectByType<RoomManager>();

        if (visualizeSafeZone)
            CreateLineMaterial();
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
            roomManager.OnRoomTransitionComplete -= OnRoomTransitionComplete;

        if (lineMaterial != null)
        {
            if (Application.isPlaying)
                Destroy(lineMaterial);
            else
                DestroyImmediate(lineMaterial);
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

        // この部屋に入った時点ではスポーンしない。
        // セーフゾーンを出た時にだけスポーン処理を開始する。
        if (isRoomActive)
        {
            StopSpawnCoroutine();
            hasStartedSpawn = false;
        }
        else
        {
            StopSpawnCoroutine();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!isRoomActive)
        {
            return;
        }

        if (roomManager != null && roomManager.IsTransitioning)
        {
            return;
        }

        if (targetEnemy == null)
        {
            return;
        }

        if (!IsPlayer(other))
        {
            return;
        }

        if (hasStartedSpawn)
        {
            return;
        }

        StartSpawnAfterSafeZoneExit();
    }

    private void StartSpawnAfterSafeZoneExit()
    {
        hasStartedSpawn = true;

        if (spawnDelay > 0f)
        {
            StopSpawnCoroutine();
            spawnCoroutine = StartCoroutine(DelayedSpawnCoroutine());
        }
        else
        {
            ActivateEnemy();
        }
    }

    private IEnumerator DelayedSpawnCoroutine()
    {
        yield return new WaitForSeconds(spawnDelay);

        ActivateEnemy();

        spawnCoroutine = null;
    }

    private void ActivateEnemy()
    {
        if (targetEnemy == null)
        {
            return;
        }

        targetEnemy.Activate(spawnPoint.position, spawnPoint.rotation);
    }

    public void CaptureInitialState()
    {
        if (hasCapturedInitialState)
        {
            return;
        }

        if (safeZoneCollider == null)
        {
            safeZoneCollider = GetComponent<Collider>();
        }

        initialEnabled = enabled;
        initialColliderEnabled = safeZoneCollider != null && safeZoneCollider.enabled;
        initialHasStartedSpawn = hasStartedSpawn;

        hasCapturedInitialState = true;
    }

    public void ResetToRespawnState()
    {
        if (safeZoneCollider == null)
        {
            safeZoneCollider = GetComponent<Collider>();
        }

        StopSpawnCoroutine();

        if (hasCapturedInitialState)
        {
            enabled = initialEnabled;
            hasStartedSpawn = initialHasStartedSpawn;

            if (safeZoneCollider != null)
            {
                safeZoneCollider.enabled = initialColliderEnabled;
                safeZoneCollider.isTrigger = true;
            }
        }
        else
        {
            enabled = true;
            hasStartedSpawn = false;

            if (safeZoneCollider != null)
            {
                safeZoneCollider.enabled = true;
                safeZoneCollider.isTrigger = true;
            }
        }

        // リスポーン時に即スポーンしない。
        // 現在部屋なら、セーフゾーン退出待ちに戻す。
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
        if (spawnCoroutine != null)
        {
            StopCoroutine(spawnCoroutine);
            spawnCoroutine = null;
        }
    }

    private void OnDisable()
    {
        StopSpawnCoroutine();
    }

    private bool IsPlayer(Collider other)
    {
        if (other == null)
            return false;

        if (usePlayerTag && other.CompareTag(playerTag))
            return true;

        PlayerController player = other.GetComponentInParent<PlayerController>();
        return player != null;
    }

    // =========================================================
    // セーフゾーン視覚化（GL ワイヤーフレーム）
    // =========================================================

    private void CreateLineMaterial()
    {
        Shader shader = Shader.Find("Hidden/Internal-Colored");
        lineMaterial = new Material(shader);
        lineMaterial.hideFlags = HideFlags.HideAndDontSave;
        lineMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        lineMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        lineMaterial.SetInt("_Cull",     (int)UnityEngine.Rendering.CullMode.Off);
        lineMaterial.SetInt("_ZWrite", 0);
    }

    private void OnRenderObject()
    {
        if (!visualizeSafeZone || lineMaterial == null || safeZoneCollider == null)
            return;

        lineMaterial.SetPass(0);

        GL.PushMatrix();
        GL.MultMatrix(transform.localToWorldMatrix);
        GL.Begin(GL.LINES);
        GL.Color(safeZoneColor);

        if (safeZoneCollider is BoxCollider boxCollider)
            DrawBoxColliderWireframe(boxCollider);
        else if (safeZoneCollider is SphereCollider sphereCollider)
            DrawSphereColliderWireframe(sphereCollider);
        else if (safeZoneCollider is CapsuleCollider capsuleCollider)
            DrawCapsuleColliderWireframe(capsuleCollider);

        GL.End();
        GL.PopMatrix();
    }

    private void DrawBoxColliderWireframe(BoxCollider boxCollider)
    {
        Vector3 center   = boxCollider.center;
        Vector3 halfSize = boxCollider.size * 0.5f;

        Vector3 v0 = center + new Vector3(-halfSize.x, -halfSize.y, -halfSize.z);
        Vector3 v1 = center + new Vector3( halfSize.x, -halfSize.y, -halfSize.z);
        Vector3 v2 = center + new Vector3( halfSize.x,  halfSize.y, -halfSize.z);
        Vector3 v3 = center + new Vector3(-halfSize.x,  halfSize.y, -halfSize.z);
        Vector3 v4 = center + new Vector3(-halfSize.x, -halfSize.y,  halfSize.z);
        Vector3 v5 = center + new Vector3( halfSize.x, -halfSize.y,  halfSize.z);
        Vector3 v6 = center + new Vector3( halfSize.x,  halfSize.y,  halfSize.z);
        Vector3 v7 = center + new Vector3(-halfSize.x,  halfSize.y,  halfSize.z);

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
        float   radius = sphereCollider.radius;

        // XY / XZ / YZ の 3 平面に円を描く
        for (int i = 0; i < WireframeSegments; i++)
        {
            float a1 = (i       / (float)WireframeSegments) * Mathf.PI * 2f;
            float a2 = ((i + 1) / (float)WireframeSegments) * Mathf.PI * 2f;

            GL.Vertex(center + new Vector3(Mathf.Cos(a1) * radius, Mathf.Sin(a1) * radius, 0f));
            GL.Vertex(center + new Vector3(Mathf.Cos(a2) * radius, Mathf.Sin(a2) * radius, 0f));

            GL.Vertex(center + new Vector3(Mathf.Cos(a1) * radius, 0f, Mathf.Sin(a1) * radius));
            GL.Vertex(center + new Vector3(Mathf.Cos(a2) * radius, 0f, Mathf.Sin(a2) * radius));

            GL.Vertex(center + new Vector3(0f, Mathf.Cos(a1) * radius, Mathf.Sin(a1) * radius));
            GL.Vertex(center + new Vector3(0f, Mathf.Cos(a2) * radius, Mathf.Sin(a2) * radius));
        }
    }

    private void DrawCapsuleColliderWireframe(CapsuleCollider capsuleCollider)
    {
        Vector3 center    = capsuleCollider.center;
        float   radius    = capsuleCollider.radius;
        float   height    = capsuleCollider.height;
        int     direction = capsuleCollider.direction; // 0:X, 1:Y, 2:Z

        float   cylinderHeight = Mathf.Max(0f, height - radius * 2f);
        Vector3 axisOffset     = Vector3.zero;

        switch (direction)
        {
            case 0: axisOffset = new Vector3(cylinderHeight * 0.5f, 0f, 0f); break;
            case 1: axisOffset = new Vector3(0f, cylinderHeight * 0.5f, 0f); break;
            case 2: axisOffset = new Vector3(0f, 0f, cylinderHeight * 0.5f); break;
        }

        Vector3 top    = center + axisOffset;
        Vector3 bottom = center - axisOffset;

        // 上下円周を描く
        for (int i = 0; i < WireframeSegments; i++)
        {
            float a1 = (i       / (float)WireframeSegments) * Mathf.PI * 2f;
            float a2 = ((i + 1) / (float)WireframeSegments) * Mathf.PI * 2f;

            Vector3 p1, p2;

            if (direction == 0)
            {
                p1 = new Vector3(0f, Mathf.Cos(a1) * radius, Mathf.Sin(a1) * radius);
                p2 = new Vector3(0f, Mathf.Cos(a2) * radius, Mathf.Sin(a2) * radius);
            }
            else if (direction == 1)
            {
                p1 = new Vector3(Mathf.Cos(a1) * radius, 0f, Mathf.Sin(a1) * radius);
                p2 = new Vector3(Mathf.Cos(a2) * radius, 0f, Mathf.Sin(a2) * radius);
            }
            else
            {
                p1 = new Vector3(Mathf.Cos(a1) * radius, Mathf.Sin(a1) * radius, 0f);
                p2 = new Vector3(Mathf.Cos(a2) * radius, Mathf.Sin(a2) * radius, 0f);
            }

            GL.Vertex(top    + p1); GL.Vertex(top    + p2);
            GL.Vertex(bottom + p1); GL.Vertex(bottom + p2);
        }

        // 縦のエッジ（4本）
        for (int i = 0; i < CapsuleEdgeCount; i++)
        {
            float   angle      = (i / (float)CapsuleEdgeCount) * Mathf.PI * 2f;
            Vector3 edgeOffset;

            if (direction == 0)
                edgeOffset = new Vector3(0f, Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
            else if (direction == 1)
                edgeOffset = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            else
                edgeOffset = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);

            GL.Vertex(top    + edgeOffset);
            GL.Vertex(bottom + edgeOffset);
        }
    }
}