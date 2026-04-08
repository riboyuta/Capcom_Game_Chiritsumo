using UnityEngine;

/// <summary>
/// HandChaser敵の見た目（アニメーション・レイアウト）を制御するコンポーネント。
/// </summary>
public sealed class HandChaserView : MonoBehaviour
{
    [Header("見た目参照")]
    [Tooltip("見た目のルートTransformです。")]
    [SerializeField] private Transform visualRoot;

    [Tooltip("腕のSpriteRendererです。")]
    [SerializeField] private SpriteRenderer armRenderer;

    [Tooltip("手のひらのSpriteRendererです。")]
    [SerializeField] private SpriteRenderer palmRenderer;

    [Header("見た目: 全体調整")]
    [Tooltip("visualRoot のローカル位置オフセットです。")]
    [SerializeField] private Vector3 visualRootLocalOffset = Vector3.zero;

    [Tooltip("visualRoot のローカルスケールです。")]
    [SerializeField] private Vector3 visualRootLocalScale = Vector3.one;

    [Header("見た目: 腕調整")]
    [Tooltip("armRenderer のローカル位置オフセットです。")]
    [SerializeField] private Vector3 armLocalOffset = Vector3.zero;

    [Tooltip("armRenderer のローカルスケールです。")]
    [SerializeField] private Vector3 armLocalScale = Vector3.one;

    [Header("見た目: 手の平調整")]
    [Tooltip("palmRenderer のローカル位置オフセットです。")]
    [SerializeField] private Vector3 palmLocalOffset = Vector3.zero;

    [Tooltip("palmRenderer のローカルスケールです。")]
    [SerializeField] private Vector3 palmLocalScale = Vector3.one;

    [Header("アニメーション")]
    [Tooltip("腕のスプライトフレーム配列です。")]
    [SerializeField] private Sprite[] armFrames;

    [Tooltip("手のひらのスプライトフレーム配列です。")]
    [SerializeField] private Sprite[] palmFrames;

    [Tooltip("アニメーションのFPSです。")]
    [SerializeField, Min(0f)] private float animationFps = 8.0f;

    private float animationTimer;

    private void Awake()
    {
        if (visualRoot == null)
        {
            visualRoot = transform;
        }

        TryAutoAssignRenderers();
    }

    private void OnValidate()
    {
        animationFps = Mathf.Max(0f, animationFps);

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
        UpdateAnimation();
        ApplyVisualLayout();
    }

    private void UpdateAnimation()
    {
        if (animationFps <= 0f)
        {
            ApplyFrame(armRenderer, armFrames, 0);
            ApplyFrame(palmRenderer, palmFrames, 0);
            return;
        }

        animationTimer += Time.deltaTime;

        int armIndex = GetLoopFrameIndex(armFrames, animationTimer, animationFps);
        int palmIndex = GetLoopFrameIndex(palmFrames, animationTimer, animationFps);

        ApplyFrame(armRenderer, armFrames, armIndex);
        ApplyFrame(palmRenderer, palmFrames, palmIndex);
    }

    private void ApplyVisualLayout()
    {
        if (visualRoot != null && visualRoot != transform)
        {
            visualRoot.localPosition = visualRootLocalOffset;
            visualRoot.localScale = visualRootLocalScale;
        }

        if (armRenderer != null)
        {
            armRenderer.transform.localPosition = armLocalOffset;
            armRenderer.transform.localScale = armLocalScale;
        }

        if (palmRenderer != null)
        {
            palmRenderer.transform.localPosition = palmLocalOffset;
            palmRenderer.transform.localScale = palmLocalScale;
        }
    }

    private static int GetLoopFrameIndex(Sprite[] frames, float time, float fps)
    {
        if (frames == null || frames.Length == 0)
        {
            return -1;
        }

        int index = Mathf.FloorToInt(time * fps) % frames.Length;
        if (index < 0)
        {
            index += frames.Length;
        }

        return index;
    }

    private static void ApplyFrame(SpriteRenderer target, Sprite[] frames, int index)
    {
        if (target == null || frames == null || frames.Length == 0)
        {
            return;
        }

        if (index < 0 || index >= frames.Length)
        {
            return;
        }

        target.sprite = frames[index];
    }

    private void TryAutoAssignRenderers()
    {
        if (visualRoot == null)
        {
            return;
        }

        if (armRenderer == null)
        {
            armRenderer = visualRoot.Find("ArmRenderer")?.GetComponent<SpriteRenderer>();
        }

        if (palmRenderer == null)
        {
            palmRenderer = visualRoot.Find("PalmRenderer")?.GetComponent<SpriteRenderer>();
        }
    }
}
