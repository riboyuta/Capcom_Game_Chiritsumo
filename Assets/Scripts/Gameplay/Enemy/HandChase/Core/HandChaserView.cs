using UnityEngine;

public sealed class HandChaserView : MonoBehaviour
{
    [Header("見た目ルート")]
    [Tooltip("見た目のルートTransformです。")]
    [SerializeField] private Transform visualRoot;

    [Header("手レンダラー")]
    [Tooltip("手のSpriteRendererです。β版以降はモデルに変更予定。")]
    [SerializeField] private SpriteRenderer handRenderer;

    [Header("ルート位置オフセット")]
    [Tooltip("visualRoot のローカル位置オフセットです。")]
    [SerializeField] private Vector3 visualRootLocalOffset = Vector3.zero;

    [Header("ルートスケール")]
    [Tooltip("visualRoot のローカルスケールです。")]
    [SerializeField] private Vector3 visualRootLocalScale = Vector3.one;

    [Header("手位置オフセット")]
    [Tooltip("handRenderer のローカル位置オフセットです。")]
    [SerializeField] private Vector3 handLocalOffset = Vector3.zero;

    [Header("手スケール")]
    [Tooltip("handRenderer のローカルスケールです。")]
    [SerializeField] private Vector3 handLocalScale = Vector3.one;

    [Header("手フレーム")]
    [Tooltip("手のスプライトフレーム配列です。")]
    [SerializeField] private Sprite[] handFrames;

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
        TryAutoAssignRenderer();
    }

    private void OnValidate()
    {
        // FPSを正の値に制限
        animationFps = Mathf.Max(0f, animationFps);

        // スケールが0の軸があれば1に修正（非表示防止）
        if (visualRootLocalScale.x == 0f) visualRootLocalScale.x = 1f;
        if (visualRootLocalScale.y == 0f) visualRootLocalScale.y = 1f;
        if (visualRootLocalScale.z == 0f) visualRootLocalScale.z = 1f;

        if (handLocalScale.x == 0f) handLocalScale.x = 1f;
        if (handLocalScale.y == 0f) handLocalScale.y = 1f;
        if (handLocalScale.z == 0f) handLocalScale.z = 1f;

        if (visualRoot == null)
        {
            visualRoot = transform;
        }

        TryAutoAssignRenderer();
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
            ApplyFrame(handRenderer, handFrames, 0);
            return;
        }

        // タイマーを進める
        animationTimer += Time.deltaTime;

        // ループ再生するフレームインデックスを計算
        int handIndex = GetLoopFrameIndex(handFrames, animationTimer, animationFps);

        // フレームを適用
        ApplyFrame(handRenderer, handFrames, handIndex);
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

        // 手の位置とスケールを設定
        if (handRenderer != null)
        {
            handRenderer.transform.localPosition = handLocalOffset;
            handRenderer.transform.localScale = handLocalScale;
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
    private void TryAutoAssignRenderer()
    {
        if (visualRoot == null)
        {
            return;
        }

        // "HandRenderer"という名前の子オブジェクトから手のRendererを取得
        if (handRenderer == null)
        {
            handRenderer = visualRoot.Find("HandRenderer")?.GetComponent<SpriteRenderer>();
        }

        // 見つからなければ最初のSpriteRendererを使用
        if (handRenderer == null)
        {
            handRenderer = visualRoot.GetComponentInChildren<SpriteRenderer>();
        }
    }
}
