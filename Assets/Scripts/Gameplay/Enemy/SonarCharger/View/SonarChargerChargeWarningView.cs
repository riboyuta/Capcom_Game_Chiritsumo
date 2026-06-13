using UnityEngine;

// SonarChargerEnemy の突進予測帯（警告ライン）の見た目を管理するビュークラス。
// Tracking → Locked → Charging の状態遷移に応じてシェーダーパラメータと Transform を更新する。
[DisallowMultipleComponent]
public sealed class SonarChargerChargeWarningView : MonoBehaviour
{
    // =========================================================
    // ビジュアル状態定義
    // =========================================================

    public enum SonarChargeWarningVisualState
    {
        Tracking,   // プレイヤーを追跡中
        Locked,     // 発射方向が確定した直後
        Charging,   // 突進チャージ中
    }

    // =========================================================
    // シェーダープロパティ ID キャッシュ
    // =========================================================

    private static readonly int WarningAlphaId    = Shader.PropertyToID("_WarningAlpha");
    private static readonly int WarningPulseId    = Shader.PropertyToID("_WarningPulse");
    private static readonly int WarningProgressId = Shader.PropertyToID("_WarningProgress");
    private static readonly int WarningStateId    = Shader.PropertyToID("_WarningState");
    private static readonly int WarningLengthId   = Shader.PropertyToID("_WarningLength");
    private static readonly int WarningWidthId    = Shader.PropertyToID("_WarningWidth");
    private static readonly int LockFlashId       = Shader.PropertyToID("_LockFlash");

    // =========================================================
    // インスペクター設定
    // =========================================================

    [Header("帯 Root")]
    [Tooltip("突進予測帯の位置・回転・スケールを制御する Root です。")]
    [SerializeField] private Transform bandRoot;

    [Header("帯 Quad")]
    [Tooltip("実際に帯として表示する Quad の Transform です。BandRoot の子を指定します。")]
    [SerializeField] private Transform bandQuadTransform;

    [Header("帯 Renderer")]
    [Tooltip("突進予測帯を描画する MeshRenderer です。Quad の MeshRenderer を指定します。")]
    [SerializeField] private MeshRenderer bandRenderer;

    [Header("先端マーカー")]
    [Tooltip("予測帯の先端に表示するマーカーです。未使用なら未設定で構いません。")]
    [SerializeField] private Transform targetMarker;

    [Header("表示調整")]
    [Tooltip("この距離以下になった帯は非表示にします。")]
    [SerializeField] private float minVisibleLength = 0.05f;

    // =========================================================
    // ランタイム状態
    // =========================================================

    // bandRoot / targetMarker の初期スケール（ResetView で復元する）
    private Vector3 initialBandLocalScale = Vector3.one;
    private Vector3 initialMarkerScale    = Vector3.one;
    private bool hasInitialBandScale;
    private bool hasInitialMarkerScale;

    private MaterialPropertyBlock propertyBlock;

    // 現在のビジュアル状態と Locked 突入後の経過時間
    private SonarChargeWarningVisualState visualState = SonarChargeWarningVisualState.Tracking;
    private float lockedEffectTimer;

    // =========================================================
    // Unity ライフサイクル
    // =========================================================

    private void Awake()
    {
        Initialize();
        Hide();
    }

    // =========================================================
    // 公開 API
    // =========================================================

    // 参照の自動解決・初期スケールの記憶・PropertyBlock の生成を行う
    public void Initialize()
    {
        if (bandRoot == null)
            bandRoot = transform;

        if (bandQuadTransform == null && bandRenderer != null)
            bandQuadTransform = bandRenderer.transform;

        if (bandRenderer == null)
            bandRenderer = GetComponentInChildren<MeshRenderer>(true);

        if (bandRoot != null && !hasInitialBandScale)
        {
            initialBandLocalScale = bandRoot.localScale;
            hasInitialBandScale   = true;
        }

        if (targetMarker != null && !hasInitialMarkerScale)
        {
            initialMarkerScale    = targetMarker.localScale;
            hasInitialMarkerScale = true;
        }

        propertyBlock ??= new MaterialPropertyBlock();
    }

    // 帯とマーカーを表示する
    public void Show()
    {
        if (bandRenderer != null) bandRenderer.enabled = true;
        if (targetMarker != null) targetMarker.gameObject.SetActive(true);
    }

    // 帯とマーカーを非表示にする
    public void Hide()
    {
        if (bandRenderer != null) bandRenderer.enabled = false;
        if (targetMarker != null) targetMarker.gameObject.SetActive(false);
    }

    // 状態・タイマー・スケールを初期値へ戻す
    public void ResetView()
    {
        Hide();

        visualState       = SonarChargeWarningVisualState.Tracking;
        lockedEffectTimer = 0.0f;

        if (bandRoot     != null && hasInitialBandScale)   bandRoot.localScale     = initialBandLocalScale;
        if (targetMarker != null && hasInitialMarkerScale) targetMarker.localScale = initialMarkerScale;
    }

    // 毎フレーム呼び出し：帯の Transform とシェーダーパラメータを更新する
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

        // Locked 状態の経過時間を積算する（フラッシュ強度の計算に使う）
        if (visualState == SonarChargeWarningVisualState.Locked)
            lockedEffectTimer += Time.deltaTime;

        Vector3 normalizedDirection = direction / length;

        UpdateBandTransform(start, normalizedDirection, length, settings);
        UpdateShaderParameters(Mathf.Clamp01(alertT), elapsedTime, length, settings);
        UpdateMarker(end, elapsedTime, settings);
    }

    // Tracking 状態へ遷移する
    public void SetTracking()
    {
        visualState       = SonarChargeWarningVisualState.Tracking;
        lockedEffectTimer = 0.0f;
    }

    // Locked 状態へ遷移する（タイマーをリセットしてフラッシュを開始する）
    public void SetLocked()
    {
        visualState       = SonarChargeWarningVisualState.Locked;
        lockedEffectTimer = 0.0f;
    }

    // Charging 状態へ遷移する
    public void SetCharging()
    {
        visualState = SonarChargeWarningVisualState.Charging;
    }

    // =========================================================
    // 内部更新
    // =========================================================

    // bandRoot を start に配置し、bandQuad を length に合わせてスケーリングする
    private void UpdateBandTransform(
        Vector3 start,
        Vector3 normalizedDirection,
        float length,
        SonarChargerSettings settings)
    {
        if (bandRoot == null)
            return;

        Vector3 rootPosition = start;
        rootPosition.z      += settings.alertPredictionBandZOffset;

        float angleZ = Mathf.Atan2(normalizedDirection.y, normalizedDirection.x) * Mathf.Rad2Deg;

        // Root は帯の開始位置（敵の現在位置）に置く
        bandRoot.position   = rootPosition;
        bandRoot.rotation   = Quaternion.Euler(0.0f, 0.0f, angleZ);
        bandRoot.localScale = Vector3.one;

        if (bandQuadTransform == null)
            return;

        // Unity 標準 Quad は中心 Pivot なので、ローカル X 方向に half 分ずらして
        // Root 位置から前方にだけ伸びるようにする
        bandQuadTransform.localPosition = new Vector3(length * 0.5f, 0.0f, 0.0f);
        bandQuadTransform.localRotation = Quaternion.identity;
        bandQuadTransform.localScale    = new Vector3(length, settings.alertPredictionBandWidth, 1.0f);
    }

    // 状態に応じたパルス速度・アルファ・フラッシュ値を計算してシェーダーへ渡す
    private void UpdateShaderParameters(
        float alertT,
        float elapsedTime,
        float length,
        SonarChargerSettings settings)
    {
        if (bandRenderer == null)
            return;

        propertyBlock ??= new MaterialPropertyBlock();
        bandRenderer.GetPropertyBlock(propertyBlock);

        float pulseSpeed = settings.alertPredictionPulseSpeed;
        float baseAlpha  = settings.alertPredictionBandAlpha;
        float stateValue = 0.0f;
        float lockFlash  = 0.0f;

        switch (visualState)
        {
            case SonarChargeWarningVisualState.Tracking:
                stateValue = 0.0f;
                break;

            case SonarChargeWarningVisualState.Locked:
                stateValue  = 1.0f;
                pulseSpeed *= 1.5f;
                baseAlpha  *= 1.2f;
                // Locked 突入直後ほど強く光る
                lockFlash   = Mathf.Clamp01(1.0f - lockedEffectTimer / Mathf.Max(0.001f, settings.lockConfirmTime));
                break;

            case SonarChargeWarningVisualState.Charging:
                stateValue  = 2.0f;
                pulseSpeed *= 2.0f;
                break;
        }

        float pulse = Mathf.Sin(elapsedTime * pulseSpeed) * 0.5f + 0.5f;
        float alpha = Mathf.Lerp(settings.alertPredictionMinAlpha, settings.alertPredictionMaxAlpha, pulse);
        alpha = Mathf.Clamp01(alpha * baseAlpha + lockFlash * 0.35f);

        propertyBlock.SetFloat(WarningAlphaId, alpha);
        propertyBlock.SetFloat(WarningPulseId, pulse);
        propertyBlock.SetFloat(WarningProgressId, alertT);
        propertyBlock.SetFloat(WarningStateId, stateValue);
        propertyBlock.SetFloat(WarningLengthId, length);
        propertyBlock.SetFloat(WarningWidthId, settings.alertPredictionBandWidth);
        propertyBlock.SetFloat(LockFlashId, lockFlash);

        bandRenderer.SetPropertyBlock(propertyBlock);
    }

    // 先端マーカーの位置とパルススケールを更新する
    private void UpdateMarker(
        Vector3 end,
        float elapsedTime,
        SonarChargerSettings settings)
    {
        if (targetMarker == null)
            return;

        Vector3 markerPosition = end;
        markerPosition.z      += settings.alertPredictionBandZOffset;
        targetMarker.position  = markerPosition;

        // 初回呼び出し時に初期スケールを記憶する（Awake より前に呼ばれた場合の保険）
        if (!hasInitialMarkerScale)
        {
            initialMarkerScale    = targetMarker.localScale;
            hasInitialMarkerScale = true;
        }

        float pulse = Mathf.Sin(elapsedTime * settings.alertPredictionPulseSpeed) * 0.5f + 0.5f;
        float scale = settings.alertPredictionTargetMarkerScale
                    + settings.alertPredictionTargetMarkerPulseScale * pulse;

        targetMarker.localScale = initialMarkerScale * Mathf.Max(0.0f, scale);
    }
}