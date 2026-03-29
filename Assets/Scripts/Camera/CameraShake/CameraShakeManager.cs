using System.Collections.Generic;
using UnityEngine;

namespace Capcom_Game_Chiritsumo.Camera.CameraShake
{
    /// <summary>
    /// カメラの揺れを管理・計算するマネージャー。
    /// MainCamera（CameraRootの子オブジェクト）にアタッチして使用します。
    /// </summary>
    public class CameraShakeManager : MonoBehaviour
    {
        // ============================================
        // Singleton Instance
        // ============================================
        public static CameraShakeManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Debug.LogWarning("CameraShakeManagerのインスタンスが複数存在したため破棄されました。");
                Destroy(gameObject);
                return;
            }
            
            _initialLocalPosition = transform.localPosition;
            _initialLocalRotation = transform.localRotation;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        // ============================================
        // State
        // ============================================
        private Vector3 _initialLocalPosition;
        private Quaternion _initialLocalRotation;

        // --- Continuous Shake (距離依存など、毎フレーム設定される持続的な揺れ) ---
        private float _continuousIntensity = 0f;
        private CameraShakeProfile _continuousProfile;
        private float _continuousSeed; // ノイズシード値

        // --- Impulse Shake (単発のイベント的な揺れ) ---
        private class ImpulseInstance
        {
            public CameraShakeProfile profile;
            public float timer;
            public float seed;
        }
        private List<ImpulseInstance> _activeImpulses = new List<ImpulseInstance>();


        private void Start()
        {
            _continuousSeed = Random.Range(0f, 1000f);
        }

        // ============================================
        // Public API
        // ============================================

        /// <summary>
        /// 継続的な揺れ（Enemyが近い時など）の強さを設定します。毎フレーム呼ぶ想定です。
        /// </summary>
        /// <param name="intensity">0f～1fの範囲（1で最大振動）</param>
        /// <param name="profile">揺れのパラメータを持つプロファイル</param>
        public void SetContinuousIntensity(float intensity, CameraShakeProfile profile)
        {
            _continuousIntensity = Mathf.Clamp01(intensity);
            _continuousProfile = profile;
        }

        /// <summary>
        /// 叩きつけ攻撃などの単発的な揺れを発生させます。
        /// </summary>
        /// <param name="profile">揺れのパラメータと持続時間(Duration)を持つプロファイル</param>
        public void ExecuteImpulseShake(CameraShakeProfile profile)
        {
            if (profile == null) return;

            var newImpulse = new ImpulseInstance
            {
                profile = profile,
                timer = profile.duration,
                seed = Random.Range(0f, 1000f)
            };
            _activeImpulses.Add(newImpulse);
        }


        // ============================================
        // Core Update Loop
        // ============================================
        private void LateUpdate()
        {
            Vector3 finalPosShake = Vector3.zero;
            Vector3 finalRotShake = Vector3.zero;

            // 1. Continuous（継続的）な揺れの計算
            if (_continuousProfile != null && _continuousIntensity > 0f)
            {
                var shake = CalculateNoise(_continuousProfile, _continuousSeed, Time.time);
                
                finalPosShake += shake.position * _continuousIntensity;
                finalRotShake += shake.rotation * _continuousIntensity;
                
                // 次のフレームで更新されなければ0に戻るように減衰（呼ぶ側が毎フレーム呼ぶ前提）
                _continuousIntensity = Mathf.Lerp(_continuousIntensity, 0f, Time.deltaTime * 5f);
            }

            // 2. Impulse（単発）な揺れの計算
            for (int i = _activeImpulses.Count - 1; i >= 0; i--)
            {
                var instance = _activeImpulses[i];
                instance.timer -= Time.deltaTime;

                if (instance.timer <= 0)
                {
                    _activeImpulses.RemoveAt(i);
                    continue;
                }

                // 進行度合い（0～1：開始～終了）
                float progress = 1f - (instance.timer / instance.profile.duration);
                // カーブを使用した強度
                float damping = instance.profile.dampingCurve.Evaluate(progress);

                var shake = CalculateNoise(instance.profile, instance.seed, Time.time);
                
                finalPosShake += shake.position * damping;
                finalRotShake += shake.rotation * damping;
            }

            // 3. 最終的な座標の適用（親ローカル座標からのオフセット）
            transform.localPosition = _initialLocalPosition + finalPosShake;
            transform.localRotation = _initialLocalRotation * Quaternion.Euler(finalRotShake);
        }

        /// <summary>
        /// パーリンノイズによる揺れ（X/Y/Z）を計算する共用メソッド
        /// </summary>
        private (Vector3 position, Vector3 rotation) CalculateNoise(CameraShakeProfile profile, float seed, float time)
        {
            float timeSpd = time * profile.shakeSpeed;

            // 各軸で異なるシードオフセットを加えることで、斜めだけでなくランダムに揺らす
            float posX = (Mathf.PerlinNoise(seed, timeSpd) - 0.5f) * 2f * profile.positionShake.x;
            float posY = (Mathf.PerlinNoise(seed + 100, timeSpd) - 0.5f) * 2f * profile.positionShake.y;
            float posZ = (Mathf.PerlinNoise(seed + 200, timeSpd) - 0.5f) * 2f * profile.positionShake.z;

            float rotX = (Mathf.PerlinNoise(seed + 300, timeSpd) - 0.5f) * 2f * profile.rotationShake.x;
            float rotY = (Mathf.PerlinNoise(seed + 400, timeSpd) - 0.5f) * 2f * profile.rotationShake.y;
            float rotZ = (Mathf.PerlinNoise(seed + 500, timeSpd) - 0.5f) * 2f * profile.rotationShake.z;

            return (new Vector3(posX, posY, posZ), new Vector3(rotX, rotY, rotZ));
        }
    }
}
