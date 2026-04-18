using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using static MapEditor;
using static UnityEditor.PlayerSettings;

public class StageLoader : MonoBehaviour
{
    [Header("プレハブパレット")]
    [Tooltip("キー毎に割り当てられているプレハブ")]
    [SerializeField] private GameObject[] tilePrefab;

    [Header("現在のステージ番号")]
    [Tooltip("現在編集しているステージの番号")]
    [SerializeField] private int stageNumber = 5;

    [Header("マップフォルダ")]
    [Tooltip("現在選択されているファイルのマップに読み書きします")]
    [SerializeField] private string mapFolder = "MapData";

    [Header("登録されているマップルート")]
    [Tooltip("このオブジェを親としてマップが生成されます")]
    [SerializeField] private Transform mapRoot;



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

        folder = Path.Combine(Application.dataPath, mapFolder);
        //folder = Application.dataPath +
        // "/Scenes/DebugScenes/koki/DebugMapScenes/DebugMapEditorScene/DebugMapEditor_MapData";
#else
// ビルドされたゲームで実行されている場合
folder = Path.Combine(Application.streamingAssetsPath, "DebugMapEditor_MapData");
#endif

        path = Path.Combine(folder, "Stage_" + stageNumber + ".json");


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


        
            //GameObject tile =
            //    Instantiate(tilePrefab[(int)data.type], spawnPos, Quaternion.identity);
            GameObject tile =
                Instantiate(tilePrefab[(int)data.type], spawnPos, Quaternion.identity, mapRoot);

            TileType tileType = tile.GetComponent<TileType>();

            tileType.type = data.type;
            tileType.gimmickID = data.gimmickID;

            spawnedTiles.Add(tile);
        }

        ConnectGimmicks(spawnedTiles); //   ギミックの接続
    }


    IEnumerator BuildStageMapFromJsonWeb()
    {
        string path = Application.streamingAssetsPath + "/" + mapFolder + "/Stage_" + stageNumber + ".json";

        UnityWebRequest request = UnityWebRequest.Get(path);
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("Map Load Error: " + request.error);
            yield break;
        }

        string json = request.downloadHandler.text;

        ClearSpawnedTiles();

        MapData mapData = JsonUtility.FromJson<MapData>(json);
        if (mapData == null || mapData.tiles == null)
        {
            Debug.LogError("MapData parse error");
            yield break;
        }

        foreach (TileData data in mapData.tiles)
        {
            Vector3 spawnPos = new Vector3(
                data.x * gridSize,
                data.y * gridSize,
                data.z * gridSize - 0.01f
            );

            GameObject tile =
            Instantiate(tilePrefab[(int)data.type], spawnPos, Quaternion.identity, mapRoot);

            TileType tileType = tile.GetComponent<TileType>();
            tileType.type = data.type;
            tileType.gimmickID = data.gimmickID;

            spawnedTiles.Add(tile);
        }

        ConnectGimmicks(spawnedTiles);
    }


    void BuildStageMapFromJsonResources() //Resourceからの読み込み (ファイル場所に注意)
    {
        TextAsset jsonFile = Resources.Load<TextAsset>("Maps/Stage_" + stageNumber);

        if (jsonFile == null)
        {
            Debug.LogError("Map not found: Stage_" + stageNumber);
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

            //GameObject tile =
            //    Instantiate(tilePrefab[(int)data.type], spawnPos, Quaternion.identity);
            GameObject tile =
               Instantiate(tilePrefab[(int)data.type], spawnPos, Quaternion.identity, mapRoot);

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
  
        for (int i = mapRoot.childCount - 1; i >= 0; i--)
        {
            GameObject obj = mapRoot.GetChild(i).gameObject;

            //実行外かプレイ中かに応じて消去方法を変更する
            if (Application.isPlaying)
                Destroy(obj);
            else
                DestroyImmediate(obj);
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


    //===========================-------
    //　　　　　実行外関数
    //===========================-------
    [ContextMenu("Load Map (Editor)")]
    void LoadMapOutPlaying()
    {
        Debug.Log("spawnedTiles count = " + spawnedTiles.Count);
        ClearSpawnedTiles();
        LoadMap();
        Debug.Log("spawnedTiles count = " + spawnedTiles.Count);
    }


    [ContextMenu("Clear Loaded Map (Editor)")]
    void ClearLoadedMapOutPlaying()
    {
        ClearSpawnedTiles();
        spawnedTiles.Clear();
        Debug.Log("Loaded Map Cleared");
    }
}