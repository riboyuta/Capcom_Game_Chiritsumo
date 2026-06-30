using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]

/// 収集アイテム単体を表すコンポーネント
/// Playerとの接触を検知し、CollectibleSessionManagerへ仮取得を依頼する
/// Managerから渡された取得状態に応じて、見た目と当たり判定を切り替える

public sealed class CollectibleItem : MonoBehaviour
{
    // -----------------------------------------------------------------------------
    // Fields
    // -----------------------------------------------------------------------------

    [Header("識別ID")]
    [Tooltip("収集IDのステージ部分ですv1ではInspectorで手入力し、例: Stage_1 のように一意に管理します")]
    [SerializeField] private string stageId = "Stage_1";

    [Tooltip("収集IDの部屋部分です配置先のRoom.RoomIdと同じ値を手入力すると、後から部屋単位で扱いやすくなります")]
    [SerializeField] private string roomId = "Room_01";

    [Tooltip("同じ部屋内でこのアイテムを識別するIDです1部屋に複数置く場合は重複しない値を設定します")]
    [SerializeField] private string localId = "Collectible_01";

    [Header("見た目")]
    [Tooltip("仮取得時に非表示へ切り替える見た目のルートです未設定の場合は子オブジェクト先頭、子がなければ自身を使います")]
    [SerializeField] private Transform visualRoot;

    [Header("参照: 収集セッション")]
    [Tooltip("仮取得状態を管理するCollectibleSessionManagerです未設定の場合はシーン内から実行時に検索します")]
    [SerializeField] private CollectibleSessionManager sessionManager;

    // 取得判定に使うCollider
    private Collider triggerCollider;

    // 表示切替対象のRenderer一覧
    private Renderer[] visualRenderers = System.Array.Empty<Renderer>();

    // 復元用に、初期状態のCollider有効状態を保持する
    private bool initialColliderEnabled;

    // 復元用に、初期状態の各Renderer有効状態を保持する
    private bool[] initialRendererEnabledStates = System.Array.Empty<bool>();

    // stageId / roomId / localId から生成した識別IDを保持する
    private string cachedFullId;

    // -----------------------------------------------------------------------------
    // Unity Lifecycle
    // -----------------------------------------------------------------------------

    private void Awake()
    {
        ResolveReferences();
        CaptureInitialVisibility();
        RebuildFullId();
        LogWarningIfIdConfigurationInvalid();
    }


    // 有効化時に、Managerの管理対象へ登録する
    private void OnEnable()
    {
        if (sessionManager == null)
        {
            Debug.LogError($"[Collectible] CollectibleSessionManagerが見つかりませんFullId={FullId}", this);
            return;
        }
        sessionManager.RegisterItem(this);

    }

    // 無効化時に、Managerの管理対象から解除する
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

    // 外部が収集アイテムのID情報を参照するための読み取り専用プロパティ
    public string StageId => stageId;
    public string RoomId => roomId;
    public string LocalId => localId;

    // ID未入力のアイテムを収集対象として扱わないための保険
    public bool HasValidId =>
        !string.IsNullOrWhiteSpace(stageId)
        && !string.IsNullOrWhiteSpace(roomId)
        && !string.IsNullOrWhiteSpace(localId);

    public bool HasValidFullId => HasValidId && !string.IsNullOrWhiteSpace(FullId);

    // 外部へこのアイテムの識別IDを渡す
    public string FullId
    {
        get
        {
            return cachedFullId;
        }
    }

    // Managerの判定結果に従って、見た目と当たり判定を取得状態へ反映する
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

    // Player接触時に、Managerへ仮取得処理を依頼する
    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
        {
            return;
        }

        if (!HasValidFullId)
        {
            Debug.LogWarning($"[CollectibleItem] FullIdが無効なため仮取得をスキップしました object={gameObject.name}, fullId={FullId}", this);
            return;
        }

        if (sessionManager == null)
        {
            Debug.LogError($"[Collectible] CollectibleSessionManagerが未設定のため、仮取得できませんFullId={FullId}", this);
            return;
        }


        sessionManager.TryTemporarilyCollect(this);
    }

    // -----------------------------------------------------------------------------
    // Main Logic
    // -----------------------------------------------------------------------------

    // 実行時に必要な参照を取得し、使用前の状態に整える
    private void ResolveReferences()
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

        if (sessionManager == null)
        {
            sessionManager = FindFirstObjectByType<CollectibleSessionManager>();
        }
    }

    // stageId / roomId / localId を、状態管理用のFullIdにまとめる
    private void RebuildFullId()
    {
        cachedFullId = $"{NormalizeIdPart(stageId)}/{NormalizeIdPart(roomId)}/{NormalizeIdPart(localId)}";
    }

    // ID未入力の配置ミスをPlay Mode開始時に見つける
    private void LogWarningIfIdConfigurationInvalid()
    {
        if (HasValidId)
        {
            return;
        }

        Debug.LogWarning(
            $"[CollectibleItem] ID設定が不足しています object={gameObject.name}, fullId={FullId}, stageId={stageId}, roomId={roomId}, localId={localId}",
            this);
    }

    // 死亡時に復元できるよう、ColliderとRendererの初期状態を保存する
    private void CaptureInitialVisibility()
    {



        initialColliderEnabled = triggerCollider != null && triggerCollider.enabled;
        initialRendererEnabledStates = new bool[visualRenderers.Length];

        for (int i = 0; i < visualRenderers.Length; i++)
        {
            initialRendererEnabledStates[i] = visualRenderers[i] != null && visualRenderers[i].enabled;
        }

    }

    // 取得済み状態として、描画と当たり判定を無効化する
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

// 描画と当たり判定を初期状態へリセットする
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

    // ID入力時の前後空白を取り除くための保険
    private static string NormalizeIdPart(string idPart)
    {
        return string.IsNullOrWhiteSpace(idPart) ? string.Empty : idPart.Trim();
    }
}
