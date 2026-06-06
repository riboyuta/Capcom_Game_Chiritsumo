using UnityEngine;

[DisallowMultipleComponent]
public sealed class SonarChargerSonarDetector : MonoBehaviour
{
    private const int SonarRingSegments = 96;
    private const int RingBridgeLineStep = 8;

    [Header("ソナー中心")]
    [Tooltip("ソナーの発生中心です。未設定時はこの GameObject の位置を使います。")]
    [SerializeField] private Transform sonarOrigin;

    [Header("Gizmo")]
    [SerializeField] private bool drawGizmos = true;
    [SerializeField] private Color currentRingColor = new Color(0.0f, 0.8f, 1.0f, 0.9f);
    [SerializeField] private Color maxRadiusColor = new Color(0.0f, 0.8f, 1.0f, 0.25f);

    [Header("GameView 可視化")]
    [Tooltip("ソナーの広がりを GameView 上にワイヤーフレームで表示します。")]
    [SerializeField] private bool visualizeSonarInGameView = true;

    [Tooltip("展開中のソナーリングの表示色です。")]
    [SerializeField] private Color gameViewCurrentRingColor = new Color(0.0f, 0.8f, 1.0f, 0.9f);

    // パルス状態
    private bool isPulseActive;
    private bool hasCompletedFirstPulse;
    private float intervalTimer;
    private float currentRadius;
    private float lastMaxRadius;
    private float lastRingThickness;

    private Material lineMaterial;

    public bool IsPulseActive => isPulseActive;
    public float CurrentRadius => currentRadius;

    private void Awake()
    {
        if (visualizeSonarInGameView)
        {
            CreateLineMaterial();
        }
    }

    private void OnDestroy()
    {
        if (lineMaterial == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(lineMaterial);
        }
        else
        {
            DestroyImmediate(lineMaterial);
        }
    }

    public void ResetDetector(SonarChargerSettings settings)
    {
        ResetPulseState(false, false);
        lastMaxRadius = settings != null ? settings.sonarMaxRadius : 0.0f;
        lastRingThickness = settings != null ? settings.sonarRingThickness : 0.0f;
    }

    public void CancelPulse()
    {
        ResetPulseState(false, true);
    }

    // ソナー更新: パルスを展開し、プレイヤーがリング上で動いていれば検知
    public bool TickSonar(
        PlayerController targetPlayer,
        SonarChargerPlayerMotionDetector motionDetector,
        SonarChargerSettings settings,
        float deltaTime,
        out Vector3 detectedPosition)
    {
        detectedPosition = Vector3.zero;

        // 必要な参照がない場合は検知失敗
        if (targetPlayer == null || motionDetector == null || settings == null)
        {
            return false;
        }

        // 描画用に最新設定を保持
        lastMaxRadius = settings.sonarMaxRadius;
        lastRingThickness = settings.sonarRingThickness;

        // パルス状態を更新、アクティブでない場合はfalse
        if (!UpdatePulse(settings, deltaTime))
        {
            return false;
        }

        Vector3 origin = GetOriginPosition();
        Vector3 playerPosition = targetPlayer.transform.position;

        // プレイヤーがリング上にいない、または動いていない場合は検知失敗
        if (!IsPlayerOnRing(origin, playerPosition, settings) || !motionDetector.IsMoving(settings.moveDetectMode))
        {
            return false;
        }

        // 検知成功！プレイヤー位置を返し、パルスをキャンセル
        detectedPosition = playerPosition;
        CancelPulse();
        return true;
    }

    // パルス更新: インターバル待機、パルス開始、半径拡大、終了判定
    private bool UpdatePulse(SonarChargerSettings settings, float deltaTime)
    {
        // パルスが非アクティブな場合、インターバル待機
        if (!isPulseActive)
        {
            intervalTimer += deltaTime;
            // 初回と２回目以降で待機時間が異なる
            float waitTime = hasCompletedFirstPulse ? settings.sonarInterval : settings.firstSonarDelay;

            // 待機時間未達ならfalse
            if (intervalTimer < waitTime)
            {
                return false;
            }

            // 待機時間経過、パルス開始
            ResetPulseState(true, hasCompletedFirstPulse);
        }

        // パルスの半径を展開
        currentRadius += settings.sonarExpandSpeed * deltaTime;

        // 最大半径に達したらパルス終了
        if (currentRadius > settings.sonarMaxRadius)
        {
            ResetPulseState(false, true);
            return false; // パルス終了
        }

        return true; // パルスアクティブ
    }

    // プレイヤーがソナーリング上にいるかを判定
    private bool IsPlayerOnRing(Vector3 origin, Vector3 playerPosition, SonarChargerSettings settings)
    {
        // 原点からプレイヤーまでの距離を計算
        float distance = Vector2.Distance(ToVector2(origin), ToVector2(playerPosition));
        // リングの半分の太さ
        float halfThickness = settings.sonarRingThickness * 0.5f;
        // 現在の半径±半分の太さの範囲内ならリング上
        return distance >= currentRadius - halfThickness && distance <= currentRadius + halfThickness;
    }

    // ソナーの発生中心位置を取得
    private Vector3 GetOriginPosition()
    {
        return sonarOrigin != null ? sonarOrigin.position : transform.position;
    }

    // ヘルパー: パルス状態をリセット
    private void ResetPulseState(bool active, bool completed)
    {
        isPulseActive = active;
        hasCompletedFirstPulse = completed;
        intervalTimer = 0.0f;
        currentRadius = 0.0f;
    }

    // ユーティリティ: Vector3をVector2に変換
    private static Vector2 ToVector2(Vector3 vector)
    {
        return new Vector2(vector.x, vector.y);
    }

    // ワイヤーフレーム描画: マテリアル作成
    private void CreateLineMaterial()
    {
        if (lineMaterial != null)
        {
            return;
        }

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

    // ワイヤーフレーム描画: マテリアル設定更新
    // ワイヤーフレーム描画: マテリアル設定更新
    private void ApplyLineMaterialSettings()
    {
        if (lineMaterial == null)
        {
            return;
        }

        lineMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        lineMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        lineMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
        lineMaterial.SetInt("_ZWrite", 0);

        lineMaterial.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
    }

    // ワイヤーフレーム描画: GameView用レンダリングコールバック
    // ワイヤーフレーム描画: GameView用レンダリングコールバック
    private void OnRenderObject()
    {
        if (!visualizeSonarInGameView)
        {
            return;
        }

        if (lineMaterial == null)
        {
            CreateLineMaterial();
        }

        if (lineMaterial == null)
        {
            return;
        }

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

    // ワイヤーフレーム描画: ソナーリング本体
    private void DrawSonarRingWireframe(Vector3 origin, float radius, float thickness)
    {
        float halfThickness = Mathf.Max(0.0f, thickness * 0.5f);
        float innerRadius = Mathf.Max(0.0f, radius - halfThickness);
        float outerRadius = radius + halfThickness;

        if (innerRadius > 0.01f)
        {
            DrawCircleWireframe(origin, innerRadius);
        }

        DrawCircleWireframe(origin, outerRadius);

        if (innerRadius <= 0.01f)
        {
            return;
        }

        // 内円と外円を少しだけ繋いで、リングの太さが分かるようにする
        for (int i = 0; i < SonarRingSegments; i += RingBridgeLineStep)
        {
            float angle = (i / (float)SonarRingSegments) * Mathf.PI * 2.0f;

            Vector3 innerPoint = origin + new Vector3(
                Mathf.Cos(angle) * innerRadius,
                Mathf.Sin(angle) * innerRadius,
                0.0f);

            Vector3 outerPoint = origin + new Vector3(
                Mathf.Cos(angle) * outerRadius,
                Mathf.Sin(angle) * outerRadius,
                0.0f);

            GL.Vertex(innerPoint);
            GL.Vertex(outerPoint);
        }
    }

    // ワイヤーフレーム描画: XY平面の円
    private void DrawCircleWireframe(Vector3 origin, float radius)
    {
        for (int i = 0; i < SonarRingSegments; i++)
        {
            float angle1 = (i / (float)SonarRingSegments) * Mathf.PI * 2.0f;
            float angle2 = ((i + 1) / (float)SonarRingSegments) * Mathf.PI * 2.0f;

            Vector3 p1 = origin + new Vector3(
                Mathf.Cos(angle1) * radius,
                Mathf.Sin(angle1) * radius,
                0.0f);

            Vector3 p2 = origin + new Vector3(
                Mathf.Cos(angle2) * radius,
                Mathf.Sin(angle2) * radius,
                0.0f);

            GL.Vertex(p1);
            GL.Vertex(p2);
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos)
        {
            return;
        }

        Vector3 origin = GetOriginPosition();

        if (lastMaxRadius > 0.0f)
        {
            Gizmos.color = maxRadiusColor;
            Gizmos.DrawWireSphere(origin, lastMaxRadius);
        }

        if (isPulseActive && currentRadius > 0.0f)
        {
            Gizmos.color = currentRingColor;
            Gizmos.DrawWireSphere(origin, currentRadius);
        }
    }
#endif
}