using System.Collections.Generic;
using System.IO;
using UnityEngine;
using static MapEditor;

/// MapEditor で保存した JSON ファイルを読み込んでタイルを生成するローダー。
/// MapEditor コンポーネントを必要とせず、実行時に単独で動作します。
[ExecuteAlways]
public class MapLoader : MonoBehaviour
{
    [Header("TileDatabase")]
    [Tooltip("MapEditor で使用しているのと同じ TileDatabase アセットをセットしてください。")]
    [SerializeField] private TileDatabase tileDatabase;

    [Header("マップフォルダ")]
    [Tooltip("JSON が保存されているフォルダパス。Application.dataPath からの相対パスを入力します。\n例: Scenes/DebugScenes/Tomoya/DebugMapScenes/DebugMapEditorScene/DebugMapEditor_MapData")]
    [SerializeField] private string mapFolder = "Scenes/DebugScenes/Tomoya/DebugMapScenes/DebugMapEditorScene/DebugMapEditor_MapData";

    [Header("ステージ番号")]
    [Tooltip("ロードするステージ番号。Stage_[番号].json が対象です。")]
    [SerializeField] private int stageNumber = 1;

    [Header("マップルート")]
    [Tooltip("生成したタイルを子として格納する Transform。未設定の場合はこの GameObject 直下に生成します。")]
    [SerializeField] private Transform mapRoot;

    private readonly List<GameObject> spawnedTiles = new List<GameObject>();
    private readonly float gridSize = 1.0f;

    // JSON フォルダのフルパス
    private string FolderPath => Path.Combine(Application.streamingAssetsPath, mapFolder);

    // ステージ JSON のフルパス
    private string FilePath => Path.Combine(FolderPath, "Stage_" + stageNumber + ".json");

    private void Awake()
    {
        // mapRoot が未設定の場合は自身を使う
        if (mapRoot == null)
        {
            mapRoot = transform;
        }
    }

    private void Start()
    {
        // Edit モードでは自動ロードしない（ContextMenu から手動実行する）
        if (!Application.isPlaying)
        {
            return;
        }

        LoadMap();
    }

    /// JSON ファイルを読み込んでタイルを生成します。
    /// 既に生成済みのタイルがあれば先に破棄してから再生成します。
    public void LoadMap()
    {
        ClearSpawnedTiles();

        if (tileDatabase == null)
        {
            Debug.LogError("[MapLoader] TileDatabase が未設定です。Inspector でセットしてください。", this);
            return;
        }

        if (!File.Exists(FilePath))
        {
            Debug.LogWarning($"[MapLoader] マップファイルが見つかりません: {FilePath}", this);
            return;
        }

        string json = File.ReadAllText(FilePath);
        MapData mapData = JsonUtility.FromJson<MapData>(json);

        if (mapData == null || mapData.tiles == null)
        {
            Debug.LogError("[MapLoader] JSON の解析に失敗しました。", this);
            return;
        }

        foreach (TileData data in mapData.tiles)
        {
            // tileID に対応する定義を TileDatabase から検索する
            TileDefinition def = tileDatabase.tiles.Find(t => t != null && t.tileID == data.tileID);
            if (def == null)
            {
                Debug.LogWarning($"[MapLoader] tileID '{data.tileID}' が TileDatabase に見つかりません。スキップします。", this);
                continue;
            }

            Vector3 spawnPos = new Vector3(
                data.x * gridSize,
                data.y * gridSize,
                data.z * gridSize - 0.01f
            );

            GameObject tile = Instantiate(def.prefab, spawnPos, Quaternion.identity, mapRoot);

            TileType tileType = tile.GetComponent<TileType>();
            if (tileType != null)
            {
                tileType.tileDefinition = def;
                tileType.gimmickType    = data.gimmickType;
                tileType.gimmickID      = data.gimmickID;
            }

            spawnedTiles.Add(tile);
        }

        Debug.Log($"[MapLoader] ロード完了: Stage_{stageNumber}.json ({spawnedTiles.Count} タイル生成)", this);
    }

    /// 生成済みのタイルをすべて破棄します。
    /// spawnedTiles リストの追跡対象に加え、mapRoot 配下の全子オブジェクトも破棄します。
    /// これにより Edit モードで生成されたタイルが Play 開始後も残る二重生成を防ぎます。
    private void ClearSpawnedTiles()
    {
        // spawnedTiles リストで追跡しているタイルを破棄する
        foreach (GameObject tile in spawnedTiles)
        {
            if (tile != null)
            {
                if (Application.isPlaying)
                    Destroy(tile);
                else
                    DestroyImmediate(tile);
            }
        }
        spawnedTiles.Clear();

        // リストで追跡されていない mapRoot 配下の残骸も破棄する
        // （Edit→Play 移行時に spawnedTiles がリセットされても取りこぼしを防ぐ）
        if (mapRoot != null)
        {
            for (int i = mapRoot.childCount - 1; i >= 0; i--)
            {
                GameObject child = mapRoot.GetChild(i).gameObject;
                if (Application.isPlaying)
                    Destroy(child);
                else
                    DestroyImmediate(child);
            }
        }
    }

    // ===== Editor 専用: Inspector 右クリック から呼び出し =====

    [ContextMenu("Load Map (Editor)")]
    private void LoadMapInEditor()
    {
        if (mapRoot == null)
        {
            mapRoot = transform;
        }
        LoadMap();
    }

    [ContextMenu("Clear Map (Editor)")]
    private void ClearMapInEditor()
    {
        if (mapRoot == null)
        {
            mapRoot = transform;
        }
        ClearSpawnedTiles();
        Debug.Log("[MapLoader] マップをクリアしました。", this);
    }
}
