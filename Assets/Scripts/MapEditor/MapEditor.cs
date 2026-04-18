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

    List<TileData> copyBuffer = new List<TileData>(); //コピーバッファ
    Vector3Int copyOrigin;

    List<TileData> temporaryBuffer = new List<TileData>(); //範囲選択用の一時保存バッファ
    Vector3Int temporaryOrigin;

    Stack<MapData> undoStack = new Stack<MapData>(); //変更履歴のスタック

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

    [Header("マップフォルダ")]
    [Tooltip("現在選択されているファイルのマップに読み書きします")]
    [SerializeField] private string mapFolder = "MapData";



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

    //フォルダ定義
    string folderPath =>
  Application.dataPath + "/" + mapFolder;

    //完全パス
    string FilePath()
    {
        return folderPath + "/Stage_" + stageNumber + ".json";
    }

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

            //タイル配置
            if (Input.GetMouseButton(0))
            {
                PlaceTile();
            }

            //タイル削除
            if (Input.GetMouseButton(1))
            {
                RemoveTile();
            }

            //セーブ
            if (Input.GetKeyDown(KeyCode.S))
            {
                //SaveMap();
                showSaveConfirm = true;
            }

            //ロード
            if (Input.GetKeyDown(KeyCode.L))
            {
                //LoadMap();
                showLoadConfirm = true;
            }

            //取り消し
            if (Input.GetKeyDown(KeyCode.Z))
            {
                Undo();
            }


            //範囲選択処理
            SelectRange();

            //範囲選択消去処理
            RemoveSelectRange();


            //コピー処理
            if (Input.GetKeyDown(KeyCode.C))
            {

                //一時保存バッファになにも存在しない場合
                if (temporaryBuffer.Count <= 0)
                {
                    Debug.Log("コピーに失敗しました：一時保存されていません");
                    return;
                }

                //selectingがtrueの場合
                if (selecting)
                {
                    Debug.Log("コピーに失敗しました：選択中です！");
                    return;
                }

                //まずは履歴に保存
                SaveUndo();

                //一時保存をコピーする
                copyBuffer.Clear();
                copyBuffer.AddRange(temporaryBuffer);

                copyOrigin = temporaryOrigin;

                Debug.Log("コピーしました");

            }



            //ペースト処理
            ExecutePaste();        

         


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


        //キーを押された直後ならUndoスタックをセーブ
        if (Input.GetMouseButtonDown(0))
        {
            SaveUndo();
        }

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

        //キーを押された直後ならUndoスタックをセーブ
        if (Input.GetMouseButtonDown(1))
        {
            SaveUndo();
        }

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


    [ContextMenu("Clear Undo (Editor)")]
    void ClearUndoOutPlaying()
    {
        undoStack.Clear();
    }



    //===========================-------
    //　　　　　 範囲選択システム
    //===========================-------

    void SelectRange()
    {

        //コピー範囲開始
        if ((Input.GetKeyDown(KeyCode.Space)))
        {
            selecting = true;
            selectStart = GetGridPosition();
            Debug.Log("範囲選択開始");

            //一時保存バッファをクリア
            temporaryBuffer.Clear();
        }

        //ドラッグ中
        else if ((Input.GetKey(KeyCode.Space)) && selecting)
        {
            selectEnd = GetGridPosition();
            Debug.Log("範囲選択中");
        }

        //離したらコピー
        else if (Input.GetKeyUp(KeyCode.Space) && selecting)
        {
            selecting = false;

            CopyTilesTemporary(selectStart, selectEnd);

            Debug.Log("一時保存が完了しました");
        }


    }


    void RemoveSelectRange()
    {
        if (Input.GetKeyDown(KeyCode.Backspace))
        {

            //一時保存バッファになにも存在しない場合
            if (temporaryBuffer.Count <= 0)
            {
                Debug.Log("範囲消去に失敗しました：一時保存されていません");
                return;
            }

            //selectingがtrueの場合
            if (selecting)
            {
                Debug.Log("範囲選択に失敗しました：選択中です！");
                return;
            }

            //まずは履歴に保存
            SaveUndo();

            // 　範囲選択を削除する
            foreach (TileData data in temporaryBuffer)
            {
                Vector3Int gridPos = new Vector3Int(
                    temporaryOrigin.x + data.x,
                    temporaryOrigin.y + data.y,
                    temporaryOrigin.z + data.z
                );

                if (!tiles.ContainsKey(gridPos))
                    continue;

                GameObject tile = tiles[gridPos];

                if (Application.isPlaying)
                    Destroy(tile);
                else
                    DestroyImmediate(tile);

                tiles.Remove(gridPos);
            }

            temporaryBuffer.Clear();
            Debug.Log("範囲削除完了");



        }
    }


    //===========================-------
    //　　　　　 履歴システム
    //===========================-------

    void SaveUndo()
    {
        MapData snapshot = new MapData();

        foreach (var tile in tiles)
        {
            TileData data = new TileData();

            data.x = tile.Key.x;
            data.y = tile.Key.y;
            data.z = tile.Key.z;

            TileType type = tile.Value.GetComponent<TileType>();

            data.type = type.type;
            data.gimmickType = type.gimmickType;
            data.gimmickID = type.gimmickID;

            snapshot.tiles.Add(data);
        }

        undoStack.Push(snapshot);

    }



    void Undo()
    {
        if (undoStack.Count == 0)
        {
            Debug.Log("Undoできません");
            return;
        }

        MapData mapData = undoStack.Pop();

        foreach (var tile in tiles)
        {
            Destroy(tile.Value);
        }

        tiles.Clear();

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

        Debug.Log("Undoしました");
    }



    //===========================-------
    //　　　　　 コピーシステム (現在この関数は未使用)
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

    void ExecutePaste()
    {
        if (Input.GetKeyDown(KeyCode.V))
        {

            //コピーバッファになにも存在しない場合
            if (copyBuffer.Count <= 0)
            {
                Debug.Log("ペーストに失敗しました：コピーがされていません");
                return;
            }

            //selectingがtrueの場合
            if (selecting)
            {
                Debug.Log("コピーに失敗しました：選択中です！");
                return;
            }

            //まずは履歴に保存
            SaveUndo();

            //コピーをペーストする
            PasteTiles(GetGridPosition());

        }
    }


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
    //　　　　　 一時コピーシステム
    //===========================-------

    void CopyTilesTemporary(Vector3Int start, Vector3Int end)
    {
        temporaryBuffer.Clear();

        int minX = Mathf.Min(start.x, end.x);
        int maxX = Mathf.Max(start.x, end.x);

        int minY = Mathf.Min(start.y, end.y);
        int maxY = Mathf.Max(start.y, end.y);

        int minZ = Mathf.Min(start.z, end.z);
        int maxZ = Mathf.Max(start.z, end.z);

        temporaryOrigin = new Vector3Int(minX, minY, minZ);

        foreach (var tile in tiles)
        {
            Vector3Int pos = tile.Key;

            if (pos.x >= minX && pos.x <= maxX &&
                pos.y >= minY && pos.y <= maxY &&
                pos.z >= minZ && pos.z <= maxZ)
            {
                TileType type = tile.Value.GetComponent<TileType>();

                TileData data = new TileData();

                data.x = pos.x - temporaryOrigin.x;
                data.y = pos.y - temporaryOrigin.y;
                data.z = pos.z - temporaryOrigin.z;

                data.type = type.type;
                data.gimmickType = type.gimmickType;
                data.gimmickID = type.gimmickID;

                temporaryBuffer.Add(data);
            }
        }

        Debug.Log("Temporary Copied Tiles : " + temporaryBuffer.Count);
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

        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }

        File.WriteAllText(FilePath(), json);

        Debug.Log("Map Saved : " + FilePath());
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





        if (!File.Exists(FilePath()))
        {
            Debug.Log("Map file not found");
            return;
        }

        string json = File.ReadAllText(FilePath());

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

        if (selecting)
        {
            Gizmos.color = Color.yellow;

            Vector3 center = ((Vector3)selectStart + (Vector3)selectEnd) / 2f * gridSize;
            Vector3 size = new Vector3(
                Mathf.Abs(selectEnd.x - selectStart.x) + 1,
                Mathf.Abs(selectEnd.y - selectStart.y) + 1,
                Mathf.Abs(selectEnd.z - selectStart.z) + 1
            ) * gridSize;

            Gizmos.DrawWireCube(center, size);
        }
    }

}


