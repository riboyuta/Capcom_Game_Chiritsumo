using UnityEngine;

public struct HandChaserSpawnRequest
{
    // 出現位置
    public Vector3 position;
    // 出現時の回転
    public Quaternion rotation;

    public HandChaserSpawnRequest(Vector3 position, Quaternion rotation)
    {
        this.position = position;
        this.rotation = rotation;
    }
}
