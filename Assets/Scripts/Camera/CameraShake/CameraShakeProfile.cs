using UnityEngine;

namespace Capcom_Game_Chiritsumo.Camera.CameraShake
{
    
    /// カメラの振動（シェイク）パラメータを保持するデータアセット

    [CreateAssetMenu(fileName = "NewCameraShakeProfile", menuName = "ScriptableObjects/CameraShake/CameraShakeProfile", order = 1)]
    public class CameraShakeProfile : ScriptableObject
    {
        [Header("===== 基本の揺れ設定 (Continuous/Impulse共通) =====")]
        [Tooltip("各軸の最大移動量 (Position)")]
        public Vector3 positionShake = new Vector3(0.5f, 0.5f, 0f);

        [Tooltip("各軸の最大回転角 (Rotation)")]
        public Vector3 rotationShake = new Vector3(2f, 2f, 2f);

        [Tooltip("揺れのスピード (パーリンノイズの進行速度)")]
        public float shakeSpeed = 20f;

        [Header("===== 単発シェイク用の設定 (Impulse専用) =====")]
        [Tooltip("揺れが持続する時間 (秒)")]
        public float duration = 0.5f;

        [Tooltip("時間経過による揺れの強さカーブ（1で開始し、0で終わる想定）")]
        public AnimationCurve dampingCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
    }
}
