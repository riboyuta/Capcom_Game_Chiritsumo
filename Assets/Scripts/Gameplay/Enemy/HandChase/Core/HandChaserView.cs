using UnityEngine;

public sealed class HandChaserView : MonoBehaviour
{
    [Header("見た目ルート")]
    [Tooltip("見た目のルートTransformです。")]
    [SerializeField] private Transform visualRoot;

    [Header("腕レンダラー")]
    [Tooltip("腕のSpriteRendererです。")]
    [SerializeField] private SpriteRenderer armRenderer;

    [Header("手のひらレンダラー")]
    [Tooltip("手のひらのSpriteRendererです。")]
    [SerializeField] private SpriteRenderer palmRenderer;

    [Header("ルート位置オフセット")]
    [Tooltip("visualRoot のローカル位置オフセットです。")]
    [SerializeField] private Vector3 visualRootLocalOffset = Vector3.zero;

    [Header("ルートスケール")]
    [Tooltip("visualRoot のローカルスケールです。")]
    [SerializeField] private Vector3 visualRootLocalScale = Vector3.one;

    [Header("腕位置オフセット")]
    [Tooltip("armRenderer のローカル位置オフセットです。")]
    [SerializeField] private Vector3 armLocalOffset = Vector3.zero;

    [Header("腕スケール")]
    [Tooltip("armRenderer のローカルスケールです。")]
    [SerializeField] private Vector3 armLocalScale = Vector3.one;

    [Header("手のひら位置オフセット")]
    [Tooltip("palmRenderer のローカル位置オフセットです。")]
    [SerializeField] private Vector3 palmLocalOffset = Vector3.zero;

    [Header("手のひらスケール")]
    [Tooltip("palmRenderer のローカルスケールです。")]
    [SerializeField] private Vector3 palmLocalScale = Vector3.one;

    [Header("腕フレーム")]
    [Tooltip("腕のスプライトフレーム配列です。")]
    [SerializeField] private Sprite[] armFrames;

    [Header("手のひらフレーム")]
    [Tooltip("手のひらのスプライトフレーム配列です。")]
    [SerializeField] private Sprite[] palmFrames;

    [Header("アニメーションFPS")]
    [Tooltip("アニメーションのFPSです。")]
    [SerializeField, Min(0f)] private float animationFps = 8.0f;

    private float animationTimer;

    private void Awake()
    {
        // ルートが未設定なら自分自身を使用
        if (visualRoot == null)
        {
            visualRoot = transform;
        }

        // Rendererを自動取得試行
        TryAutoAssignRenderers();
    }

    private void OnValidate()
    {
        // FPSを正の値に制限
        animationFps = Mathf.Max(0f, animationFps);

        // スケールが0の軸があれば1に修正（非表示防止）
        if (visualRootLocalScale.x == 0f) visualRootLocalScale.x = 1f;
        if (visualRootLocalScale.y == 0f) visualRootLocalScale.y = 1f;
        if (visualRootLocalScale.z == 0f) visualRootLocalScale.z = 1f;

        if (armLocalScale.x == 0f) armLocalScale.x = 1f;
        if (armLocalScale.y == 0f) armLocalScale.y = 1f;
        if (armLocalScale.z == 0f) armLocalScale.z = 1f;

        if (palmLocalScale.x == 0f) palmLocalScale.x = 1f;
        if (palmLocalScale.y == 0f) palmLocalScale.y = 1f;
        if (palmLocalScale.z == 0f) palmLocalScale.z = 1f;

        if (visualRoot == null)
        {
            visualRoot = transform;
        }

        TryAutoAssignRenderers();
    }

    private void Update()
    {
        // アニメーションとレイアウトを毎フレーム更新
        UpdateAnimation();
        ApplyVisualLayout();
    }

    // スプライトアニメーションを更新
    private void UpdateAnimation()
    {
        // FPSが0以下なら最初のフレームを表示
        if (animationFps <= 0f)
        {
            ApplyFrame(armRenderer, armFrames, 0);
            ApplyFrame(palmRenderer, palmFrames, 0);
            return;
        }

        // タイマーを進める
        animationTimer += Time.deltaTime;

        // ループ再生するフレームインデックスを計算
        int armIndex = GetLoopFrameIndex(armFrames, animationTimer, animationFps);
        int palmIndex = GetLoopFrameIndex(palmFrames, animationTimer, animationFps);

        // フレームを適用
        ApplyFrame(armRenderer, armFrames, armIndex);
        ApplyFrame(palmRenderer, palmFrames, palmIndex);
    }

    // 視覚的レイアウト（位置とスケール）を適用
    private void ApplyVisualLayout()
    {
        // ルートの位置とスケールを設定
        if (visualRoot != null && visualRoot != transform)
        {
            visualRoot.localPosition = visualRootLocalOffset;
            visualRoot.localScale = visualRootLocalScale;
        }

        // 腕の位置とスケールを設定
        if (armRenderer != null)
        {
            armRenderer.transform.localPosition = armLocalOffset;
            armRenderer.transform.localScale = armLocalScale;
        }

        // 手の平の位置とスケールを設定
        if (palmRenderer != null)
        {
            palmRenderer.transform.localPosition = palmLocalOffset;
            palmRenderer.transform.localScale = palmLocalScale;
        }
    }

    // 時間とFPSからループ再生するフレームインデックスを計算
    private static int GetLoopFrameIndex(Sprite[] frames, float time, float fps)
    {
        // フレームがない場合は-1を返す
        if (frames == null || frames.Length == 0)
        {
            return -1;
        }

        // 現在の時間からフレームインデックスを計算し、フレーム数で剰余を取ってループ
        int index = Mathf.FloorToInt(time * fps) % frames.Length;
        if (index < 0)
        {
            index += frames.Length;  // 負の値の場合は正に変換
        }

        return index;
    }

    // 指定したインデックスのスプライトをRendererに適用
    private static void ApplyFrame(SpriteRenderer target, Sprite[] frames, int index)
    {
        // 対象がない、またはフレームがない場合は何もしない
        if (target == null || frames == null || frames.Length == 0)
        {
            return;
        }

        // インデックスが範囲外なら何もしない
        if (index < 0 || index >= frames.Length)
        {
            return;
        }

        // スプライトを適用
        target.sprite = frames[index];
    }

    // 子オブジェクトからRendererを自動取得試行
    private void TryAutoAssignRenderers()
    {
        if (visualRoot == null)
        {
            return;
        }

        // "ArmRenderer"という名前の子オブジェクトから腕のRendererを取得
        if (armRenderer == null)
        {
            armRenderer = visualRoot.Find("ArmRenderer")?.GetComponent<SpriteRenderer>();
        }

        // "PalmRenderer"という名前の子オブジェクトから手の平のRendererを取得
        if (palmRenderer == null)
        {
            palmRenderer = visualRoot.Find("PalmRenderer")?.GetComponent<SpriteRenderer>();
        }
    }
}
