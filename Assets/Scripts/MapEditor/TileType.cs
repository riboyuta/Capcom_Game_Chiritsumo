using UnityEngine;

public class TileType : MonoBehaviour
{
    public TileTypeEnum type;
    public TileGimmickTypeEnum gimmickType;
    public TileGimmickIDEnum gimmickID;
}

public enum TileTypeEnum
{
    Block01,
    Block02,
    Block03,
    Block04,
    Block05,
    Block06,
    Block07, 
    Block08,
    Block09,

    
    MAX
}


public enum TileGimmickTypeEnum
{
    None,

    Switch,    //スイッチ
    SlideWall, //スライドする壁
    Breakable, //壊れる床
    Spring,    //ばね

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
    ID_010,
    ID_011,
    ID_012,
    ID_013,
    ID_014,
    ID_015,
    ID_016,
    ID_017,
    ID_018,
    ID_019,
    ID_020
}