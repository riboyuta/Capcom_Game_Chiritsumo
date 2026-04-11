using UnityEngine;
using UnityEngine.InputSystem;

public sealed class ProximityEffectController : MonoBehaviour
{
    [Header("設定")]
    [Tooltip("接近エフェクトの設定です。")]
    [SerializeField] private ProximitySettings settings = new ProximitySettings();

    private Transform playerTransform;
    private PlayerController cachedPlayerController;
    private bool isProximityRumbling;
    private bool isActive;

    // エフェクト全体の有効/無効を制御
    public bool IsActive
    {
        get => isActive;
        set
        {
            isActive = value;
            // 無効化時は振動を停止
            if (!isActive)
            {
                StopProximityRumbleIfNeeded();
            }
        }
    }

    private void OnValidate()
    {
        settings?.Validate();
    }

    private void Update()
    {
        // 無効状態ならエフェクトを停止して終了
        if (!isActive)
        {
            StopProximityRumbleIfNeeded();
            return;
        }

        // エフェクトを毎フレーム更新
        UpdateEffects();
    }

    private void OnDisable()
    {
        // コンポーネント無効化時に振動を確実に停止
        StopProximityRumbleIfNeeded();
    }

    public void SetPlayerTarget(Transform player)
    {
        // プレイヤーの参照を設定
        playerTransform = player;
        // プレイヤーが変わったのでキャッシュをクリア
        cachedPlayerController = null;
    }

    private void UpdateEffects()
    {
        if (playerTransform == null)
        {
            StopProximityRumbleIfNeeded();
            return;
        }

        // プレイヤーとの距離から強度を計算
        float dx = Mathf.Abs(playerTransform.position.x - transform.position.x);
        float intensity = CalculateIntensity(dx);

        // カメラシェイク
        if (settings.shakeProfile != null)
        {
            Capcom_Game_Chiritsumo.Camera.CameraShake.CameraShakeManager.Instance?.SetContinuousIntensity(intensity, settings.shakeProfile);
        }

        // Vignette（暗転と脈打ち）
        Capcom_Game_Chiritsumo.Camera.VignetteEffects.VignetteManager.Instance?.SetContinuousIntensity(intensity);

        // コントローラー振動
        UpdateProximityRumble(intensity);
    }

    // 距離から接近エフェクトの強度を計算（0.0～1.0）
    private float CalculateIntensity(float distance)
    {
        // 最小距離より遠ければ効果なし
        if (distance > settings.minShakeDistance)
        {
            return 0f;
        }

        // 距離に応じて強度を線形補間
        return 1.0f - Mathf.InverseLerp(settings.maxShakeDistance, settings.minShakeDistance, distance);
    }

    private void UpdateProximityRumble(float intensity)
    {
        // 振動が無効、またはゲームパッドが接続されていない場合は停止
        if (!settings.enableRumble || Gamepad.current == null)
        {
            StopProximityRumbleIfNeeded();
            return;
        }

        // プレイヤーが死亡状態なら振動停止
        if (cachedPlayerController == null && playerTransform != null)
        {
            cachedPlayerController = playerTransform.GetComponentInChildren<PlayerController>();
        }

        if (cachedPlayerController != null && cachedPlayerController.IsDeadState)
        {
            StopProximityRumbleIfNeeded();
            return;
        }

        // 強度が低すぎる場合は停止
        if (intensity <= 0.01f)
        {
            StopProximityRumbleIfNeeded();
            return;
        }

        // 強度に応じた振動を設定
        float low = settings.maxLowFrequency * intensity;
        float high = settings.maxHighFrequency * intensity;
        Gamepad.current.SetMotorSpeeds(low, high);
        isProximityRumbling = true;
    }

    private void StopProximityRumbleIfNeeded()
    {
        // 振動中で、かつゲームパッドが接続されている場合のみ停止
        if (isProximityRumbling && Gamepad.current != null)
        {
            Gamepad.current.ResetHaptics();
            isProximityRumbling = false;
        }
    }
}
