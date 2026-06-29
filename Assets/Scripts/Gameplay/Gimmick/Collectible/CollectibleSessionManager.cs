using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CollectibleSessionManager : MonoBehaviour
{
    [Header("参照: プレイヤー")]
    [Tooltip("死亡受理イベントを購読するPlayerFacadeです。未設定の場合はシーン内から実行時に検索します。")]
    [SerializeField] private PlayerFacade playerFacade;

    [Header("参照: 収集進行")]
    [Tooltip("保存済み収集IDを保持するストアです。v1では永続保存せず、同じステージプレイ中のメモリ保存だけに使います。")]
    [SerializeField] private CollectibleProgressStore progressStore;

    [Header("デバッグ")]
    [Tooltip("仮取得、死亡破棄、をDebug.Logへ出力するかを設定します。")]
    [SerializeField] private bool enableDebugLog = true;

    // 現在の部屋セッション中に取ったが、まだ部屋突破で確定していないID。
    private readonly HashSet<string> temporaryCollectedIds = new HashSet<string>();
    private readonly List<CollectibleItem> registeredItems = new List<CollectibleItem>();


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

    public bool IsTemporarilyCollected(string fullId)
    {
        return !string.IsNullOrWhiteSpace(fullId) && temporaryCollectedIds.Contains(fullId);
    }

    public bool IsSaved(string fullId)
    {
        return progressStore != null && progressStore.IsSaved(fullId);
    }

    public bool IsUnavailable(string fullId)
    {
        return IsSaved(fullId) || IsTemporarilyCollected(fullId);
    }

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

    public void UnregisterItem(CollectibleItem item)
    {
        if (item == null)
        {
            return;
        }

        registeredItems.Remove(item);
    }

    public bool TryTemporarilyCollect(CollectibleItem item)
    {

        if (item == null)
        {
            return false;
        }

        string fullId = item.FullId;
        if (!item.HasValidId)
        {
            Debug.LogWarning($"[Collectible] IDが未設定です。id={fullId}", item);
            return false;
        }

        if (IsSaved(fullId))
        {
            item.ApplyCollectedState( true);
            return false;
        }

        if (!temporaryCollectedIds.Add(fullId))
        {
            item.ApplyCollectedState(true);
            return false;
        }

        item.ApplyCollectedState( true);

        if (enableDebugLog)
        {
            Debug.Log($"[Collectible] 仮取得しました。id={fullId}", item);
        }

        return true;
    }



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

    private void SubscribeDeathEvent()
    {


        if (playerFacade == null)
        {
            if (enableDebugLog)
            {
                Debug.LogWarning("[Collectible] PlayerFacade が見つからないため死亡リセットを購読できません。", this);
            }

            return;
        }

        playerFacade.DeathAccepted += OnPlayerDeathAccepted;
    }

    private void UnsubscribeDeathEvent()
    {

        if (playerFacade != null)
        {
            playerFacade.DeathAccepted -= OnPlayerDeathAccepted;
        }

    }

    private void OnPlayerDeathAccepted(PlayerDeathCause deathCause)
    {
        if (temporaryCollectedIds.Count <= 0)
        {
            if (enableDebugLog)
            {
                Debug.Log($"[Collectible] 死亡リセットしました。仮取得はありません。cause={deathCause}", this);
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
                $"[Collectible] 死亡したため仮取得を破棄しました。count={discardedCount}, ids={discardedIds}",
                this);
        }
    }

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
