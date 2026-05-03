using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[CreateAssetMenu(menuName = "MapEditor/TileDatabase")]
public class TileDatabase : ScriptableObject
{
    public List<TileDefinition> tiles = new List<TileDefinition>(100);


    //–ŒÌ–h~‚Ì‰‰ñ“o˜^
#if UNITY_EDITOR
    private void OnValidate()
    {
        foreach (var tile in tiles)
        {


            // IDi‰‰ñ‚¾‚¯j
            if (tile.prefab != null && string.IsNullOrEmpty(tile.tileID))
            {
                tile.tileID = tile.prefab.name;
            }

            // ƒAƒCƒRƒ“©“®¶¬
            if (tile.icon == null && tile.prefab != null)
            {
                tile.icon = AssetPreview.GetAssetPreview(tile.prefab);
            }
        }
    }
#endif


}

