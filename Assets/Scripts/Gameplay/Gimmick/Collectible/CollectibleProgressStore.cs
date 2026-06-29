using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]

/// 保存済みの収集アイテムIDを保持するコンポーネント
public sealed class CollectibleProgressStore : MonoBehaviour
{
    // -----------------------------------------------------------------------------
    // Fields
    // -----------------------------------------------------------------------------

    // 保存済みとして確定した収集アイテムID
    private readonly HashSet<string> savedIds = new HashSet<string>();

    // -----------------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------------

    public IReadOnlyCollection<string> SavedIds => savedIds;

    // 指定IDが保存済みか確認する
    public bool IsSaved(string fullId)
    {
        return !string.IsNullOrWhiteSpace(fullId) && savedIds.Contains(fullId);
    }

    // 保存済みIDに追加し、今回新しく追加できたかを返す
    public bool AddSaved(string fullId)
    {
        if (string.IsNullOrWhiteSpace(fullId))
        {
            Debug.LogWarning("[Collectible] fullId が空のため保存しません", this);
            return false;
        }

        bool added = savedIds.Add(fullId);
        if (added)
        {
            Debug.Log($"[Collectible] 保存済みIDに追加しましたid={fullId}", this);
        }

        return added;
    }

    // デバッグ用に保存済みID一覧を出力する
    public void LogSavedIds()
    {
        string ids = savedIds.Count > 0
            ? string.Join(", ", savedIds)
            : "(none)";

        Debug.Log($"[Collectible] 保存済みID一覧ですcount={savedIds.Count}, ids={ids}", this);
    }
}
