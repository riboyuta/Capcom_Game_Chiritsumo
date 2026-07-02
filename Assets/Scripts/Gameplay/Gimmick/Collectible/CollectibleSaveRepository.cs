using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// 保存済み収集アイテムIDの永続保存だけを担当するRepository
/// Application.persistentDataPath 配下のJSONを読み書きし、ゲーム進行状態は保持しない
public sealed class CollectibleSaveRepository
{
    // -----------------------------------------------------------------------------
    // Fields
    // -----------------------------------------------------------------------------

    private const int CurrentVersion = 1;
    private const string SaveFileName = "collectible_progress.json";

    private readonly string savePath;

    [Serializable]
    private sealed class CollectibleSaveData
    {
        public int version = CurrentVersion;
        public List<string> savedIds = new List<string>();
    }

    // -----------------------------------------------------------------------------
    // Constructor
    // -----------------------------------------------------------------------------

    public CollectibleSaveRepository()
    {
        savePath = Path.Combine(Application.persistentDataPath, SaveFileName);
    }

    // -----------------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------------

    public string SavePath => savePath;

    public IReadOnlyCollection<string> LoadSavedIds()
    {
        if (!File.Exists(savePath))
        {
            return Array.Empty<string>();
        }

        try
        {
            string json = File.ReadAllText(savePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                Debug.LogWarning($"[CollectibleSaveRepository] 保存ファイルが空のため空のID一覧を返します path={savePath}");
                return Array.Empty<string>();
            }

            CollectibleSaveData saveData = JsonUtility.FromJson<CollectibleSaveData>(json);
            if (saveData == null)
            {
                Debug.LogWarning($"[CollectibleSaveRepository] 保存ファイルの読み込みに失敗したため空のID一覧を返します path={savePath}");
                return Array.Empty<string>();
            }

            return NormalizeIds(saveData.savedIds);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[CollectibleSaveRepository] 保存ファイルの読み込みに失敗したため空のID一覧を返します path={savePath}, error={exception.Message}");
            return Array.Empty<string>();
        }
    }

    public bool SaveSavedIds(IEnumerable<string> ids)
    {
        try
        {
            string directoryPath = Path.GetDirectoryName(savePath);
            if (!string.IsNullOrEmpty(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            CollectibleSaveData saveData = new CollectibleSaveData
            {
                version = CurrentVersion,
                savedIds = NormalizeIds(ids),
            };

            string json = JsonUtility.ToJson(saveData, prettyPrint: true);
            File.WriteAllText(savePath, json);
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[CollectibleSaveRepository] 保存ファイルの書き込みに失敗しました path={savePath}, error={exception.Message}");
            return false;
        }
    }

    public bool DeleteSaveFile()
    {
        if (!File.Exists(savePath))
        {
            return true;
        }

        try
        {
            File.Delete(savePath);
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[CollectibleSaveRepository] 保存ファイルの削除に失敗しました path={savePath}, error={exception.Message}");
            return false;
        }
    }

    public void LogSavePath()
    {
        Debug.Log($"[CollectibleSaveRepository] 保存ファイルパス path={savePath}");
    }

    // -----------------------------------------------------------------------------
    // Internal Helpers
    // -----------------------------------------------------------------------------

    private static List<string> NormalizeIds(IEnumerable<string> ids)
    {
        List<string> normalizedIds = new List<string>();
        HashSet<string> knownIds = new HashSet<string>();

        if (ids == null)
        {
            return normalizedIds;
        }

        foreach (string id in ids)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            string normalizedId = id.Trim();
            if (knownIds.Add(normalizedId))
            {
                normalizedIds.Add(normalizedId);
            }
        }

        return normalizedIds;
    }
}
