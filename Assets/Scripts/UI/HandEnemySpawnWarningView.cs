using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public enum SpawnWarningScreenEdge
{
    Left,
    Right,
    Top,
    Bottom
}

[DisallowMultipleComponent]
public sealed class HandEnemySpawnWarningView : MonoBehaviour
{
    [Header("参照")]
    [Tooltip("画面全体を覆う RawImage です。")]
    [SerializeField] private RawImage screenImage;

    [Tooltip("UI/HandEnemyEdgeWarning を使った Material です。")]
    [SerializeField] private Material warningMaterial;

    [Header("見た目")]
    [Tooltip("警告色です。")]
    [SerializeField] private Color warningColor = new Color(1f, 0f, 0f, 1f);

    [Tooltip("最大不透明度です。")]
    [SerializeField, Range(0f, 1f)] private float maxAlpha = 0.8f;

    [Tooltip("端からどのくらい内側まで滲ませるかです。")]
    [SerializeField, Range(0.01f, 0.5f)] private float edgeThickness = 0.18f;

    [Tooltip("滲み境界のぼかし量です。")]
    [SerializeField, Range(0.001f, 0.5f)] private float edgeSoftness = 0.22f;

    [Tooltip("ノイズの細かさです。")]
    [SerializeField, Min(1f)] private float noiseScale = 42f;

    [Tooltip("ノイズの強さです。")]
    [SerializeField, Range(0f, 1f)] private float noiseStrength = 0.45f;

    [Header("点滅")]
    [Tooltip("有効にすると警告UIを点滅させます。無効にすると一定の濃さで表示します。")]
    [SerializeField] private bool enableBlink = true;

    [Tooltip("点滅速度です。")]
    [SerializeField, Min(0.1f)] private float blinkSpeed = 6f;

    [Tooltip("点滅時の最低明るさです。0に近いほど強く点滅します。")]
    [SerializeField, Range(0f, 1f)] private float minBlinkRate = 0.25f;

    [Header("時間")]
    [Tooltip("Time.timeScale の影響を受けずに再生するかです。")]
    [SerializeField] private bool useUnscaledTime = false;

    private static readonly int WarningColorId = Shader.PropertyToID("_WarningColor");
    private static readonly int WarningAlphaId = Shader.PropertyToID("_WarningAlpha");
    private static readonly int EdgeId = Shader.PropertyToID("_Edge");
    private static readonly int EdgeThicknessId = Shader.PropertyToID("_EdgeThickness");
    private static readonly int EdgeSoftnessId = Shader.PropertyToID("_EdgeSoftness");
    private static readonly int NoiseScaleId = Shader.PropertyToID("_NoiseScale");
    private static readonly int NoiseStrengthId = Shader.PropertyToID("_NoiseStrength");

    private Material runtimeMaterial;
    private Coroutine runningCoroutine;

    public bool IsPlaying => runningCoroutine != null;

    private void Awake()
    {
        if (screenImage == null)
        {
            screenImage = GetComponentInChildren<RawImage>(true);
        }

        if (screenImage != null)
        {
            screenImage.raycastTarget = false;
        }

        if (screenImage != null && warningMaterial != null)
        {
            runtimeMaterial = Instantiate(warningMaterial);
            screenImage.material = runtimeMaterial;
        }

        ResetImmediate();
    }

    private void OnDestroy()
    {
        if (runtimeMaterial == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(runtimeMaterial);
        }
        else
        {
            DestroyImmediate(runtimeMaterial);
        }
    }

    public void Play(SpawnWarningScreenEdge edge, float duration)
    {
        StopAndHide();

        if (screenImage == null || runtimeMaterial == null)
        {
            return;
        }

        runningCoroutine = StartCoroutine(PlayRoutine(edge, duration));
    }

    public void PlayLoop(SpawnWarningScreenEdge edge)
    {
        StopAndHide();

        if (screenImage == null || runtimeMaterial == null)
        {
            return;
        }

        runningCoroutine = StartCoroutine(PlayLoopRoutine(edge));
    }

    private IEnumerator PlayLoopRoutine(SpawnWarningScreenEdge edge)
    {
        SetImageEnabled(true);
        SetStaticMaterialValues(edge);

        float elapsed = 0f;

        while (true)
        {
            float deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            elapsed += deltaTime;

            float fadeIn = Mathf.Clamp01(elapsed / 0.15f);

            float blinkRate = 1f;

            if (enableBlink)
            {
                float blink = Mathf.Sin(elapsed * blinkSpeed * Mathf.PI * 2f) * 0.5f + 0.5f;
                blinkRate = Mathf.Lerp(minBlinkRate, 1f, blink);
            }

            float alpha = maxAlpha * fadeIn * blinkRate;
            runtimeMaterial.SetFloat(WarningAlphaId, alpha);

            yield return null;
        }
    }

    public void StopAndHide()
    {
        if (runningCoroutine != null)
        {
            StopCoroutine(runningCoroutine);
            runningCoroutine = null;
        }

        ResetImmediate();
    }

    private IEnumerator PlayRoutine(SpawnWarningScreenEdge edge, float duration)
    {
        SetImageEnabled(true);
        SetStaticMaterialValues(edge);

        float elapsed = 0f;
        duration = Mathf.Max(0.01f, duration);

        while (elapsed < duration)
        {
            float deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            elapsed += deltaTime;

            float t = Mathf.Clamp01(elapsed / duration);

            float fadeIn = Mathf.Clamp01(t / 0.15f);
            float fadeOut = Mathf.Clamp01((1f - t) / 0.18f);
            float envelope = Mathf.Min(fadeIn, fadeOut);

            float blink = Mathf.Sin(elapsed * blinkSpeed * Mathf.PI * 2f) * 0.5f + 0.5f;
            float blinkRate = Mathf.Lerp(minBlinkRate, 1f, blink);

            float alpha = maxAlpha * envelope * blinkRate;
            runtimeMaterial.SetFloat(WarningAlphaId, alpha);

            yield return null;
        }

        ResetImmediate();
        runningCoroutine = null;
    }

    private void SetStaticMaterialValues(SpawnWarningScreenEdge edge)
    {
        runtimeMaterial.SetColor(WarningColorId, warningColor);
        runtimeMaterial.SetFloat(WarningAlphaId, 0f);
        runtimeMaterial.SetFloat(EdgeId, ToShaderEdgeValue(edge));
        runtimeMaterial.SetFloat(EdgeThicknessId, edgeThickness);
        runtimeMaterial.SetFloat(EdgeSoftnessId, edgeSoftness);
        runtimeMaterial.SetFloat(NoiseScaleId, noiseScale);
        runtimeMaterial.SetFloat(NoiseStrengthId, noiseStrength);
    }

    private void ResetImmediate()
    {
        if (runtimeMaterial != null)
        {
            runtimeMaterial.SetFloat(WarningAlphaId, 0f);
        }

        SetImageEnabled(false);
    }

    private void SetImageEnabled(bool enabled)
    {
        if (screenImage == null)
        {
            return;
        }

        screenImage.enabled = enabled;
        screenImage.raycastTarget = false;
    }

    private float ToShaderEdgeValue(SpawnWarningScreenEdge edge)
    {
        switch (edge)
        {
            case SpawnWarningScreenEdge.Left:
                return 0f;

            case SpawnWarningScreenEdge.Right:
                return 1f;

            case SpawnWarningScreenEdge.Top:
                return 2f;

            case SpawnWarningScreenEdge.Bottom:
                return 3f;

            default:
                return 0f;
        }
    }
}