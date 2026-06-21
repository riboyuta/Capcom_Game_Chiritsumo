using UnityEngine;

// SonarChargerEnemy が使用するソナー検知コンポーネント。
// 一定間隔でパルスを展開し、リング上に条件を満たすプレイヤーがいれば検知する。
[DisallowMultipleComponent]
public sealed class SonarChargerSonarDetector : MonoBehaviour
{
    private const int SonarRingSegments  = 96;
    private const int RingBridgeLineStep = 8;   // 内外円をつなぐ補助線の間隔（セグメント単位）

    // =========================================================
    // インスペクター設定
    // =========================================================

    [Header("ソナー中心")]
    [Tooltip("ソナーの発生中心です。未設定時はこの GameObject の位置を使います。")]
    [SerializeField] private Transform sonarOrigin;

    [Header("Gizmo")]
    [SerializeField] private bool  drawGizmos        = true;
    [SerializeField] private Color currentRingColor  = new Color(0.0f, 0.8f, 1.0f, 0.9f);
    [SerializeField] private Color maxRadiusColor    = new Color(0.0f, 0.8f, 1.0f, 0.25f);

    [Header("GameView 可視化")]
    [Tooltip("ソナーの広がりを GameView 上にワイヤーフレームで表示します。")]
    [SerializeField] private bool  visualizeSonarInGameView  = true;

    [Tooltip("展開中のソナーリングの表示色です。")]
    [SerializeField] private Color gameViewCurrentRingColor  = new Color(0.0f, 0.8f, 1.0f, 0.9f);

    // =========================================================
    // ランタイム状態
    // =========================================================

    // パルス進行状態
    private bool  isPulseActive;
    private bool  hasCompletedFirstPulse;
    private float intervalTimer;

    // 半径管理（前フレームと現フレームの半径でスイープ判定する）
    private float currentRadius;
    private float previousRadius;

    // 設定値キャッシュ（OnRenderObject・Gizmo 描画用）
    private float lastMaxRadius;
    private float lastRingThickness;

    private Material lineMaterial;

    // =========================================================
    // 公開プロパティ
    // =========================================================

    public bool  IsPulseActive  => isPulseActive;
    public float CurrentRadius  => currentRadius;

    // =========================================================
    // Unity ライフサイクル
    // =========================================================

    private void Awake()
    {
        if (visualizeSonarInGameView)
            CreateLineMaterial();
    }

    private void OnDestroy()
    {
        if (lineMaterial == null)
            return;

        if (Application.isPlaying)
            Destroy(lineMaterial);
        else
            DestroyImmediate(lineMaterial);
    }

    // =========================================================
    // 公開 API
    // =========================================================

    // 状態をリセットし、設定値をキャッシュする
    public void ResetDetector(
    SonarChargerSettings settings)
    {
        isPulseActive = false;
        hasCompletedFirstPulse = false;
        intervalTimer = 0.0f;
        previousRadius = 0.0f;
        currentRadius = 0.0f;

        lastMaxRadius =
            settings != null
                ? settings.sonarMaxRadius
                : 0.0f;

        lastRingThickness =
            settings != null
                ? settings.sonarRingThickness
                : 0.0f;
    }

    // ソナー発信タイマーを進める
    // 敵が起動中なら、攻撃状態や画面内復帰中でも時間を計測する
    public void TickInterval(
        SonarChargerSettings settings,
        float deltaTime)
    {
        if (settings == null)
        {
            return;
        }

        float waitTime =
            hasCompletedFirstPulse
                ? Mathf.Max(0.01f, settings.sonarInterval)
                : Mathf.Max(0.0f, settings.firstSonarDelay);

        intervalTimer =
            Mathf.Min(
                intervalTimer + Mathf.Max(0.0f, deltaTime),
                waitTime);
    }

    // 展開中のパルスだけをキャンセルする
    // 次回発信までのタイマーは維持する
    public void CancelPulse()
    {
        isPulseActive = false;
        previousRadius = 0.0f;
        currentRadius = 0.0f;
    }

    // 画面外移動によってソナーを中断する
    // 中断したソナーは正常な発信として扱わず、
    // 次に発信可能になった時点ですぐ再展開できる状態にする
    public void CancelPulseForViewRecovery(
        SonarChargerSettings settings)
    {
        // 展開中でなければ、現在のインターバルを変更しない
        if (!isPulseActive)
        {
            return;
        }

        StopPulse();

        if (settings == null)
        {
            return;
        }

        hasCompletedFirstPulse = true;

        // 次回発信の待機を完了済みにする
        intervalTimer =
            Mathf.Max(
                0.01f,
                settings.sonarInterval);
    }

    // ソナーを 1 フレーム分進め、プレイヤーを検知したら true を返す
    public bool TickSonar(
        PlayerController targetPlayer,
        SonarChargerPlayerMotionDetector motionDetector,
        SonarChargerSettings settings,
        float deltaTime,
        out Vector3 detectedPosition)
    {
        detectedPosition = Vector3.zero;

        if (targetPlayer == null || motionDetector == null || settings == null)
            return false;

        lastMaxRadius     = settings.sonarMaxRadius;
        lastRingThickness = settings.sonarRingThickness;

        if (!UpdatePulse(settings, deltaTime, out bool reachedMaxRadius))
            return false;

        Vector3 origin         = GetOriginPosition();
        Vector3 playerPosition = targetPlayer.transform.position;

        bool isOnRing               = IsPlayerOnSweptRing(origin, playerPosition, settings);
        bool meetsDetectionCondition = motionDetector.IsMoving(settings.moveDetectMode);

        if (isOnRing && meetsDetectionCondition)
        {
            detectedPosition = playerPosition;
            CancelPulse();
            return true;
        }

        // 最大半径到達フレームの判定を終えたらリセットする
        if (reachedMaxRadius)
        {
            StopPulse();
        }
        return false;
    }

    // =========================================================
    // パルス更新
    // =========================================================

    // インターバル完了確認 → パルス開始 → 半径拡大 → 終了判定
    private bool UpdatePulse(
        SonarChargerSettings settings,
        float deltaTime,
        out bool reachedMaxRadius)
    {
        reachedMaxRadius = false;

        if (!isPulseActive)
        {
            float waitTime =
                hasCompletedFirstPulse
                    ? Mathf.Max(0.01f, settings.sonarInterval)
                    : Mathf.Max(0.0f, settings.firstSonarDelay);

            if (intervalTimer < waitTime)
            {
                return false;
            }

            StartPulse();
        }

        previousRadius =
            currentRadius;

        float expandDistance =
            Mathf.Max(
                0.0f,
                settings.sonarExpandSpeed) *
            Mathf.Max(
                0.0f,
                deltaTime);

        currentRadius +=
            expandDistance;

        if (currentRadius >= settings.sonarMaxRadius)
        {
            currentRadius =
                Mathf.Max(
                    0.0f,
                    settings.sonarMaxRadius);

            reachedMaxRadius =
                true;
        }

        return true;
    }

    // =========================================================
    // 検知判定
    // =========================================================

    // 前後フレームの半径でスイープした環状領域内にプレイヤーがいるか判定する（XY 平面）
    private bool IsPlayerOnSweptRing(
        Vector3 origin,
        Vector3 playerPosition,
        SonarChargerSettings settings)
    {
        float distance      = Vector2.Distance(ToVector2(origin), ToVector2(playerPosition));
        float halfThickness = Mathf.Max(0.0f, settings.sonarRingThickness) * 0.5f;

        float minRadius = Mathf.Max(0.0f, Mathf.Min(previousRadius, currentRadius) - halfThickness);
        float maxRadius =                  Mathf.Max(previousRadius, currentRadius) + halfThickness;

        return distance >= minRadius && distance <= maxRadius;
    }

    // =========================================================
    // ユーティリティ
    // =========================================================

    // ソナーの発生中心位置を返す
    private Vector3 GetOriginPosition()
    {
        return sonarOrigin != null ? sonarOrigin.position : transform.position;
    }

    // 新しいソナーパルスを開始する
    private void StartPulse()
    {
        isPulseActive = true;

        // 初回ソナーは開始した時点で完了扱いにする
        hasCompletedFirstPulse = true;

        // 次回発信までの時間は今回の開始時点から計測する
        intervalTimer = 0.0f;

        previousRadius = 0.0f;
        currentRadius = 0.0f;
    }

    // 現在のパルス表示だけを終了する
    // 発信タイマーは維持する
    private void StopPulse()
    {
        isPulseActive = false;
        previousRadius = 0.0f;
        currentRadius = 0.0f;
    }

    // XY 平面で扱うため Z を捨てて Vector2 に変換する
    private static Vector2 ToVector2(Vector3 vector)
    {
        return new Vector2(vector.x, vector.y);
    }

    // =========================================================
    // GameView ワイヤーフレーム描画
    // =========================================================

    private void CreateLineMaterial()
    {
        if (lineMaterial != null)
            return;

        Shader shader = Shader.Find("Hidden/Internal-Colored");

        if (shader == null)
        {
            Debug.LogWarning("[SonarChargerSonarDetector] Hidden/Internal-Colored shader が見つかりません。", this);
            return;
        }

        lineMaterial = new Material(shader);
        lineMaterial.hideFlags = HideFlags.HideAndDontSave;
        ApplyLineMaterialSettings();
    }

    private void ApplyLineMaterialSettings()
    {
        if (lineMaterial == null)
            return;

        lineMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        lineMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        lineMaterial.SetInt("_Cull",     (int)UnityEngine.Rendering.CullMode.Off);
        lineMaterial.SetInt("_ZWrite",   0);
        lineMaterial.SetInt("_ZTest",    (int)UnityEngine.Rendering.CompareFunction.Always);
    }

    private void OnRenderObject()
    {
        if (!visualizeSonarInGameView)
            return;

        if (lineMaterial == null)
            CreateLineMaterial();

        if (lineMaterial == null)
            return;

        Vector3 origin = GetOriginPosition();

        ApplyLineMaterialSettings();
        lineMaterial.SetPass(0);

        GL.PushMatrix();
        GL.MultMatrix(Matrix4x4.identity);
        GL.Begin(GL.LINES);

        if (isPulseActive && currentRadius > 0.0f)
        {
            GL.Color(gameViewCurrentRingColor);
            DrawSonarRingWireframe(origin, currentRadius, lastRingThickness);
        }

        GL.End();
        GL.PopMatrix();
    }

    // 内円・外円と補助線でリングの太さを視覚化する
    private void DrawSonarRingWireframe(Vector3 origin, float radius, float thickness)
    {
        float halfThickness = Mathf.Max(0.0f, thickness * 0.5f);
        float innerRadius   = Mathf.Max(0.0f, radius - halfThickness);
        float outerRadius   = radius + halfThickness;

        if (innerRadius > 0.01f)
            DrawCircleWireframe(origin, innerRadius);

        DrawCircleWireframe(origin, outerRadius);

        if (innerRadius <= 0.01f)
            return;

        // 内外円を RingBridgeLineStep おきに繋いでリング幅を分かりやすくする
        for (int i = 0; i < SonarRingSegments; i += RingBridgeLineStep)
        {
            float angle = (i / (float)SonarRingSegments) * Mathf.PI * 2.0f;

            Vector3 innerPoint = origin + new Vector3(Mathf.Cos(angle) * innerRadius, Mathf.Sin(angle) * innerRadius, 0.0f);
            Vector3 outerPoint = origin + new Vector3(Mathf.Cos(angle) * outerRadius, Mathf.Sin(angle) * outerRadius, 0.0f);

            GL.Vertex(innerPoint);
            GL.Vertex(outerPoint);
        }
    }

    // XY 平面に円を描く
    private void DrawCircleWireframe(Vector3 origin, float radius)
    {
        for (int i = 0; i < SonarRingSegments; i++)
        {
            float a1 = (i       / (float)SonarRingSegments) * Mathf.PI * 2.0f;
            float a2 = ((i + 1) / (float)SonarRingSegments) * Mathf.PI * 2.0f;

            GL.Vertex(origin + new Vector3(Mathf.Cos(a1) * radius, Mathf.Sin(a1) * radius, 0.0f));
            GL.Vertex(origin + new Vector3(Mathf.Cos(a2) * radius, Mathf.Sin(a2) * radius, 0.0f));
        }
    }

    // =========================================================
    // Gizmo
    // =========================================================

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos)
            return;

        Vector3 origin = GetOriginPosition();

        // 最大到達半径
        if (lastMaxRadius > 0.0f)
        {
            Gizmos.color = maxRadiusColor;
            Gizmos.DrawWireSphere(origin, lastMaxRadius);
        }

        // 現在のリング位置
        if (isPulseActive && currentRadius > 0.0f)
        {
            Gizmos.color = currentRingColor;
            Gizmos.DrawWireSphere(origin, currentRadius);
        }
    }
#endif
}