using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]

/// 収集アイテム単体を表すコンポーネント。
/// Playerとの接触を検知し、CollectibleSessionManagerへ仮取得を依頼する。
/// Managerから渡された取得状態に応じて、見た目と当たり判定を切り替える。

public sealed class CollectibleItem : MonoBehaviour
{
    // -----------------------------------------------------------------------------
    // Fields
    // -----------------------------------------------------------------------------

    [Header("識別ID")]
    [Tooltip("収集IDのステージ部分です。v1ではInspectorで手入力し、例: Stage_1 のように一意に管理します。")]
    [SerializeField] private string stageId = "Stage_1";

    [Tooltip("収集IDの部屋部分です。配置先のRoom.RoomIdと同じ値を手入力すると、後から部屋単位で扱いやすくなります。")]
    [SerializeField] private string roomId = "Room_01";

    [Tooltip("同じ部屋内でこのアイテムを識別するIDです。1部屋に複数置く場合は重複しない値を設定します。")]
    [SerializeField] private string localId = "Collectible_01";

    [Header("見た目")]
    [Tooltip("仮取得時に非表示へ切り替える見た目のルートです。未設定の場合は子オブジェクト先頭、子がなければ自身を使います。")]
    [SerializeField] private Transform visualRoot;

    [Header("参照: 収集セッション")]
    [Tooltip("仮取得状態を管理するCollectibleSessionManagerです。未設定の場合はシーン内から実行時に検索します。")]
    [SerializeField] private CollectibleSessionManager sessionManager;

    // 取得判定に使うCollider。
    private Collider triggerCollider;

    // 表示切替対象のRenderer一覧。
    private Renderer[] visualRenderers = System.Array.Empty<Renderer>();

    private bool initialColliderEnabled;
    private bool[] initialRendererEnabledStates = System.Array.Empty<bool>();

    // stageId / roomId / localId から生成した収集アイテム識別ID。
    private string cachedFullId;

    // -----------------------------------------------------------------------------
    // Unity Lifecycle
    // -----------------------------------------------------------------------------

    private void Awake()
    {
        EnsureRuntimeReferences();
        CaptureInitialVisibility();
        RebuildFullId();
    }


    
    private void OnEnable()
    {
        if (sessionManager != null)
        {
            sessionManager.RegisterItem(this);
        }

    }

    private void OnDisable()
    {
        if (sessionManager != null)
        {
            sessionManager.UnregisterItem(this);
        }
    }

    // -----------------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------------

    public string StageId => stageId;
    public string RoomId => roomId;
    public string LocalId => localId;
    public bool HasValidId =>
        !string.IsNullOrWhiteSpace(stageId)
        && !string.IsNullOrWhiteSpace(roomId)
        && !string.IsNullOrWhiteSpace(localId);

    public string FullId
    {
        get
        {
            return cachedFullId;
        }
    }

    public void ApplyCollectedState(bool isCollected)
    {
        if (isCollected)
        {
            HideForCollectedState();
            return;
        }

        RestoreInitialVisibility();
    }

    // -----------------------------------------------------------------------------
    // Event Handlers
    // -----------------------------------------------------------------------------

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
        {
            return;
        }

        if (sessionManager == null)
        {
            sessionManager = FindFirstObjectByType<CollectibleSessionManager>();
        }

        if (sessionManager == null)
        {
            Debug.LogWarning($"[Collectible] CollectibleSessionManager がないため仮取得できません。id={FullId}", this);
            return;
        }

        sessionManager.TryTemporarilyCollect(this);
    }

    // -----------------------------------------------------------------------------
    // Main Logic
    // -----------------------------------------------------------------------------

    private void EnsureRuntimeReferences()
    {
        if (triggerCollider == null)
        {
            triggerCollider = GetComponent<Collider>();
        }

        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
        }

        if (visualRoot == null)
        {
            visualRoot = transform.childCount > 0 ? transform.GetChild(0) : transform;
        }

        visualRenderers = visualRoot != null
            ? visualRoot.GetComponentsInChildren<Renderer>(true)
            : System.Array.Empty<Renderer>();
    }

    private void RebuildFullId()
    {
        cachedFullId = $"{NormalizeIdPart(stageId)}/{NormalizeIdPart(roomId)}/{NormalizeIdPart(localId)}";
    }

    private void CaptureInitialVisibility()
    {



        initialColliderEnabled = triggerCollider != null && triggerCollider.enabled;
        initialRendererEnabledStates = new bool[visualRenderers.Length];

        for (int i = 0; i < visualRenderers.Length; i++)
        {
            initialRendererEnabledStates[i] = visualRenderers[i] != null && visualRenderers[i].enabled;
        }

    }

    private void HideForCollectedState()
    {

        if (triggerCollider != null)
        {
            triggerCollider.enabled = false;
        }
            
        for (int i = 0; i < visualRenderers.Length; i++)
        {
            if (visualRenderers[i] != null)
            {
                visualRenderers[i].enabled = false;
            }
        }
    }

    private void RestoreInitialVisibility()
    {
        if (triggerCollider != null)
        {
            triggerCollider.enabled = initialColliderEnabled;
        }

        int restoreCount = Mathf.Min(visualRenderers.Length, initialRendererEnabledStates.Length);
        for (int i = 0; i < restoreCount; i++)
        {
            if (visualRenderers[i] != null)
            {
                visualRenderers[i].enabled = initialRendererEnabledStates[i];
            }
        }
    }

    // -----------------------------------------------------------------------------
    // Query Helpers
    // -----------------------------------------------------------------------------

    private static string NormalizeIdPart(string idPart)
    {
        return string.IsNullOrWhiteSpace(idPart) ? string.Empty : idPart.Trim();
    }
}
