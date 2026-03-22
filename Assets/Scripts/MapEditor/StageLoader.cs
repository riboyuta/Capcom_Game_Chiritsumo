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
    [SerializeField] private int stageNumber = 1;


    private float gridSize = 1.0f;

    void Start()
    {
        LoadMap();
    }

    void LoadMap()
    {
        string folder = Application.dataPath +
        "/Scenes/DebugScenes/DebugMapScenes/DebugMapEditorScene/DebugMapEditor_MapData";

        string path = folder + "/Stage" + stageNumber + ".json";

        if (!File.Exists(path))
        {
            Debug.Log("Map file not found");
            return;
        }

        string json = File.ReadAllText(path);

        MapData mapData = JsonUtility.FromJson<MapData>(json);

        foreach (TileData data in mapData.tiles)
        {
            Vector3 spawnPos = new Vector3(
                data.x * gridSize,
                data.y * gridSize,
                data.z * gridSize - 0.01f
            );

            Instantiate(tilePrefab[(int)data.type], spawnPos, Quaternion.identity);
        }
    }
}