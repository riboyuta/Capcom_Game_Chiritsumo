using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using static UnityEditor.Rendering.InspectorCurveEditor;

[ExecuteAlways]
public class MapEditor : MonoBehaviour
{

    Dictionary<Vector3Int, GameObject> tiles =
        new Dictionary<Vector3Int, GameObject>();

    List<TileData> copyBuffer = new List<TileData>(); 
    Vector3Int copyOrigin;

    //範囲選択用
    bool selecting = false;
    Vector3Int selectStart;
    Vector3Int selectEnd;

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

            //範囲選択処理
            {
                ////コピー範囲開始
                //if (Input.GetMouseButtonDown(2)) //ホイールクリック
                //{
                //    selecting = true;
                //    selectStart = GetGridPosition();
                //}

                ////ドラッグ中
                //if (Input.GetMouseButton(2) && selecting)
                //{
                //    selectEnd = GetGridPosition();
                //}

                ////離したらコピー
                //if (Input.GetMouseButtonUp(2) && selecting)
                //{
                //    selecting = false;

                //    CopyTiles(selectStart, selectEnd);

                //    Debug.Log("Copy Complete");
                //}
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




    //===========================-------
    //　　　　　 コピーシステム
    //===========================-------

    void CopyTiles(Vector3Int start, Vector3Int end)
    {
        copyBuffer.Clear();

        int minX = Mathf.Min(start.x, end.x);
        int maxX = Mathf.Max(start.x, end.x);

        int minY = Mathf.Min(start.y, end.y);
        int maxY = Mathf.Max(start.y, end.y);

        int minZ = Mathf.Min(start.z, end.z);
        int maxZ = Mathf.Max(start.z, end.z);

        copyOrigin = new Vector3Int(minX, minY, minZ);

        foreach (var tile in tiles)
        {
            Vector3Int pos = tile.Key;

            if (pos.x >= minX && pos.x <= maxX &&
                pos.y >= minY && pos.y <= maxY &&
                pos.z >= minZ && pos.z <= maxZ)
            {
                TileType type = tile.Value.GetComponent<TileType>();

                TileData data = new TileData();

                data.x = pos.x - copyOrigin.x;
                data.y = pos.y - copyOrigin.y;
                data.z = pos.z - copyOrigin.z;

                data.type = type.type;
                data.gimmickType = type.gimmickType;
                data.gimmickID = type.gimmickID;

                copyBuffer.Add(data);
            }
        }

        Debug.Log("Copied Tiles : " + copyBuffer.Count);
    }

    //===========================-------
    //　　　　　 ペーストシステム
    //===========================-------

    void PasteTiles(Vector3Int targetPos)
    {
        foreach (TileData data in copyBuffer)
        {
            Vector3Int gridPos = new Vector3Int(
                targetPos.x + data.x,
                targetPos.y + data.y,
                targetPos.z + data.z
            );

            if (tiles.ContainsKey(gridPos))
                continue;

            Vector3 spawnPos = new Vector3(
                gridPos.x * gridSize,
                gridPos.y * gridSize,
                gridPos.z * gridSize - 0.01f
            );

            GameObject tile =
                Instantiate(tilePrefab[(int)data.type], spawnPos, Quaternion.identity);

            TileType tileType = tile.GetComponent<TileType>();

            tileType.type = data.type;
            tileType.gimmickType = data.gimmickType;
            tileType.gimmickID = data.gimmickID;

            tiles.Add(gridPos, tile);
        }

        Debug.Log("Paste Complete");
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
        public TileGimmickTypeEnum gimmickType;
        public TileGimmickIDEnum gimmickID;

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
            data.gimmickType = tile.Value.GetComponent<TileType>().gimmickType;
            data.gimmickID = tile.Value.GetComponent<TileType>().gimmickID;


            mapData.tiles.Add(data);
        }

        string json = JsonUtility.ToJson(mapData, true);

        string folder = Application.dataPath +
"/Scenes/DebugScenes/koki/DebugMapScenes/DebugMapEditorScene/DebugMapEditor_MapData";
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
"/Scenes/DebugScenes/koki/DebugMapScenes/DebugMapEditorScene/DebugMapEditor_MapData";
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

            TileType tileType = tile.GetComponent<TileType>();

            tileType.type = data.type;
            tileType.gimmickType = data.gimmickType;
            tileType.gimmickID = data.gimmickID;

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


