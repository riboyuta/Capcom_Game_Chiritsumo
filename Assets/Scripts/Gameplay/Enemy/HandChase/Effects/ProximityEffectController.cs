using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// プレイヤー接近時のエフェクト（カメラシェイク・Vignette・コントローラー振動）を制御するコンポーネント。
/// </summary>
public sealed class ProximityEffectController : MonoBehaviour
{
    [Header("設定")]
    [Tooltip("接近エフェクトの設定です。")]
    [SerializeField] private ProximitySettings settings = new ProximitySettings();

    private Transform playerTransform;
    private PlayerController cachedPlayerController;
    private bool isProximityRumbling;
    private bool isActive;

    public bool IsActive
    {
        get => isActive;
        set
        {
            isActive = value;
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
        if (!isActive)
        {
            StopProximityRumbleIfNeeded();
            return;
        }

        UpdateEffects();
    }

    private void OnDisable()
    {
        StopProximityRumbleIfNeeded();
    }

    public void SetPlayerTarget(Transform player)
    {
        playerTransform = player;
        cachedPlayerController = null;
    }

    private void UpdateEffects()
    {
        if (playerTransform == null)
        {
            StopProximityRumbleIfNeeded();
            return;
        }

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

    private float CalculateIntensity(float distance)
    {
        if (distance > settings.minShakeDistance)
        {
            return 0f;
        }

        return 1.0f - Mathf.InverseLerp(settings.maxShakeDistance, settings.minShakeDistance, distance);
    }

    private void UpdateProximityRumble(float intensity)
    {
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

        if (intensity <= 0.01f)
        {
            StopProximityRumbleIfNeeded();
            return;
        }

        float low = settings.maxLowFrequency * intensity;
        float high = settings.maxHighFrequency * intensity;
        Gamepad.current.SetMotorSpeeds(low, high);
        isProximityRumbling = true;
    }

    private void StopProximityRumbleIfNeeded()
    {
        if (isProximityRumbling && Gamepad.current != null)
        {
            Gamepad.current.ResetHaptics();
            isProximityRumbling = false;
        }
    }
}
