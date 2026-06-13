using UnityEngine;

[DisallowMultipleComponent]
public sealed class SonarChargerChargeWarningView : MonoBehaviour
{
    public enum SonarChargeWarningVisualState
    {
        Tracking,
        Locked,
        Charging
    }

    private static readonly int WarningAlphaId = Shader.PropertyToID("_WarningAlpha");
    private static readonly int WarningPulseId = Shader.PropertyToID("_WarningPulse");
    private static readonly int WarningProgressId = Shader.PropertyToID("_WarningProgress");
    private static readonly int WarningStateId = Shader.PropertyToID("_WarningState");
    private static readonly int WarningLengthId = Shader.PropertyToID("_WarningLength");
    private static readonly int WarningWidthId = Shader.PropertyToID("_WarningWidth");
    private static readonly int LockFlashId = Shader.PropertyToID("_LockFlash");

    [Header("帯Root")]
    [Tooltip("突進予測帯の位置・回転・スケールを制御するRootです。")]
    [SerializeField] private Transform bandRoot;

    [Header("帯Quad")]
    [Tooltip("実際に帯として表示するQuadのTransformです。BandRootの子を指定します。")]
    [SerializeField] private Transform bandQuadTransform;

    [Header("帯Renderer")]
    [Tooltip("突進予測帯を描画するMeshRendererです。QuadのMeshRendererを指定します。")]
    [SerializeField] private MeshRenderer bandRenderer;

    [Header("先端マーカー")]
    [Tooltip("予測帯の先端に表示するマーカーです。未使用なら未設定で構いません。")]
    [SerializeField] private Transform targetMarker;

    [Header("表示調整")]
    [Tooltip("この距離以下になった帯は非表示にします。")]
    [SerializeField] private float minVisibleLength = 0.05f;

    private Vector3 initialBandLocalScale = Vector3.one;
    private Vector3 initialMarkerScale = Vector3.one;

    private bool hasInitialBandScale;
    private bool hasInitialMarkerScale;

    private MaterialPropertyBlock propertyBlock;

    private SonarChargeWarningVisualState visualState = SonarChargeWarningVisualState.Tracking;
    private float lockedEffectTimer;

    private void Awake()
    {
        Initialize();
        Hide();
    }

    public void Initialize()
    {
        if (bandRoot == null)
        {
            bandRoot = transform;
        }

        if (bandQuadTransform == null && bandRenderer != null)
        {
            bandQuadTransform = bandRenderer.transform;
        }

        if (bandRenderer == null)
        {
            bandRenderer = GetComponentInChildren<MeshRenderer>(true);
        }

        if (bandRoot != null && !hasInitialBandScale)
        {
            initialBandLocalScale = bandRoot.localScale;
            hasInitialBandScale = true;
        }

        if (targetMarker != null && !hasInitialMarkerScale)
        {
            initialMarkerScale = targetMarker.localScale;
            hasInitialMarkerScale = true;
        }

        propertyBlock ??= new MaterialPropertyBlock();
    }

    public void Show()
    {
        if (bandRenderer != null)
        {
            bandRenderer.enabled = true;
        }

        if (targetMarker != null)
        {
            targetMarker.gameObject.SetActive(true);
        }
    }

    public void Hide()
    {
        if (bandRenderer != null)
        {
            bandRenderer.enabled = false;
        }

        if (targetMarker != null)
        {
            targetMarker.gameObject.SetActive(false);
        }
    }

    public void ResetView()
    {
        Hide();

        visualState = SonarChargeWarningVisualState.Tracking;
        lockedEffectTimer = 0.0f;

        if (bandRoot != null && hasInitialBandScale)
        {
            bandRoot.localScale = initialBandLocalScale;
        }

        if (targetMarker != null && hasInitialMarkerScale)
        {
            targetMarker.localScale = initialMarkerScale;
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

        Vector3 direction = end - start;
        direction.z = 0.0f;

        float length = direction.magnitude;

        if (length <= minVisibleLength)
        {
            Hide();
            return;
        }

        Show();

        if (visualState == SonarChargeWarningVisualState.Locked)
        {
            lockedEffectTimer += Time.deltaTime;
        }

        Vector3 normalizedDirection = direction / length;

        UpdateBandTransform(
            start,
            end,
            normalizedDirection,
            length,
            settings);

        UpdateShaderParameters(
            Mathf.Clamp01(alertT),
            elapsedTime,
            length,
            settings);

        UpdateMarker(end, elapsedTime, settings);
    }

    private void UpdateBandTransform(
    Vector3 start,
    Vector3 end,
    Vector3 normalizedDirection,
    float length,
    SonarChargerSettings settings)
    {
        if (bandRoot == null)
        {
            return;
        }

        Vector3 rootPosition = start;
        rootPosition.z += settings.alertPredictionBandZOffset;

        float angleZ = Mathf.Atan2(normalizedDirection.y, normalizedDirection.x) * Mathf.Rad2Deg;

        // Rootは帯の開始位置、つまり敵の現在位置に置く。
        bandRoot.position = rootPosition;
        bandRoot.rotation = Quaternion.Euler(0.0f, 0.0f, angleZ);
        bandRoot.localScale = Vector3.one;

        if (bandQuadTransform == null)
        {
            return;
        }

        // Unity標準Quadは中心Pivotなので、ローカルX方向に半分ずらす。
        // これで帯はRoot位置から前方にだけ伸びる。
        bandQuadTransform.localPosition = new Vector3(length * 0.5f, 0.0f, 0.0f);
        bandQuadTransform.localRotation = Quaternion.identity;
        bandQuadTransform.localScale = new Vector3(
            length,
            settings.alertPredictionBandWidth,
            1.0f);
    }

    private void UpdateShaderParameters(
    float alertT,
    float elapsedTime,
    float length,
    SonarChargerSettings settings)
    {
        if (bandRenderer == null)
        {
            return;
        }

        propertyBlock ??= new MaterialPropertyBlock();
        bandRenderer.GetPropertyBlock(propertyBlock);

        float pulseSpeed = settings.alertPredictionPulseSpeed;
        float baseAlpha = settings.alertPredictionBandAlpha;
        float stateValue = 0.0f;
        float lockFlash = 0.0f;

        switch (visualState)
        {
            case SonarChargeWarningVisualState.Tracking:
                stateValue = 0.0f;

                // Alert中でも危険範囲として読めるよう、透明度は下げすぎない。
                pulseSpeed *= 1.0f;
                baseAlpha *= 1.0f;
                break;

            case SonarChargeWarningVisualState.Locked:
                stateValue = 1.0f;
                pulseSpeed *= 1.5f;

                // LockConfirmに入った直後ほど強く光る。
                lockFlash = Mathf.Clamp01(1.0f - lockedEffectTimer / Mathf.Max(0.001f, settings.lockConfirmTime));
                baseAlpha *= 1.2f;
                break;

            case SonarChargeWarningVisualState.Charging:
                stateValue = 2.0f;
                pulseSpeed *= 2.0f;
                baseAlpha *= 1.0f;
                break;
        }

        float pulse = Mathf.Sin(elapsedTime * pulseSpeed) * 0.5f + 0.5f;

        float alpha = Mathf.Lerp(
            settings.alertPredictionMinAlpha,
            settings.alertPredictionMaxAlpha,
            pulse);

        alpha *= baseAlpha;
        alpha = Mathf.Clamp01(alpha + lockFlash * 0.35f);

        propertyBlock.SetFloat(Shader.PropertyToID("_WarningAlpha"), alpha);
        propertyBlock.SetFloat(Shader.PropertyToID("_WarningPulse"), pulse);
        propertyBlock.SetFloat(Shader.PropertyToID("_WarningProgress"), alertT);
        propertyBlock.SetFloat(Shader.PropertyToID("_WarningState"), stateValue);
        propertyBlock.SetFloat(Shader.PropertyToID("_WarningLength"), length);
        propertyBlock.SetFloat(Shader.PropertyToID("_WarningWidth"), settings.alertPredictionBandWidth);
        propertyBlock.SetFloat(LockFlashId, lockFlash);

        bandRenderer.SetPropertyBlock(propertyBlock);
    }

    private void UpdateMarker(
        Vector3 end,
        float elapsedTime,
        SonarChargerSettings settings)
    {
        if (targetMarker == null)
        {
            return;
        }

        Vector3 markerPosition = end;
        markerPosition.z += settings.alertPredictionBandZOffset;
        targetMarker.position = markerPosition;

        if (!hasInitialMarkerScale)
        {
            initialMarkerScale = targetMarker.localScale;
            hasInitialMarkerScale = true;
        }

        float pulse = Mathf.Sin(elapsedTime * settings.alertPredictionPulseSpeed) * 0.5f + 0.5f;

        float scale = settings.alertPredictionTargetMarkerScale
            + settings.alertPredictionTargetMarkerPulseScale * pulse;

        targetMarker.localScale = initialMarkerScale * Mathf.Max(0.0f, scale);
    }

    public void SetTracking()
    {
        visualState = SonarChargeWarningVisualState.Tracking;
        lockedEffectTimer = 0.0f;
    }

    public void SetLocked()
    {
        visualState = SonarChargeWarningVisualState.Locked;
        lockedEffectTimer = 0.0f;
    }

    public void SetCharging()
    {
        visualState = SonarChargeWarningVisualState.Charging;
    }
}