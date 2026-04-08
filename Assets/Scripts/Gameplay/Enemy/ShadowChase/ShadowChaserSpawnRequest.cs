using UnityEngine;

// ShadowChaserEnemy を起動する時の出現情報。
// トリガーごとに異なる出現位置・向きを渡すために使う。
public struct ShadowChaserSpawnRequest
{
    // 出現位置
    public Vector3 position;
    // 出現時の回転
    public Quaternion rotation;

    public ShadowChaserSpawnRequest(Vector3 position, Quaternion rotation)
    {
        this.position = position;
        this.rotation = rotation;
    }
}