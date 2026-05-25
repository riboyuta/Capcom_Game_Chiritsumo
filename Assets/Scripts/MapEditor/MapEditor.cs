using Capcom_Game_Chiritsumo.Camera.CameraShake;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using static MapEditor;
using static UnityEditor.Rendering.InspectorCurveEditor;

[ExecuteAlways]
public class MapEditor : MonoBehaviour
{

    [System.Serializable] //タイル個々のクラス
    public class TileData
    {
        public int x;
        public int y;
        public int z;

        public string tileID;
        public TileGimmickTypeEnum gimmickType;
        public TileGimmickIDEnum gimmickID;

    }

    [System.Serializable] //マップ全体のクラス
    public class MapData
    {
        public List<TileData> tiles = new List<TileData>();
        public string comment;
    }

    [System.Serializable] //変更できるグリッドサイズ
    public enum GridSizeType
    {
        Normal = 0,
        Quarter = 1
    }

    //グリッドサイズ変換
    private float gridSize
    {
        get
        {
            switch (gridSizeType)
            {
                case GridSizeType.Quarter: return 0.25f;
                default: return 1.0f;
            }
        }
    }


    Dictionary<Vector3Int, GameObject> tiles =
        new Dictionary<Vector3Int, GameObject>();

    private Dictionary<Vector2Int, List<GameObject>> chunks
    = new Dictionary<Vector2Int, List<GameObject>>();

    List<TileData> copyBuffer = new List<TileData>(); //コピーバッファ
    Vector3Int copyOrigin;

    List<TileData> temporaryBuffer = new List<TileData>(); //範囲選択用の一時保存バッファ
    Vector3Int temporaryOrigin;

    Stack<MapData> undoStack = new Stack<MapData>(); //変更履歴のスタック


    [Header("現在のステージ番号")]
    [Tooltip("現在編集しているステージの番号")]
    [SerializeField] private int stageNumber = 1;

    [Header("マップフォルダ")]
    [Tooltip("現在選択されているファイルのマップに読み書きします")]
    [SerializeField] private string mapFolder = "MapData";

    //[Header("グリッドサイズ")]
    //[Tooltip("配置するグリッド間隔を変更できます")]
    [SerializeField] private GridSizeType gridSizeType = GridSizeType.Normal;

    [Header("描写範囲")]
    [Tooltip("描写するチャンクの範囲の係数を変更できます。値が大きいほど広がります。")]
    [SerializeField] private float chunkDrawDistance = 1.0f;

    [Header("コメント")]
    [Tooltip("ステージ毎に保存されるメモ書き")]
    [TextArea(15, 30)]
    [SerializeField] private string comment;


    [Header("カメラシェイクプロファイル")]
    [Tooltip(" //マップエディタのデバッグ用カメラシェイクのプロファイルセット")]
    [SerializeField] private CameraShakeProfile CSMapeditorTestProfile;

    [Header("TileDatabase")]
    [Tooltip("TileDatabaseスクリプトをここにドラッグしてね")]
    [SerializeField] private TileDatabase tileDatabase;


    [SerializeField] private int chunkSize = 16;

    bool showSaveConfirm = false;
    bool showLoadConfirm = false;
    bool showPrefabConfirm = false;

    private bool IsLoaded = false;
    private bool FinalSaveCheck = false;

    private int currentPage = 0;
    private int selectedKeyNumber = 0;
    private int selectedIndex;
    private TileDefinition currentTile;

    bool selecting = false;
    Vector3Int selectStart;
    Vector3Int selectEnd;

    bool canPlaceTile = true;
    int PlaceTileFlagTimer = 0;

    bool canGridVisualization = false;





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

            if (showSaveConfirm || showLoadConfirm) { return; } //セーブ、ロード選択中はエディット操作できない

            //数字キーで使うタイルを変える
            if (Input.GetKeyDown(KeyCode.Alpha0) || Input.GetKeyDown(KeyCode.Keypad0)) selectedKeyNumber = 0;
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) selectedKeyNumber = 1;
            if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) selectedKeyNumber = 2;
            if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) selectedKeyNumber = 3;
            if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4)) selectedKeyNumber = 4;
            if (Input.GetKeyDown(KeyCode.Alpha5) || Input.GetKeyDown(KeyCode.Keypad5)) selectedKeyNumber = 5;
            if (Input.GetKeyDown(KeyCode.Alpha6) || Input.GetKeyDown(KeyCode.Keypad6)) selectedKeyNumber = 6;
            if (Input.GetKeyDown(KeyCode.Alpha7) || Input.GetKeyDown(KeyCode.Keypad7)) selectedKeyNumber = 7;
            if (Input.GetKeyDown(KeyCode.Alpha8) || Input.GetKeyDown(KeyCode.Keypad8)) selectedKeyNumber = 8;
            if (Input.GetKeyDown(KeyCode.Alpha9) || Input.GetKeyDown(KeyCode.Keypad9)) selectedKeyNumber = 9;

            selectedIndex = currentPage * 10 + selectedKeyNumber;




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

            //プレハブ一覧
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                showPrefabConfirm = !showPrefabConfirm;
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


            //Gizmo表示
            {
                //範囲選択時ライン表示

                RangeVisualization();

                //グリッドライン表示
                GridVisualization();
            }

           

            //グリッドライン表示切り替え
            if (Input.GetKeyDown(KeyCode.N))
            {
                canGridVisualization = !canGridVisualization;
            }

            //チャンク描写
            ChunkVisibilityUpdate();

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



            // タイル設置制限のクールタイム減少
            if (!canPlaceTile)
            {
                PlaceTileFlagTimer--;
                Debug.Log("a");

                if (PlaceTileFlagTimer <= 0)
                {
                    canPlaceTile = true;
                    PlaceTileFlagTimer = 0;
                }
            }


          

            //カメラシェイクのテスト
            if (Input.GetKeyDown(KeyCode.B))
            {
                Debug.Log("カメラシェイク実行");
                CameraShakeManager.Instance.ExecuteImpulseShake(CSMapeditorTestProfile);
            }



        }
    

        //実行外
        else
        {
            
        }


    }



    //グリッド取得関数
    Vector3Int GetGridPosition()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit))
        {

            Vector3 pos = hit.point;

            int gridX = Mathf.RoundToInt(pos.x / gridSize);
            int gridY = Mathf.RoundToInt(pos.y / gridSize);
            int gridZ = 0; // Mathf.RoundToInt(pos.z / gridSize);

            return new Vector3Int(gridX, gridY, gridZ);
        }


        return new Vector3Int(-10, -10, -10);
    }



    void PlaceTile()
    {
        if (showPrefabConfirm)
        {
            Debug.LogWarning("プレハブ選択中です！");
            return;
        }


        if (!canPlaceTile)
        {
            Debug.LogWarning("[canPlaceTile]がfalseです！");
            return;
        }

        if (currentTile == null)
        {
            Debug.LogWarning("タイルが選択されてない！");
            return;
        }

        Vector3Int gridPos = GetGridPosition();
        if (tiles.ContainsKey(gridPos)) return; //同じ場所だった場合はreturn

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


        TileType prefabTileType =
        currentTile.prefab.GetComponent<TileType>();

        if (prefabTileType == null)
        {
            Debug.LogError("TileTypeが付いてない！");
            return;
        }

        GameObject tile = Instantiate(currentTile.prefab, spawnPos, Quaternion.identity);
        TileType tileType = tile.GetComponent<TileType>();
        tileType.tileDefinition = currentTile;

        tiles.Add(gridPos, tile);
        RegisterChunk(gridPos, tile);


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

        GameObject tile = tiles[gridPos]; 
        UnregisterChunk(gridPos, tile);

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


    void SetTilePlacePrevent(int timer)
    {
        canPlaceTile = false;
        PlaceTileFlagTimer = timer;
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
        chunks.Clear();

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
            //一時保存バッファをクリア
            temporaryBuffer.Clear();

            selecting = true;
            selectStart = GetGridPosition();
            Debug.Log("範囲選択開始");

           
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
                UnregisterChunk(gridPos, tile);

                if (Application.isPlaying)  Destroy(tile);
                else DestroyImmediate(tile);

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


            data.tileID = type.tileDefinition.tileID;
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
        chunks.Clear();

        foreach (TileData data in mapData.tiles)
        {
            Vector3Int gridPos = new Vector3Int(data.x, data.y, data.z);

            Vector3 spawnPos = new Vector3(
                data.x * gridSize,
                data.y * gridSize,
                data.z * gridSize - 0.01f
            );


            //定義されてるタイルを探す
            TileDefinition def = tileDatabase.tiles.Find(t => t.tileID == data.tileID);
            if (def == null)
            {
                Debug.LogError("TileIDが見当たりませぬ" + data.tileID);
                continue;
            }

            GameObject tile = Instantiate(def.prefab, spawnPos, Quaternion.identity);
            TileType tileType = tile.GetComponent<TileType>();

            tileType.tileDefinition = def;
            tileType.gimmickType = data.gimmickType;
            tileType.gimmickID = data.gimmickID;

            tiles.Add(gridPos, tile);
            RegisterChunk(gridPos, tile);
        }

        Debug.Log("Undoしました");
    }


    //===========================-------
    //　　　 チャンク描画システム
    //===========================-------


    // チャンク座標の生成・登録
    void RegisterChunk(Vector3Int gridpos, GameObject tile)
    {
        Vector2Int chunkPos = new Vector2Int(
        Mathf.FloorToInt((float)gridpos.x / chunkSize),
        Mathf.FloorToInt((float)gridpos.y / chunkSize)
        );

        if (!chunks.ContainsKey(chunkPos))
        {
            chunks.Add(chunkPos, new List<GameObject>());
        }

        chunks[chunkPos].Add(tile);
    }

    // チャンク座標の削除
    void UnregisterChunk(Vector3Int gridpos, GameObject tile)
    {
        Vector2Int chunkPos = new Vector2Int(
        Mathf.FloorToInt((float)gridpos.x / chunkSize),
        Mathf.FloorToInt((float)gridpos.y / chunkSize)
        );

        if (chunks.ContainsKey(chunkPos))
        {
            chunks[chunkPos].Remove(tile);
            if (chunks[chunkPos].Count == 0)
            {
                chunks.Remove(chunkPos);
            }
        }
    }

    //　チャンク毎の描写アップデート
    void ChunkVisibilityUpdate()
    {

        float cameraZ = Mathf.Abs(Camera.main.transform.position.z);
        float distance = cameraZ * chunkDrawDistance;
        if (distance < 1.0f) { distance = 1.0f; }

        Vector3 bottomLeft =
            Camera.main.ViewportToWorldPoint(
                new Vector3(0, 0, distance));

        Vector3 topRight =
            Camera.main.ViewportToWorldPoint(
                new Vector3(1, 1, distance));

      

        int minChunkX =
            Mathf.FloorToInt(bottomLeft.x / chunkSize);

        int maxChunkX =
            Mathf.FloorToInt(topRight.x / chunkSize);

        int minChunkY =
            Mathf.FloorToInt(bottomLeft.y / chunkSize);

        int maxChunkY =
            Mathf.FloorToInt(topRight.y / chunkSize);

        foreach (var chunk in chunks)
        {
            Vector2Int chunkPos = chunk.Key;

            bool visible =
                chunkPos.x >= minChunkX &&
                chunkPos.x <= maxChunkX &&
                chunkPos.y >= minChunkY &&
                chunkPos.y <= maxChunkY;

            foreach (GameObject tile in chunk.Value)
            {
                if (tile != null)
                {
                    tile.SetActive(visible);
                }
            }
        }
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

                data.tileID = type.tileDefinition.tileID;
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


            //定義されてるタイルを探す
            TileDefinition def = tileDatabase.tiles.Find(t => t.tileID == data.tileID);
            if (def == null)
            {
                Debug.LogError("TileIDが見当たりませぬ" + data.tileID);
                continue;
            }

            GameObject tile = Instantiate(def.prefab, spawnPos, Quaternion.identity);
            TileType tileType = tile.GetComponent<TileType>();

            tileType.tileDefinition = def;
            tileType.gimmickType = data.gimmickType;
            tileType.gimmickID = data.gimmickID;

            tiles.Add(gridPos, tile);
            RegisterChunk(gridPos, tile);
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

                data.tileID = type.tileDefinition.tileID;
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
            TileType type = tile.Value.GetComponent<TileType>();

            data.tileID = type.tileDefinition.tileID;
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
        chunks.Clear();

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


            //定義されてるタイルを探す
            TileDefinition def = tileDatabase.tiles.Find(t => t.tileID == data.tileID);
            if (def == null)
            {
                Debug.LogError("TileIDが見当たりませぬ" + data.tileID);
                continue;
            }

            GameObject tile = Instantiate(def.prefab, spawnPos, Quaternion.identity);
            TileType tileType = tile.GetComponent<TileType>();

            tileType.tileDefinition = def;
            tileType.gimmickType = data.gimmickType;
            tileType.gimmickID = data.gimmickID;

            tiles.Add(gridPos, tile);
            RegisterChunk(gridPos, tile);
        }


        if (!IsLoaded) IsLoaded = true;
        Debug.Log("Map Loaded :" + FilePath());
    }


    

    //現在のブロックをレジストとして登録する
    void RegisterExistingTiles()
    {
        tiles.Clear();
        chunks.Clear();

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
                RegisterChunk(gridPos, tile.gameObject);
            }
        }
    }



    //===========================-------
    //　　　　　   GUI
    //===========================-------
    void OnGUI()
    {
        // ===== セーブ＆ロードUI =====

        GUIStyle style = new GUIStyle(GUI.skin.box);
        style.fontSize = 160;
        style.alignment = TextAnchor.UpperCenter;

        GUIStyle buttonStyle = new GUIStyle(GUI.skin.button);
        buttonStyle.fontSize = 80;

        GUIStyle warningButtonStyle = new GUIStyle(GUI.skin.button);
        warningButtonStyle.fontSize = 80;
        warningButtonStyle.normal.textColor = Color.red;







   

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
                if (!IsLoaded)
                {
                    FinalSaveCheck = true;
                    showSaveConfirm = false;
                    return;
                }
              
                SaveMap();
                showSaveConfirm = false;
                
            }

            if (GUI.Button(new Rect(boxX + 950, boxY + 450, 300, 120), "No", buttonStyle))
            {
                showSaveConfirm = false;
                
            }

        }

        if (FinalSaveCheck)
        {
            Debug.Log("GUI.FinalSaveCheck");

            style.fontSize = 120;

            GUI.Box(new Rect(boxX, boxY, boxWidth + 100, boxHeight + 100), "まだロードされてないよ！？", style);
            if (GUI.Button(new Rect(boxX + 200, boxY + 180, 1200, 100), "それでも上書き保存する！", warningButtonStyle))
            {

                SaveMap();
                IsLoaded = true;
                FinalSaveCheck = false;

            }

            if (GUI.Button(new Rect(boxX + 200, boxY + 290, 1200, 120), "キャンセル", buttonStyle))
            {
                FinalSaveCheck = false;
            }

            if (GUI.Button(new Rect(boxX + 200, boxY + 450, 1200, 360), "キャンセル", buttonStyle))
            {
                FinalSaveCheck = false;
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

        // ===== プレハブ選択ホットバーUI =====

        if (showSaveConfirm || showLoadConfirm) return;
         
        if (showPrefabConfirm)
        {

            int tileCount = tileDatabase.tiles.Count;

            float size = 100;
            float margin = 10;

            float startX = 50;
            float startY = Screen.height - 150;

            for (int i = 0; i < 10; i++)
            {
                int index = currentPage * 10 + i;

                if (index >= tileCount) break;

                Rect rect = new Rect(startX + i * (size + margin), startY, size, size);

                TileDefinition def = tileDatabase.tiles[index];

                // ボタン（アイコン表示）
                if (GUI.Button(rect, def.icon != null ? def.icon : Texture2D.grayTexture))
                {
                    selectedKeyNumber = i;
                    currentTile = def;
                    showPrefabConfirm = false;
                    SetTilePlacePrevent(7);
                }

                // 選択中の枠
                if (i == selectedKeyNumber)
                {
                    GUI.color = Color.yellow;
                    GUI.Box(rect, "");
                    GUI.color = Color.white;
                }
            }

            // ===== ページ切り替え =====
            if (GUI.Button(new Rect(50, startY - 70, 100, 50), "<"))
            {
                currentPage = Mathf.Max(0, currentPage - 1);
                selectedKeyNumber = 0;
                SetTilePlacePrevent(7);

            }

            if (GUI.Button(new Rect(200, startY - 70, 100, 50), ">"))
            {
                int maxPage = (tileCount - 1) / 10;
                currentPage = Mathf.Min(maxPage, currentPage + 1);
                SetTilePlacePrevent(7);

            }
        }

      
    }

    //===========================-------
    //　　　　　   Gizmos
    //===========================-------




    void DrawWireCube(Vector3 center, Vector3 size, Color color)
    {
        Vector3 half = size / 2f;

        Vector3 p1 = center + new Vector3(-half.x, -half.y, -half.z);
        Vector3 p2 = center + new Vector3(half.x, -half.y, -half.z);
        Vector3 p3 = center + new Vector3(half.x, half.y, -half.z);
        Vector3 p4 = center + new Vector3(-half.x, half.y, -half.z);

      

        // 前
        Debug.DrawLine(p1, p2, color, 0.01f);
        Debug.DrawLine(p2, p3, color, 0.01f);
        Debug.DrawLine(p3, p4, color, 0.01f);
        Debug.DrawLine(p4, p1, color, 0.01f);
                                    
     
    }


    void GridVisualization()
    {
        if (!canGridVisualization) return;

        //foreach (var tile in tiles)
        //{
        //    Vector3 pos = new Vector3(
        //        tile.Key.x * gridSize,
        //        tile.Key.y * gridSize,
        //        tile.Key.z * gridSize
        //    );

        //    DrawWireCube(pos, Vector3.one * gridSize, Color.green);
        //}



        Vector3Int center = GetGridPosition();
        int range = 10;

        for (int x = -range; x <= range; x++)
        {
            for (int y = -range; y <= range; y++)
            {
                Vector3 pos = new Vector3(
                    (center.x + x) * gridSize,
                    (center.y + y) * gridSize,
                    (float)-0.1f
                );

                DrawWireCube(pos, Vector3.one * gridSize, Color.green);

            }
        }


    }


    void RangeVisualization()
    {
        if (selecting)
        {

            Vector3 center2 = ((Vector3)selectStart + (Vector3)selectEnd) / 2f * gridSize;
            Vector3 size = new Vector3(
                Mathf.Abs(selectEnd.x - selectStart.x) + 1,
                Mathf.Abs(selectEnd.y - selectStart.y) + 1,
                Mathf.Abs(selectEnd.z - selectStart.z) + 1
            ) * gridSize;

            DrawWireCube(center2, size, Color.yellow);
        }

        if (!selecting && temporaryBuffer != null && temporaryBuffer.Count > 0)
        {

            Vector3 center2 = ((Vector3)selectStart + (Vector3)selectEnd) / 2f * gridSize;
            Vector3 size = new Vector3(
                Mathf.Abs(selectEnd.x - selectStart.x) + 1,
                Mathf.Abs(selectEnd.y - selectStart.y) + 1,
                Mathf.Abs(selectEnd.z - selectStart.z) + 1
            ) * gridSize;

            DrawWireCube(center2, size, Color.red);
        }
    }
   

}


