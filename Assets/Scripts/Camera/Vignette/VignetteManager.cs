using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Capcom_Game_Chiritsumo.Camera.VignetteEffects
{
    /// シーン内のVignette（ビネット）エフェクトを動的に生成・管理し、
    /// 距離などのIntensityに基づいて画面四隅を暗くします。
    /// 自動的にGameObjectを生成するため、スクリプトから呼び出すだけで動作します。
    public class VignetteManager : MonoBehaviour
    {
        private static VignetteManager _instance;
        public static VignetteManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindAnyObjectByType<VignetteManager>();
                    if (_instance == null)
                    {
                        GameObject go = new GameObject("VignetteManager");
                        _instance = go.AddComponent<VignetteManager>();
                    }
                }
                return _instance;
            }
        }

        private Volume _volume;
        private Vignette _vignette;
        
        [Header("Vignette Settings")]
        [Tooltip("Vignetteの最大強度（0.0 ~ 1.0）")]
        public float maxVignetteIntensity = 0.5f;

        [Tooltip("Vignetteのベースカラー")]
        public Color vignetteColor = Color.black;
        
        [Header("Pulse Effect (鼓動)")]
        [Tooltip("脈を打つ強さの割合 (0.0~1.0)。\n0.5の場合、Intensityの50%分が脈打ちによって変化します。")]
        public float pulseDepth = 0.4f;

        [Tooltip("脈の最低スピード (プレイヤーが遠くIntensityが低い時)")]
        public float minPulseSpeed = 2.0f;

        [Tooltip("脈の最大スピード (プレイヤーが近くIntensityが最大の時)")]
        public float maxPulseSpeed = 8.0f;

        // 外部から受け取る継続的な揺れの強さ (0.0 ~ 1.0)
        private float _continuousIntensity = 0f;

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
                return;
            }

            SetupVignette();
        }

        private void OnDestroy()
        {
            // 動的生成したVolumeProfileがメモリに残ったままエディタが参照しようとするエラーを防ぐため破棄する
            if (_volume != null && _volume.profile != null)
            {
                Destroy(_volume.profile);
            }

            if (_instance == this)
            {
                _instance = null;
            }
        }

        private void SetupVignette()
        {
            // 動的にVolumeとProfileを生成
            _volume = gameObject.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.weight = 1f;
            
            // 既存のVolumeより優先度を高くして確実に適用させる
            _volume.priority = 100f; 
            
            VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _volume.profile = profile;

            if (!profile.TryGet(out _vignette))
            {
                _vignette = profile.Add<Vignette>(false);
            }

            _vignette.active = true;
            
            // Override State を有効にする
            _vignette.intensity.overrideState = true;
            _vignette.intensity.value = 0f;
            
            _vignette.color.overrideState = true;
            _vignette.color.value = vignetteColor;

            _vignette.smoothness.overrideState = true;
            _vignette.smoothness.value = 1.0f; // 四隅が丸く、かつスムーズに暗くなる
            
            _vignette.rounded.overrideState = true;
            _vignette.rounded.value = true;
        }

        /// 継続的なVignetteの強さ（Enemyが近い時など）を設定します。毎フレーム呼ぶ想定です。
        /// <param name="intensity">0f～1fの範囲（1で最大振動）</param>
        public void SetContinuousIntensity(float intensity)
        {
            // 複数の呼び出し元がある場合、最も大きなIntensityを採用する
            if (intensity > _continuousIntensity)
            {
                _continuousIntensity = Mathf.Clamp01(intensity);
            }
        }

        private void LateUpdate()
        {
            if (_vignette == null) return;

            if (_continuousIntensity > 0.001f)
            {
                // 心臓の鼓動となるスピード（Intensityが1に近いほど早くなる）
                float currentPulseSpeed = Mathf.Lerp(minPulseSpeed, maxPulseSpeed, _continuousIntensity);
                
                // サイン波を用いて鼓動波形を生成 (0.0 ~ 1.0 に正規化)
                float rawSin = Mathf.Sin(Time.time * currentPulseSpeed);
                
                // 二乗してシャープな波（心臓の「ドッ」）にする
                float pulseWave = Mathf.Pow(Mathf.Max(0f, rawSin), 2f); 

                // pulseWave が 0 ~ 1.0 の間で変動
                // 1fからpulseDepth分だけ引いた幅で揺れる
                // 例: pulseDepth = 0.4 なら、 0.6 〜 1.0 の間を揺れる
                float pulseMultiplier = 1f - (pulseDepth * (1f - pulseWave));

                // 実際のVignette Intensityを計算してセット
                _vignette.intensity.value = maxVignetteIntensity * _continuousIntensity * pulseMultiplier;
                
                // 動作確認用デバッグログ
                // Debug.Log($"[Vignette] ContinuousIntensity:{_continuousIntensity:F2}, Current Vignette Intensity:{_vignette.intensity.value:F2}");
                
                // 次のフレームで更新されなければ0に戻るように減衰（呼ぶ側が毎フレーム呼ぶ前提）
                _continuousIntensity = Mathf.Lerp(_continuousIntensity, 0f, Time.deltaTime * 5f);
            }
            else
            {
                // Intensityがない場合はフェードアウト
                _continuousIntensity = 0f;
                _vignette.intensity.value = Mathf.Lerp(_vignette.intensity.value, 0f, Time.deltaTime * 5f);
            }
        }
    }
}
