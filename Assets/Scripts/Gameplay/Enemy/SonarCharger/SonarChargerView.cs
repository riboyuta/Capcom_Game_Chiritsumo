using UnityEngine;

[DisallowMultipleComponent]
public sealed class SonarChargerView : MonoBehaviour
{
    // ────────────────────────────────────────
    // シリアライズフィールド
    // ────────────────────────────────────────

    [Header("見た目Root")]
    [Tooltip("揺れや向き反転を適用する見た目Rootです。Root本体ではなく子のVisualを推奨します。")]
    [SerializeField] private Transform visualRoot;

    [Header("表示制御Renderer")]
    [Tooltip("表示/非表示を切り替えるRenderer群です。未設定時は子を含めて自動取得します。")]
    [SerializeField] private Renderer[] controlledRenderers;

    [Header("向き反転")]
    [Tooltip("突進方向に応じて見た目を左右反転するかです。")]
    [SerializeField] private bool flipByDirectionX = true;

    // ────────────────────────────────────────
    // 内部状態
    // ────────────────────────────────────────

    private Transform ownerRoot;
    private Vector3 initialVisualLocalPosition;
    private Vector3 initialVisualLocalScale;
    private bool hasCapturedInitialState;

    // ────────────────────────────────────────
    // 初期化・状態保存
    // ────────────────────────────────────────

    public void Initialize(Transform owner)
    {
        ownerRoot = owner;
        InitializeReferences();
        CaptureInitialState();
    }

    public void CaptureInitialState()
    {
        if (hasCapturedInitialState)
        {
            return;
        }

        if (visualRoot != null)
        {
            initialVisualLocalPosition = visualRoot.localPosition;
            initialVisualLocalScale = visualRoot.localScale;
        }

        hasCapturedInitialState = true;
    }

    public void ResetToInitialState()
    {
        if (visualRoot == null)
        {
            return;
        }

        visualRoot.localPosition = initialVisualLocalPosition;
        visualRoot.localScale = initialVisualLocalScale;
    }

    // ────────────────────────────────────────
    // 状態更新
    // ────────────────────────────────────────

    public void TickAlert(float stateElapsedTime, SonarChargerSettings settings)
    {
        if (visualRoot == null || settings == null)
        {
            return;
        }

        if (ShouldSkipShake())
        {
            return;
        }

        float amplitude = CalculateAlertAmplitude(stateElapsedTime, settings);
        float frequency = settings.alertShakeFrequency;

        Vector3 offset = CalculateShakeOffset(stateElapsedTime, frequency, amplitude);
        visualRoot.localPosition = initialVisualLocalPosition + offset;
    }

    public void ResetVisualOffset()
    {
        if (visualRoot == null)
        {
            return;
        }

        visualRoot.localPosition = initialVisualLocalPosition;
    }

    public void ApplyDirection(Vector3 direction)
    {
        if (!flipByDirectionX || visualRoot == null)
        {
            return;
        }

        if (Mathf.Abs(direction.x) < 0.001f)
        {
            return;
        }

        Vector3 scale = initialVisualLocalScale;
        scale.x = Mathf.Abs(initialVisualLocalScale.x) * Mathf.Sign(direction.x);
        visualRoot.localScale = scale;
    }

    public void SetVisible(bool visible)
    {
        if (controlledRenderers == null)
        {
            return;
        }

        foreach (Renderer renderer in controlledRenderers)
        {
            if (renderer != null)
            {
                renderer.enabled = visible;
            }
        }
    }

    // ────────────────────────────────────────
    // ヘルパー
    // ────────────────────────────────────────

    private void InitializeReferences()
    {
        if (visualRoot == null)
        {
            visualRoot = transform;
        }

        if (controlledRenderers == null || controlledRenderers.Length == 0)
        {
            controlledRenderers = GetComponentsInChildren<Renderer>(true);
        }
    }

    private bool ShouldSkipShake()
    {
        return ownerRoot != null && visualRoot == ownerRoot;
    }

    private float CalculateAlertAmplitude(float stateElapsedTime, SonarChargerSettings settings)
    {
        float amplitude = settings.alertShakeAmplitude;

        if (settings.growShakeTowardCharge && settings.alertTime > 0.0f)
        {
            float t = Mathf.Clamp01(stateElapsedTime / settings.alertTime);
            amplitude *= Mathf.Lerp(0.35f, 1.0f, t);
        }

        return amplitude;
    }

    private Vector3 CalculateShakeOffset(float time, float frequency, float amplitude)
    {
        float x = Mathf.Sin(time * frequency) * amplitude;
        float y = Mathf.Cos(time * frequency * 0.73f) * amplitude;
        return new Vector3(x, y, 0.0f);
    }
}