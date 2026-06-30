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

    // JSON保存の読み書きだけを担当するRepository
    private CollectibleSaveRepository saveRepository;

    // -----------------------------------------------------------------------------
    // Unity Lifecycle
    // -----------------------------------------------------------------------------

    private void Awake()
    {
        ResolveReferences();
        LoadSavedIdsFromRepository();
    }

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

    // Repositoryから保存済みIDを読み込み、メモリ上の保存済みIDへ反映する
    public void LoadSavedIdsFromRepository()
    {
        ResolveReferences();

        if (saveRepository == null)
        {
            Debug.LogWarning("[CollectibleProgressStore] CollectibleSaveRepository がないため保存済みIDをロードできません", this);
            return;
        }

        savedIds.Clear();

        IReadOnlyCollection<string> loadedIds = saveRepository.LoadSavedIds();
        foreach (string id in loadedIds)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            savedIds.Add(id.Trim());
        }

        Debug.Log($"[CollectibleProgressStore] 保存済みIDをロードしました count={savedIds.Count}, path={saveRepository.SavePath}", this);
    }

    // 現在メモリ上にある保存済みIDをRepositoryへ保存する
    public bool SaveCurrentIds()
    {
        ResolveReferences();

        if (saveRepository == null)
        {
            Debug.LogWarning("[CollectibleProgressStore] CollectibleSaveRepository がないため保存済みIDを保存できません", this);
            return false;
        }

        bool succeeded = saveRepository.SaveSavedIds(savedIds);
        if (succeeded)
        {
            Debug.Log($"[CollectibleProgressStore] 保存済みIDを保存しました count={savedIds.Count}, path={saveRepository.SavePath}", this);
            return true;
        }

        Debug.LogWarning($"[CollectibleProgressStore] 保存済みIDの保存に失敗しました count={savedIds.Count}, path={saveRepository.SavePath}", this);
        return false;
    }

    // デバッグ用に保存済みID一覧を出力する
    public void LogSavedIds()
    {
        string ids = savedIds.Count > 0
            ? string.Join(", ", savedIds)
            : "(none)";

        Debug.Log($"[CollectibleProgressStore] 保存済みID一覧ですcount={savedIds.Count}, ids={ids}", this);
    }

    // Repositoryが使用する保存ファイルパスを出力する
    public void LogSavePath()
    {
        ResolveReferences();

        if (saveRepository == null)
        {
            Debug.LogWarning("[CollectibleProgressStore] CollectibleSaveRepository がないため保存パスを確認できません", this);
            return;
        }

        saveRepository.LogSavePath();
    }

    // -----------------------------------------------------------------------------
    // Main Logic
    // -----------------------------------------------------------------------------

    // 実行時に必要な参照を取得し、使用前の状態に整える
    private void ResolveReferences()
    {
        if (saveRepository == null)
        {
            saveRepository = new CollectibleSaveRepository();
        }
    }
}
