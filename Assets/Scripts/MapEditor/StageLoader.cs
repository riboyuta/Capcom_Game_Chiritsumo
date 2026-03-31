using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using static MapEditor;

public class StageLoader : MonoBehaviour
{
    [Header("プレハブパレット")]
    [Tooltip("キー毎に割り当てられているプレハブ")]
    [SerializeField] private GameObject[] tilePrefab;

    [Header("現在のステージ番号")]
    [Tooltip("現在編集しているステージの番号")]
    [SerializeField] private int stageNumber = 5;


    private float gridSize = 1.0f;
    private readonly List<GameObject> spawnedTiles = new List<GameObject>();
    void Start()
    {
        LoadMap();

    }

    void LoadMap()
    {

#if UNITY_WEBGL && !UNITY_EDITOR //ビルド時でWebGL版
    //StartCoroutine(BuildStageMapFromJsonWeb());
     BuildStageMapFromJsonResources(); //Resourceからの読み込み
#else
        BuildStageMapFromJson();
#endif

    }

    // 死亡復帰向けに既存ランタイムマップを破棄してから再生成します。
    public void RebuildStageForRespawn()
    {
        ClearSpawnedTiles();
        LoadMap();        
    }

    // JSON 読み込みとタイル生成の本体処理です。
    void BuildStageMapFromJson()
    {

        string folder;
        string path;

#if UNITY_EDITOR
        // Unityエディターで実行されている場合
        folder = Application.dataPath +
            "/Scenes/DebugScenes/koki/DebugMapScenes/DebugMapEditorScene/DebugMapEditor_MapData";
#else
// ビルドされたゲームで実行されている場合
folder = Path.Combine(Application.streamingAssetsPath, "DebugMapEditor_MapData");
#endif

        path = Path.Combine(folder, "Stage" + stageNumber + ".json");


        if (!File.Exists(path))
        {
            Debug.LogWarning("マップファイルが見つかりません。");
            return;
        }

        string json = File.ReadAllText(path);

        MapData mapData = JsonUtility.FromJson<MapData>(json);

        //spawnedTiles.Clear();
        ClearSpawnedTiles();

        foreach (TileData data in mapData.tiles)
        {
            Vector3 spawnPos = new Vector3(
                data.x * gridSize,
                data.y * gridSize,
                data.z * gridSize - 0.01f
            );

            GameObject tile =
                Instantiate(tilePrefab[(int)data.type], spawnPos, Quaternion.identity);

            TileType tileType = tile.GetComponent<TileType>();

            tileType.type = data.type;
            tileType.gimmickID = data.gimmickID;

            spawnedTiles.Add(tile);
        }

        ConnectGimmicks(spawnedTiles);
    }


    IEnumerator BuildStageMapFromJsonWeb()
    {
        string folder = Path.Combine(Application.streamingAssetsPath, "DebugMapEditor_MapData");
        string path = Path.Combine(folder, "Stage" + stageNumber + ".json");

        UnityWebRequest request = UnityWebRequest.Get(path);
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError(request.error);
            yield break;
        }

        string json = request.downloadHandler.text;

        //spawnedTiles.Clear();   
        ClearSpawnedTiles();

        MapData mapData = JsonUtility.FromJson<MapData>(json);

        foreach (TileData data in mapData.tiles)
        {
            Vector3 spawnPos = new Vector3(
                data.x * gridSize,
                data.y * gridSize,
                data.z * gridSize - 0.01f
            );

            GameObject tile =
                Instantiate(tilePrefab[(int)data.type], spawnPos, Quaternion.identity);

            TileType tileType = tile.GetComponent<TileType>();
            tileType.type = data.type;
            tileType.gimmickID = data.gimmickID;

            spawnedTiles.Add(tile);
        }

        ConnectGimmicks(spawnedTiles);
    }


    void BuildStageMapFromJsonResources() //Resourceからの読み込み (ファイル場所に注意)
    {
        TextAsset jsonFile = Resources.Load<TextAsset>("Maps/Stage" + stageNumber);

        if (jsonFile == null)
        {
            Debug.LogError("Map not found: Stage" + stageNumber);
            return;
        }

        string json = jsonFile.text;

        ClearSpawnedTiles();

        MapData mapData = JsonUtility.FromJson<MapData>(json);

        foreach (TileData data in mapData.tiles)
        {
            Vector3 spawnPos = new Vector3(
                data.x * gridSize,
                data.y * gridSize,
                data.z * gridSize - 0.01f
            );

            GameObject tile =
                Instantiate(tilePrefab[(int)data.type], spawnPos, Quaternion.identity);

            TileType tileType = tile.GetComponent<TileType>();
            tileType.type = data.type;
            tileType.gimmickID = data.gimmickID;

            spawnedTiles.Add(tile);
        }

        ConnectGimmicks(spawnedTiles);
    }




    
    // StageLoader が生成したランタイムタイルのみを明示的に破棄します。
    void ClearSpawnedTiles()
    {
        for (int i = 0; i < spawnedTiles.Count; i++)
        {
            if (spawnedTiles[i] == null)
            {
                continue;
            }

            Destroy(spawnedTiles[i]);
        }

        spawnedTiles.Clear();
    }

    // 生成済みタイル同士の関連を見て Slide と Switch の接続を組み直します。


    void ConnectGimmicks(List<GameObject> tiles)
    {
        foreach (var tile in tiles)
        {
            SlideGimmick slide = tile.GetComponent<SlideGimmick>();

            if (slide == null) continue;

            TileType slideTile = tile.GetComponent<TileType>();

            foreach (var other in tiles)
            {
                SwitchGimmick sw = other.GetComponent<SwitchGimmick>();

                if (sw == null) continue;

                TileType swTile = other.GetComponent<TileType>();

                if (slideTile.gimmickID == swTile.gimmickID)
                {
                    slide.SetSwitch(sw);
                }
            }
        }
    }
}