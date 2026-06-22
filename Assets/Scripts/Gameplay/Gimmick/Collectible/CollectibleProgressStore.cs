using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CollectibleProgressStore : MonoBehaviour
{
    [Header("デバッグ")]
    [Tooltip("保存済みIDの追加や一覧確認をDebug.Logへ出力するかを設定します。v1ではメモリ上の確認用途だけに使います。")]
    [SerializeField] private bool enableDebugLog = true;

    // v1では永続保存せず、同じステージプレイ中だけ保存済みIDを保持する。
    private readonly HashSet<string> savedIds = new HashSet<string>();

    public IReadOnlyCollection<string> SavedIds => savedIds;

    public bool IsSaved(string fullId)
    {
        return !string.IsNullOrWhiteSpace(fullId) && savedIds.Contains(fullId);
    }

    public bool AddSaved(string fullId)
    {
        if (string.IsNullOrWhiteSpace(fullId))
        {
            Debug.LogWarning("[CollectibleProgressStore] Empty collectible id was ignored.", this);
            return false;
        }

        bool added = savedIds.Add(fullId);
        if (added && enableDebugLog)
        {
            Debug.Log($"[CollectibleProgressStore] Saved collectible id: {fullId}", this);
        }

        return added;
    }

    public void LogSavedIds()
    {
        string ids = savedIds.Count > 0
            ? string.Join(", ", savedIds)
            : "(none)";

        Debug.Log($"[CollectibleProgressStore] Saved collectible ids: count={savedIds.Count}, ids={ids}", this);
    }
}
