using UnityEngine;

public class TileType : MonoBehaviour
{
    [HideInInspector] public TileDefinition tileDefinition; 

    public TileGimmickTypeEnum gimmickType = TileGimmickTypeEnum.None;
    public TileGimmickIDEnum gimmickID = TileGimmickIDEnum.None;


    public void Initialize(TileDefinition definition, TileGimmickTypeEnum gimmickType, TileGimmickIDEnum gimmickID)
    {
        tileDefinition = definition;
        this.gimmickType = gimmickType;
        this.gimmickID = gimmickID;
    }
}


public enum TileGimmickTypeEnum
{
    None,

    Switch,    //スイッチ
    SlideWall, //スライドする壁
    Breakable, //壊れる床
    Spring,    //ばね
    Key, //鍵

    MAX
}

public enum TileGimmickIDEnum
{
    None,
    
    ID_01,
    ID_02,
    ID_03,
    ID_04,
    ID_05,
    ID_06,
    ID_07,
    ID_08,
    ID_09,
    ID_10,
    ID_11,
    ID_12,
    ID_13,
    ID_14,
    ID_15,
    ID_16,
    ID_17,
    ID_18,
    ID_19,
    ID_20
}


