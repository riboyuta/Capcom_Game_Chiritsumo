using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class MapEditor : MonoBehaviour
{


    Dictionary<Vector3Int, GameObject> tiles =
        new Dictionary<Vector3Int, GameObject>();

    public GameObject[] tilePrefab;

    int currentTile = 1;

    public int stageNumber = 1;

    public float gridSize = 1.0f;

    void Update()
    {

        //数字キーで使うタイルを変える
        if (Input.GetKeyDown(KeyCode.Alpha0) || Input.GetKeyDown(KeyCode.Keypad0)) currentTile = 0;
        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) currentTile = 1;
        if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) currentTile = 2;


        if (Input.GetMouseButton(0))
        {
            PlaceTile();
        }

        if (Input.GetMouseButton(1))
        {
            RemoveTile();
        }

        if (Input.GetKeyDown(KeyCode.S))
        {
            SaveMap();
        }

        if (Input.GetKeyDown(KeyCode.L))
        {
            LoadMap();
        }

    }



    Vector3Int GetGridPosition()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit))
        {
            Vector3 pos = hit.point;

            int gridX = Mathf.RoundToInt(pos.x / gridSize);
            int gridY = Mathf.RoundToInt(pos.y / gridSize);
            int gridZ = Mathf.RoundToInt(pos.z / gridSize);

            return new Vector3Int(gridX, gridY, gridZ);
        }

        return new Vector3Int(0, 0, 0);

    }




    void PlaceTile()
    {
        Vector3Int gridPos = GetGridPosition();

        if (tiles.ContainsKey(gridPos)) return;

        Vector3 spawnPos = new Vector3(
            gridPos.x * gridSize,
            gridPos.y * gridSize,
            gridPos.z * gridSize - 0.01f
        );

        GameObject tile =
            Instantiate(tilePrefab[currentTile], spawnPos, Quaternion.identity);

        tiles.Add(gridPos, tile);
    }




    void RemoveTile()
    {
        Vector3Int gridPos = GetGridPosition();

        if (!tiles.ContainsKey(gridPos)) return;

        Destroy(tiles[gridPos]);
        tiles.Remove(gridPos);
    }


    //===========================-------
    //　　　　　保存システム
    //===========================-------

    [System.Serializable]
    public class TileData
    {
        public int x;
        public int y;
        public int z;

        public TileTypeEnum type;
    }

    [System.Serializable]
    public class MapData
    {
        public List<TileData> tiles = new List<TileData>();
    }

    public void SaveMap()
    {
        MapData mapData = new MapData();

        foreach (var tile in tiles)
        {
            TileData data = new TileData();

            data.x = tile.Key.x;
            data.y = tile.Key.y;
            data.z = tile.Key.z;

            data.type = tile.Value.GetComponent<TileType>().type;

            mapData.tiles.Add(data);
        }

        string json = JsonUtility.ToJson(mapData, true);

        string folder = Application.dataPath +
"/Scenes/DebugScenes/DebugMapScenes/DebugMapEditorScene/DebugMapEditor_MapData";
        if (!Directory.Exists(folder))
        {
            Directory.CreateDirectory(folder);
        }

        string path = folder + "/Stage" + stageNumber + ".json";

        File.WriteAllText(path, json);
        Debug.Log("Map Saved : " + path);
    }




    public void LoadMap()
    {
        //現在読み込まれてるマップを全て消す
        foreach (var tile in tiles)
        {
            Destroy(tile.Value);
        }

        tiles.Clear();

        string folder = Application.dataPath +
"/Scenes/DebugScenes/DebugMapScenes/DebugMapEditorScene/DebugMapEditor_MapData";
        if (!Directory.Exists(folder))
        {
            Directory.CreateDirectory(folder);
        }

        string path = folder + "/Stage" + stageNumber + ".json";

        if (!System.IO.File.Exists(path))
        {
            Debug.Log("Map file not found");
            return;
        }

        string json = System.IO.File.ReadAllText(path);

        MapData mapData = JsonUtility.FromJson<MapData>(json);

        foreach (TileData data in mapData.tiles)
        {
            Vector3Int gridPos = new Vector3Int(data.x, data.y, data.z);

            Vector3 spawnPos = new Vector3(
                data.x * gridSize,
                data.y * gridSize,
                data.z * gridSize - 0.01f
            );

            GameObject tile =
                Instantiate(tilePrefab[(int)data.type], spawnPos, Quaternion.identity);

            tile.GetComponent<TileType>().type = (TileTypeEnum)data.type;

            tiles.Add(gridPos, tile);
        }

        Debug.Log("Map Loaded");
    }
}

