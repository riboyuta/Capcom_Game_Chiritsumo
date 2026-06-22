using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public sealed class SonarChargerActivator : MonoBehaviour, IRespawnResettable
{
    private const int WireframeSegments = 24;
    private const int CapsuleEdgeCount = 4;

    [Header("起動対象の敵")]
    [Tooltip("起動対象の SonarChargerEnemy です。")]
    [SerializeField] private SonarChargerEnemy targetEnemy;

    [Header("ゲーム進行管理")]
    [Tooltip("初回有効発動時に経過時間計測の開始通知を送る GameController です。未使用なら未設定で構いません。")]
    [SerializeField] private GameController gameController;

    [Header("スポーン位置使用フラグ")]
    [Tooltip("起動時に敵をこの位置へ移動させるかです。")]
    [SerializeField] private bool useSpawnPointOnActivate = false;

    [Header("スポーン位置")]
    [Tooltip("起動時のスポーン位置です。未設定時はこの GameObject の Transform を使います。")]
    [SerializeField] private Transform spawnPoint;

    [Header("プレイヤー判定タグ")]
    [Tooltip("プレイヤーとして判定するタグ名です。")]
    [SerializeField] private string playerTag = "Player";

    [Header("敵起動までの遅延時間")]
    [Tooltip("プレイヤーがこのセーフゾーンから出てから、敵が起動するまでの遅延時間です。0なら出た瞬間に起動します。")]
    [SerializeField] private float spawnDelay = 0.0f;

    [Header("所属する部屋")]
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

    // Unityライフサイクル: 初期化
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

    private void OnDisable()
    {
        StopSpawnCoroutine();
    }

    // ルーム遷移イベント: アクティブ状態を更新
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
        }
        else
        {
            StopSpawnCoroutine();
        }
    }

    // セーフゾーン退出: プレイヤーが出たら敵を起動
    private void OnTriggerExit(Collider other)
    {
        // 部屋が非アクティブなら何もしない
        if (!isRoomActive)
        {
            return;
        }

        // 部屋遷移中なら何もしない
        if (roomManager != null && roomManager.IsTransitioning)
        {
            return;
        }

        // 敵参照がないなら何もしない
        if (targetEnemy == null)
        {
            return;
        }

        // プレイヤー以外は無視
        if (!IsPlayer(other))
        {
            return;
        }

        // 既に起動処理を開始していれば無視
        if (hasStartedSpawn)
        {
            return;
        }

        // 敵の起動開始
        StartSpawnAfterSafeZoneExit();
    }

    // 敵の起動処理：ディレイあり/なし
    private void StartSpawnAfterSafeZoneExit()
    {
        // 起動処理を開始済みとマーク
        hasStartedSpawn = true;

        // GameControllerに経過時間開始を通知
        if (gameController != null)
        {
            gameController.StartOrResumeElapsedTime();
        }

        // 遅延設定があればコルーチンで遅延起動、なければ即起動
        if (spawnDelay > 0.0f)
        {
            StopSpawnCoroutine();
            spawnCoroutine = StartCoroutine(DelayedSpawnCoroutine());
        }
        else
        {
            ActivateEnemy();
        }
    }

    // ディレイ付き起動コルーチン
    private IEnumerator DelayedSpawnCoroutine()
    {
        // spawnDelay秒待機
        yield return new WaitForSeconds(spawnDelay);

        // 待機後に敵を起動
        ActivateEnemy();

        // コルーチン完了をマーク
        spawnCoroutine = null;
    }

    // 敵を実際に起動する
    private void ActivateEnemy()
    {
        if (targetEnemy == null)
        {
            return;
        }

        // スポーン位置を使用する設定なら、敵を指定位置に配置してから起動
        if (useSpawnPointOnActivate && spawnPoint != null)
        {
            targetEnemy.BeginChase(spawnPoint.position, spawnPoint.rotation);
        }
        else
        {
            // スポーン位置未使用なら現在位置のまま起動
            targetEnemy.BeginChase();
        }
    }

    // IRespawnResettable: 初期状態を保存
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

    // IRespawnResettable: リスポーン時に初期状態に戻す
    public void ResetToRespawnState()
    {
        // Collider参照がない場合は再取得
        if (safeZoneCollider == null)
        {
            safeZoneCollider = GetComponent<Collider>();
        }

        // 遅延起動コルーチンを中断
        StopSpawnCoroutine();

        // 初期状態が保存されていればそれを復元、そうでなければデフォルト値
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
            // デフォルト: 有効で未起動状態にリセット
            enabled = true;
            hasStartedSpawn = false;

            if (safeZoneCollider != null)
            {
                safeZoneCollider.enabled = true;
                safeZoneCollider.isTrigger = true;
            }
        }

        // 敵本体をリスポーンリセット
        if (targetEnemy != null)
        {
            targetEnemy.ResetEncounterForRespawn();
        }

        // Roomアクティブ状態を再取得
        RefreshRoomActiveState();
    }

    // ヘルパー: ルームのアクティブ状態を更新
    private void RefreshRoomActiveState()
    {
        // RoomManagerまたはRoomがない場合は常にアクティブ扱い
        if (roomManager == null || parentRoom == null)
        {
            isRoomActive = true;
            return;
        }

        // 現在のアクティブRoomが親と同じで、かつ遷移中でなければtrue
        isRoomActive = roomManager.CurrentRoom == parentRoom && !roomManager.IsTransitioning;
    }

    // ヘルパー: 実行中のコルーチンを停止
    private void StopSpawnCoroutine()
    {
        if (spawnCoroutine != null)
        {
            StopCoroutine(spawnCoroutine);
            spawnCoroutine = null;
        }
    }

    // ヘルパー: 当たったColliderがプレイヤーかを判定
    private bool IsPlayer(Collider other)
    {
        if (other == null)
        {
            return false;
        }

        if (other.CompareTag(playerTag))
        {
            return true;
        }

        PlayerController player = other.GetComponentInParent<PlayerController>();
        return player != null;
    }

    // ワイヤーフレーム描画: マテリアル作成
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

    // ワイヤーフレーム描画: レンダリングコールバック
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

    // ワイヤーフレーム描画: BoxCollider
    private void DrawBoxColliderWireframe(BoxCollider boxCollider)
    {
        Vector3 center = boxCollider.center;
        Vector3 size = boxCollider.size;
        Vector3 halfSize = size * 0.5f;

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

    // ワイヤーフレーム描画: SphereCollider
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

    // ワイヤーフレーム描画: CapsuleCollider
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
}
