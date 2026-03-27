using System.Collections.Generic;
using System.IO;
using UnityEngine;
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

    void Start()
    {
        LoadMap();
    }

    void LoadMap()
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
            Debug.Log("Map file not found");
            return;
        }

        string json = File.ReadAllText(path);

        MapData mapData = JsonUtility.FromJson<MapData>(json);

        List<GameObject> spawnedTiles = new List<GameObject>();

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