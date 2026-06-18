using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class BlinkRespawnTransitionView : MonoBehaviour
{
    [Header("参照")]
    [Tooltip("画面全体を覆う RawImage です。")]
    [SerializeField] private RawImage screenImage;

    [Tooltip("Hidden/UI/BlinkRespawnTransition を使った Material です。")]
    [SerializeField] private Material blinkMaterial;

    [Header("時間")]
    [Tooltip("幕が閉じる時間です。")]
    [SerializeField, Min(0.01f)] private float closeDuration = 0.22f;

    [Tooltip("幕が開く時間です。")]
    [SerializeField, Min(0.01f)] private float openDuration = 0.28f;

    [Tooltip("完全に閉じた状態を維持する時間です。")]
    [SerializeField, Min(0f)] private float closedHoldTime = 0.04f;

    [Header("見た目")]
    [Tooltip("幕の色です。基本は黒で問題ありません。")]
    [SerializeField] private Color transitionColor = Color.black;

    [Tooltip("楕円の境界のぼかし量です。")]
    [SerializeField, Range(0.001f, 0.2f)] private float edgeSoftness = 0.035f;

    [Tooltip("横方向の開き幅です。大きいほど横に広い楕円になります。")]
    [SerializeField, Min(1f)] private float horizontalRadius = 1.8f;

    [Tooltip("縦方向の開き幅です。大きいほど閉じ始めがゆるくなります。")]
    [SerializeField, Min(1f)] private float verticalRadius = 1.7f;

    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int OpenAmountId = Shader.PropertyToID("_OpenAmount");
    private static readonly int SoftnessId = Shader.PropertyToID("_Softness");
    private static readonly int AspectId = Shader.PropertyToID("_Aspect");
    private static readonly int HorizontalRadiusId = Shader.PropertyToID("_HorizontalRadius");
    private static readonly int VerticalRadiusId = Shader.PropertyToID("_VerticalRadius");

    private Material runtimeMaterial;
    private Coroutine runningCoroutine;

    private void Awake()
    {
        if (screenImage == null)
        {
            screenImage = GetComponentInChildren<RawImage>(true);
        }

        if (screenImage != null && blinkMaterial != null)
        {
            runtimeMaterial = Instantiate(blinkMaterial);
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

    public IEnumerator PlayClose()
    {
        StopRunningCoroutine();

        SetImageEnabled(true);
        yield return AnimateOpenAmount(1f, 0f, closeDuration);

        if (closedHoldTime > 0f)
        {
            yield return WaitUnscaled(closedHoldTime);
        }
    }

    public IEnumerator PlayOpen()
    {
        StopRunningCoroutine();

        SetImageEnabled(true);
        yield return AnimateOpenAmount(0f, 1f, openDuration);

        ResetImmediate();
    }

    public void ResetImmediate()
    {
        StopRunningCoroutine();
        SetOpenAmount(1f);
        SetImageEnabled(false);
    }

    public void ForceClosed()
    {
        StopRunningCoroutine();
        SetImageEnabled(true);
        SetOpenAmount(0f);
    }

    private IEnumerator AnimateOpenAmount(float from, float to, float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(elapsed / duration);
            t = SmoothStep(t);

            SetOpenAmount(Mathf.Lerp(from, to, t));

            yield return null;
        }

        SetOpenAmount(to);
    }

    private IEnumerator WaitUnscaled(float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private void SetOpenAmount(float openAmount)
    {
        if (runtimeMaterial == null)
        {
            return;
        }

        float aspect = Screen.height > 0
            ? (float)Screen.width / Screen.height
            : 1f;

        runtimeMaterial.SetColor(ColorId, transitionColor);
        runtimeMaterial.SetFloat(OpenAmountId, Mathf.Clamp01(openAmount));
        runtimeMaterial.SetFloat(SoftnessId, edgeSoftness);
        runtimeMaterial.SetFloat(AspectId, aspect);
        runtimeMaterial.SetFloat(HorizontalRadiusId, horizontalRadius);
        runtimeMaterial.SetFloat(VerticalRadiusId, verticalRadius);
    }

    private void SetImageEnabled(bool enabled)
    {
        if (screenImage == null)
        {
            return;
        }

        screenImage.enabled = enabled;
        screenImage.raycastTarget = enabled;
    }

    private void StopRunningCoroutine()
    {
        if (runningCoroutine == null)
        {
            return;
        }

        StopCoroutine(runningCoroutine);
        runningCoroutine = null;
    }

    private float SmoothStep(float t)
    {
        return t * t * (3f - 2f * t);
    }
}