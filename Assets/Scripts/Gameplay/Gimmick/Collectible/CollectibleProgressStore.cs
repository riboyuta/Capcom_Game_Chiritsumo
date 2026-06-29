using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CollectibleProgressStore : MonoBehaviour
{
    // -----------------------------------------------------------------------------
    // Fields
    // -----------------------------------------------------------------------------

    // 永続保存せず、同じステージプレイ中だけ保存済みIDを保持する。
    private readonly HashSet<string> savedIds = new HashSet<string>();

    // -----------------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------------

    public IReadOnlyCollection<string> SavedIds => savedIds;

    public bool IsSaved(string fullId)
    {
        return !string.IsNullOrWhiteSpace(fullId) && savedIds.Contains(fullId);
    }

    public bool AddSaved(string fullId)
    {
        if (string.IsNullOrWhiteSpace(fullId))
        {
            Debug.LogWarning("[Collectible] fullId が空のため保存しません。", this);
            return false;
        }

        bool added = savedIds.Add(fullId);
        if (added)
        {
            Debug.Log($"[Collectible] 保存済みIDに追加しました。id={fullId}", this);
        }

        return added;
    }

    public void LogSavedIds()
    {
        string ids = savedIds.Count > 0
            ? string.Join(", ", savedIds)
            : "(none)";

        Debug.Log($"[Collectible] 保存済みID一覧です。count={savedIds.Count}, ids={ids}", this);
    }
}
