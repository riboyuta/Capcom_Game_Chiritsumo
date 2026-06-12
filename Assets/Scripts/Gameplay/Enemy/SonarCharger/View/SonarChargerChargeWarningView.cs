using UnityEngine;

[DisallowMultipleComponent]
public sealed class SonarChargerChargeWarningView : MonoBehaviour
{
    [Header("予測線: 芯")]
    [Tooltip("突進ルートの中心線です。")]
    [SerializeField] private LineRenderer coreLine;

    [Header("予測線: 発光")]
    [Tooltip("突進ルートの発光・太い外側線です。未使用なら未設定で構いません。")]
    [SerializeField] private LineRenderer glowLine;

    [Header("先端マーカー")]
    [Tooltip("予測線の先端に表示するマーカーです。未使用なら未設定で構いません。")]
    [SerializeField] private Transform targetMarker;

    [Header("表示色")]
    [SerializeField] private Color coreColor = new Color(1.0f, 0.2f, 0.1f, 1.0f);
    [SerializeField] private Color glowColor = new Color(1.0f, 0.2f, 0.1f, 0.45f);

    [Header("Zオフセット")]
    [Tooltip("予測線を少し手前に出すためのZオフセットです。")]
    [SerializeField] private float zOffset = -0.05f;

    private Vector3 initialMarkerScale = Vector3.one;
    private bool hasInitialMarkerScale;

    private void Awake()
    {
        Initialize();
        Hide();
    }

    public void Initialize()
    {
        if (coreLine == null)
        {
            coreLine = GetComponentInChildren<LineRenderer>(true);
        }

        SetupLine(coreLine);
        SetupLine(glowLine);

        if (targetMarker != null && !hasInitialMarkerScale)
        {
            initialMarkerScale = targetMarker.localScale;
            hasInitialMarkerScale = true;
        }
    }

    public void Show()
    {
        SetLineVisible(coreLine, true);
        SetLineVisible(glowLine, true);

        if (targetMarker != null)
        {
            targetMarker.gameObject.SetActive(true);
        }
    }

    public void ResetView()
    {
        Hide();

        if (targetMarker != null && hasInitialMarkerScale)
        {
            targetMarker.localScale = initialMarkerScale;
        }
    }

    public void Hide()
    {
        if (coreLine != null)
        {
            coreLine.enabled = false;
        }

        if (glowLine != null)
        {
            glowLine.enabled = false;
        }

        if (targetMarker != null)
        {
            targetMarker.gameObject.SetActive(false);
        }
    }

    public void UpdateWarning(
        Vector3 start,
        Vector3 end,
        float alertT,
        float elapsedTime,
        SonarChargerSettings settings)
    {
        if (settings == null || !settings.showAlertPredictionLine)
        {
            Hide();
            return;
        }

        Show();

        start.z += zOffset;
        end.z += zOffset;

        float pulse = Mathf.Sin(elapsedTime * settings.alertPredictionPulseSpeed) * 0.5f + 0.5f;
        float alpha = Mathf.Lerp(
            settings.alertPredictionMinAlpha,
            settings.alertPredictionMaxAlpha,
            pulse);

        float coreWidth = Mathf.Max(0.001f, settings.alertPredictionCoreWidth);
        float glowWidth = Mathf.Max(coreWidth, settings.alertPredictionGlowWidth);

        UpdateLine(coreLine, start, end, coreWidth, coreColor, alpha);
        UpdateLine(glowLine, start, end, glowWidth, glowColor, alpha * glowColor.a);

        UpdateMarker(end, pulse, settings);
    }

    private void SetupLine(LineRenderer line)
    {
        if (line == null)
        {
            return;
        }

        line.positionCount = 2;
        line.useWorldSpace = true;
        line.enabled = false;
    }

    private void SetLineVisible(LineRenderer line, bool visible)
    {
        if (line != null)
        {
            line.enabled = visible;
        }
    }

    private void UpdateLine(
        LineRenderer line,
        Vector3 start,
        Vector3 end,
        float width,
        Color baseColor,
        float alpha)
    {
        if (line == null)
        {
            return;
        }

        Color color = baseColor;
        color.a = Mathf.Clamp01(alpha);

        line.positionCount = 2;
        line.SetPosition(0, start);
        line.SetPosition(1, end);
        line.startWidth = width;
        line.endWidth = width;
        line.startColor = color;
        line.endColor = color;
    }

    private void UpdateMarker(
        Vector3 end,
        float pulse,
        SonarChargerSettings settings)
    {
        if (targetMarker == null)
        {
            return;
        }

        targetMarker.position = end;

        if (!hasInitialMarkerScale)
        {
            initialMarkerScale = targetMarker.localScale;
            hasInitialMarkerScale = true;
        }

        float scale = settings.alertPredictionTargetMarkerScale
            + settings.alertPredictionTargetMarkerPulseScale * pulse;

        targetMarker.localScale = initialMarkerScale * Mathf.Max(0.0f, scale);
    }
}