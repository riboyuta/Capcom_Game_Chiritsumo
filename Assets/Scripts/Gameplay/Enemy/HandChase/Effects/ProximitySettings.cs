using UnityEngine;

[System.Serializable]
public sealed class ProximitySettings
{
    [Header("カメラシェイク")]
    [Tooltip("プレイヤー近接時に発生させるカメラ振動のプロファイル")]
    public Capcom_Game_Chiritsumo.Camera.CameraShake.CameraShakeProfile shakeProfile;

    [Tooltip("揺れが最大(強度1.0)になるプレイヤーとの距離")]
    [Min(0f)] public float maxShakeDistance = 2.0f;

    [Tooltip("揺れが発生し始める(強度0.0)プレイヤーとの距離")]
    [Min(0f)] public float minShakeDistance = 10.0f;

    [Header("コントローラー振動")]
    [Tooltip("コントローラー振動を有効にするかどうか")]
    public bool enableRumble = true;

    [Tooltip("接近時の最大低周波振動強度")]
    [Range(0f, 1f)] public float maxLowFrequency = 0.5f;

    [Tooltip("接近時の最大高周波振動強度")]
    [Range(0f, 1f)] public float maxHighFrequency = 0.5f;

    // 設定値を検証して不正な値を修正
    public void Validate()
    {
        maxShakeDistance = Mathf.Max(0f, maxShakeDistance);
        minShakeDistance = Mathf.Max(maxShakeDistance, minShakeDistance);
        maxLowFrequency = Mathf.Clamp01(maxLowFrequency);
        maxHighFrequency = Mathf.Clamp01(maxHighFrequency);
    }
}
