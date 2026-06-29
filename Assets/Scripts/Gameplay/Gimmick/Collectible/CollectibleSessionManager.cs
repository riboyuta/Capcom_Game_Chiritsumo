using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]

/// 収集アイテムのプレイ中状態を管理するコンポーネント
/// 仮取得IDを保持し、死亡時に破棄してItemの表示状態を再反映する
/// 保存済みIDの管理はCollectibleProgressStoreへ委譲する
public sealed class CollectibleSessionManager : MonoBehaviour
{
    // -----------------------------------------------------------------------------
    // Fields
    // -----------------------------------------------------------------------------

    [Header("参照: プレイヤー")]
    [Tooltip("死亡受理イベントを購読するPlayerFacadeです未設定の場合はシーン内から実行時に検索します")]
    [SerializeField] private PlayerFacade playerFacade;

    [Header("参照: 収集進行")]
    [Tooltip("保存済み収集IDを保持するストアですv1では永続保存せず、同じステージプレイ中のメモリ保存だけに使います")]
    [SerializeField] private CollectibleProgressStore progressStore;

    [Header("デバッグ")]
    [Tooltip("仮取得、死亡破棄、をDebug.Logへ出力するかを設定します")]
    [SerializeField] private bool enableDebugLog = true;

    // 現在の部屋セッション中に取ったが、まだ部屋突破で確定していないID
    private readonly HashSet<string> temporaryCollectedIds = new HashSet<string>();

    // 表示状態を更新する対象の収集アイテム一覧
    private readonly List<CollectibleItem> registeredItems = new List<CollectibleItem>();

    // -----------------------------------------------------------------------------
    // Unity Lifecycle
    // -----------------------------------------------------------------------------

    private void Awake()
    {
        ResolveReferences();
    }

    private void Start()
    {
        SubscribeDeathEvent();
        RefreshRegisteredItems();
    }

    private void OnDestroy()
    {
        UnsubscribeDeathEvent();
    }

    // -----------------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------------

    // 指定IDが現在の部屋セッションで仮取得済みか確認する
    public bool IsTemporarilyCollected(string fullId)
    {
        return !string.IsNullOrWhiteSpace(fullId) && temporaryCollectedIds.Contains(fullId);
    }

    // 指定IDが保存済みかProgressStoreへ確認する
    public bool IsSaved(string fullId)
    {
        return progressStore != null && progressStore.IsSaved(fullId);
    }

    // 保存済みまたは仮取得済みで、現在取得できないIDか確認する
    public bool IsUnavailable(string fullId)
    {
        return IsSaved(fullId) || IsTemporarilyCollected(fullId);
    }

    // Itemを管理対象へ登録し、現在の取得状態をすぐ反映する
    public void RegisterItem(CollectibleItem item)
    {
        if (item == null)
        {
            return;
        }

        if (!registeredItems.Contains(item))
        {
            registeredItems.Add(item);
        }

        item.ApplyCollectedState(IsUnavailable(item.FullId));
    }

    // ItemをManagerの管理対象から外す
    public void UnregisterItem(CollectibleItem item)
    {
        if (item == null)
        {
            return;
        }

        registeredItems.Remove(item);
    }

    // Player接触時に呼ばれ、未取得なら仮取得IDへ追加してItemを非表示にする
    public bool TryTemporarilyCollect(CollectibleItem item)
    {

        if (item == null)
        {
            return false;
        }

        string fullId = item.FullId;
        if (!item.HasValidId)
        {
            Debug.LogWarning($"[Collectible] IDが未設定ですid={fullId}", item);
            return false;
        }

        if (IsSaved(fullId))
        {
            item.ApplyCollectedState(isCollected: true);
            return false;
        }

        if (!temporaryCollectedIds.Add(fullId))
        {
            item.ApplyCollectedState(isCollected: true);
            return false;
        }

        item.ApplyCollectedState(isCollected: true);

        if (enableDebugLog)
        {
            Debug.Log($"[Collectible] 仮取得しましたid={fullId}", item);
        }

        return true;
    }

    // -----------------------------------------------------------------------------
    // Event Handlers
    // -----------------------------------------------------------------------------

    // 死亡時に仮取得IDを破棄し、Itemの表示状態を再反映する
    private void OnPlayerDeathAccepted(PlayerDeathCause deathCause)
    {
        if (temporaryCollectedIds.Count <= 0)
        {
            if (enableDebugLog)
            {
                Debug.Log($"[Collectible] 死亡リセットしました仮取得はありませんcause={deathCause}", this);
            }

            return;
        }

        string discardedIds = string.Join(", ", temporaryCollectedIds);
        int discardedCount = temporaryCollectedIds.Count;

        temporaryCollectedIds.Clear();
        RefreshRegisteredItems();

        if (enableDebugLog)
        {
            Debug.Log(
                $"[Collectible] 死亡したため仮取得を破棄しましたcount={discardedCount}, ids={discardedIds}",
                this);
        }
    }

    // -----------------------------------------------------------------------------
    // Main Logic
    // -----------------------------------------------------------------------------

    // 実行時に必要な参照を取得し、使用前の状態に整える
    private void ResolveReferences()
    {
        if (playerFacade == null)
        {
            playerFacade = FindFirstObjectByType<PlayerFacade>();
        }

        if (progressStore == null)
        {
            progressStore = GetComponent<CollectibleProgressStore>();
        }

        if (progressStore == null)
        {
            progressStore = FindFirstObjectByType<CollectibleProgressStore>();
        }
    }

    // Player死亡時にOnPlayerDeathAcceptedが呼ばれるよう登録する
    private void SubscribeDeathEvent()
    {


        if (playerFacade == null)
        {
            if (enableDebugLog)
            {
                Debug.LogWarning("[Collectible] PlayerFacade が見つからないため死亡リセットを購読できません", this);
            }

            return;
        }

        playerFacade.DeathAccepted += OnPlayerDeathAccepted;
    }

    // Manager破棄時にPlayer死亡時の登録を解除する
    private void UnsubscribeDeathEvent()
    {

        if (playerFacade != null)
        {
            playerFacade.DeathAccepted -= OnPlayerDeathAccepted;
        }

    }

    // 保存済み・仮取得済みの状態をもとに、登録済みItemの表示状態を更新する
    private void RefreshRegisteredItems()
    {
        for (int i = registeredItems.Count - 1; i >= 0; i--)
        {
            CollectibleItem item = registeredItems[i];
            if (item == null)
            {
                registeredItems.RemoveAt(i);
                continue;
            }

            item.ApplyCollectedState(IsUnavailable(item.FullId));
        }
    }
}
