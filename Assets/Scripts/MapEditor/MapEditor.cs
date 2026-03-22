using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

[ExecuteAlways]
public class MapEditor : MonoBehaviour
{

    Dictionary<Vector3Int, GameObject> tiles =
        new Dictionary<Vector3Int, GameObject>();

    [Header("プレハブパレット")]
    [Tooltip("キー毎に割り当てられているプレハブ")]
    [SerializeField]　private GameObject[] tilePrefab;


    [Header("現在のステージ番号")]
    [Tooltip("現在編集しているステージの番号")]
    [SerializeField] private int stageNumber = 1;


    [Header("コメント")]
    [Tooltip("ステージ毎に保存されるメモ書き")]
    [TextArea(15, 30)]
    [SerializeField] private string comment;


    [Header("タイル変換")]
    [Tooltip("変換したいプレハブをセットしよう")]
    [SerializeField] private ChangeTileContainer changeTileContainer;
    [System.Serializable]
    public struct ChangeTileContainer
    {
        public TileTypeEnum target;
        public TileTypeEnum changeTo;
    }


    float gridSize = 1.0f;
   
    bool showSaveConfirm = false;

    bool showLoadConfirm = false;

    private int currentTile = 1;


   

    void OnEnable()
    {
        RegisterExistingTiles();
    }

 
    void Update()
    {
      


        // プレイ中
        if (Application.isPlaying)
        {
            if (showSaveConfirm || showLoadConfirm) { return; } //セーブorロード選択中はエディット操作できない

            //数字キーで使うタイルを変える
            if (Input.GetKeyDown(KeyCode.Alpha0) || Input.GetKeyDown(KeyCode.Keypad0)) currentTile = 0;
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) currentTile = 1;
            if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) currentTile = 2;
            if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) currentTile = 3;
            if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4)) currentTile = 4;
            if (Input.GetKeyDown(KeyCode.Alpha5) || Input.GetKeyDown(KeyCode.Keypad5)) currentTile = 5;
            if (Input.GetKeyDown(KeyCode.Alpha6) || Input.GetKeyDown(KeyCode.Keypad6)) currentTile = 6;
            if (Input.GetKeyDown(KeyCode.Alpha7) || Input.GetKeyDown(KeyCode.Keypad7)) currentTile = 7;
            if (Input.GetKeyDown(KeyCode.Alpha8) || Input.GetKeyDown(KeyCode.Keypad8)) currentTile = 8;
            if (Input.GetKeyDown(KeyCode.Alpha9) || Input.GetKeyDown(KeyCode.Keypad9)) currentTile = 9;


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
                //SaveMap();
                showSaveConfirm = true;
            }



            if (Input.GetKeyDown(KeyCode.L))
            {
                //LoadMap();
                showLoadConfirm = true;
            }
        }
    
        //実行外
        else
        {
            
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


    void ClearTile()
    {
        foreach (var tile in tiles.Values)
        {
            if (tile != null)
            {
                Destroy(tile);
            }
        }
    }


    void ChangeTileAll(TileTypeEnum from, TileTypeEnum to)
    {
        List<Vector3Int> keys = new List<Vector3Int>(tiles.Keys);

        foreach (var gridPos in keys)
        {
            GameObject tile = tiles[gridPos];

            TileType tileType = tile.GetComponent<TileType>();

            if (tileType.type == from)
            {
                Vector3 pos = tile.transform.position;

                Destroy(tile);

                GameObject newTile =
                    Instantiate(tilePrefab[(int)to], pos, Quaternion.identity);

                newTile.GetComponent<TileType>().type = to;

                tiles[gridPos] = newTile;
            }
        }
    }


    //===========================-------
    //　　　　　実行外関数
    //===========================-------
    [ContextMenu("Load Map (Editor)")]
    void LoadMapOutPlaying()
    {
        LoadMap();
        RegisterExistingTiles();
    }


    [ContextMenu("Clear Loaded Map (Editor)")]
    void ClearLoadedMapOutPlaying()
    {
        foreach (var tile in tiles.Values)
        {
            if (tile != null)
            {
                DestroyImmediate(tile);
            }
        }

        tiles.Clear();

        Debug.Log("Loaded Map Cleared");
    }

    [ContextMenu("ChangeTiles (Editor)")]
    void ChangeTilesOutPlaying()
    {
        ChangeTileAll(changeTileContainer.target, changeTileContainer.changeTo);
        RegisterExistingTiles();
    }

    //===========================-------
    //　　　　　保存システム
    //===========================-------

    [System.Serializable] //タイル個々のクラス
    public class TileData
    {
        public int x;
        public int y;
        public int z;

        public TileTypeEnum type;
    }

    [System.Serializable] //マップ全体のクラス
    public class MapData
    {
        public List<TileData> tiles = new List<TileData>();
        public string comment;
    }

    public void SaveMap()
    {
        MapData mapData = new MapData();
        mapData.comment = comment;

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
            if (Application.isPlaying)
                Destroy(tile.Value);
            else
                DestroyImmediate(tile.Value);
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
        comment = mapData.comment;

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

            tiles.Add(gridPos, tile);
        }

        Debug.Log("Map Loaded");
    }


    

    //現在のブロックをレジストとして登録する
    void RegisterExistingTiles()
    {
        tiles.Clear();

        TileType[] allTiles = FindObjectsByType<TileType>(FindObjectsSortMode.None);

        foreach (var tile in allTiles)
        {
            Vector3 pos = tile.transform.position;

            Vector3Int gridPos = new Vector3Int(
                Mathf.RoundToInt(pos.x / gridSize),
                Mathf.RoundToInt(pos.y / gridSize),
                Mathf.RoundToInt(pos.z / gridSize)
            );

            if (!tiles.ContainsKey(gridPos))
            {
                tiles.Add(gridPos, tile.gameObject);
            }
        }
    }

    //===========================-------
    //　　　　　   GUI
    //===========================-------
    void OnGUI()
    {
        GUIStyle style = new GUIStyle(GUI.skin.box);
        style.fontSize = 160;
        style.alignment = TextAnchor.UpperCenter;

        GUIStyle buttonStyle = new GUIStyle(GUI.skin.button);
        buttonStyle.fontSize = 80;

        float w = Screen.width;
        float h = Screen.height;

        float boxWidth = 1600;
        float boxHeight = 800;

        float boxX = (w - boxWidth) / 2;
        float boxY = (h - boxHeight) / 2;

        if (showSaveConfirm)
        {
            GUI.Box(new Rect(boxX, boxY, boxWidth, boxHeight), "Save Map?", style);

            if (GUI.Button(new Rect(boxX + 350, boxY + 450, 300, 120), "Yes", buttonStyle))
            {
                SaveMap();
                showSaveConfirm = false;
            }

            if (GUI.Button(new Rect(boxX + 950, boxY + 450, 300, 120), "No", buttonStyle))
            {
                showSaveConfirm = false;
            }
        }

        if (showLoadConfirm)
        {
            GUI.Box(new Rect(boxX, boxY, boxWidth, boxHeight), "Load Map?", style);

            if (GUI.Button(new Rect(boxX + 350, boxY + 450, 300, 120), "Yes", buttonStyle))
            {
                LoadMap();
                showLoadConfirm = false;
            }

            if (GUI.Button(new Rect(boxX + 950, boxY + 450, 300, 120), "No", buttonStyle))
            {
                showLoadConfirm = false;
            }
        }
    }

    //===========================-------
    //　　　　　   Gizmos
    //===========================-------
    void OnDrawGizmos()
    {
        Gizmos.color = Color.green;

        foreach (var tile in tiles)
        {
            Vector3 pos = new Vector3(
                tile.Key.x * gridSize,
                tile.Key.y * gridSize,
                tile.Key.z * gridSize
            );

            Gizmos.DrawWireCube(pos, Vector3.one * gridSize);
        }
    }

}


