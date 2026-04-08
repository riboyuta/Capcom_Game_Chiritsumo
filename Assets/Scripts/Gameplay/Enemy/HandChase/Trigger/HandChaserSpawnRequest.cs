using UnityEngine;

/// <summary>
/// HandChaser敵の出現情報。
/// トリガーごとに異なる出現位置を指定できる。
/// </summary>
public struct HandChaserSpawnRequest
{
    public Vector3 position;
    public Quaternion rotation;

    public HandChaserSpawnRequest(Vector3 position, Quaternion rotation)
    {
        this.position = position;
        this.rotation = rotation;
    }
}
