using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// シーン遷移時のフェードイン/フェードアウト演出を管理するシングルトン。
/// Canvas + CanvasGroup をコードで動的生成するため Prefab は不要。
/// DontDestroyOnLoad で全シーンにわたって永続する。
/// 
/// 使用例:
///   FadeManager.Instance.FadeOut(0.5f, () => SceneFlow.LoadResult());
///   FadeManager.Instance.FadeIn(0.5f);
public sealed class FadeController : MonoBehaviour
{
    public static FadeController Instance { get; private set; }

    // フェード中かどうか。
    public bool IsFading { get; private set; }

    // フェード用の CanvasGroup。alpha を操作して暗転/復帰を行う。
    private CanvasGroup canvasGroup;

    // 実行中のフェードコルーチン。二重起動を防ぐために保持する。
    private Coroutine activeFadeCoroutine;

    private void Awake()
    {
        // シングルトン制御。
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // フェード用 UI をコードで構築する。
        BuildFadeCanvas();

        // 初期状態は完全透明（フェードなし）。
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// 画面を暗転させる（alpha 0 → 1）。
    /// duration..フェードにかける秒数。
    /// onComplete..フェード完了時に呼ばれるコールバック。
    public void FadeOut(float duration, Action onComplete = null)
    {
        StartFade(0f, 1f, duration, onComplete);
    }

    /// 画面を復帰させる（alpha 1 → 0）。
    /// duration..フェードにかける秒数。
    /// onComplete..フェード完了時に呼ばれるコールバック。
    public void FadeIn(float duration, Action onComplete = null)
    {
        StartFade(1f, 0f, duration, onComplete);
    }

    /// フェードを開始する。実行中のフェードがあれば中断して上書きする。
    private void StartFade(float fromAlpha, float toAlpha, float duration, Action onComplete)
    {
        // 実行中のフェードがあれば停止する。
        if (activeFadeCoroutine != null)
        {
            StopCoroutine(activeFadeCoroutine);
        }

        activeFadeCoroutine = StartCoroutine(FadeCoroutine(fromAlpha, toAlpha, duration, onComplete));
    }

    /// alpha を線形補間するコルーチン。
    private IEnumerator FadeCoroutine(float fromAlpha, float toAlpha, float duration, Action onComplete)
    {
        IsFading = true;
        canvasGroup.blocksRaycasts = true;
        canvasGroup.alpha = fromAlpha;

        // duration が 0 以下の場合は即座に完了させる。
        if (duration <= 0f)
        {
            canvasGroup.alpha = toAlpha;
        }
        else
        {
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                canvasGroup.alpha = Mathf.Lerp(fromAlpha, toAlpha, t);
                yield return null;
            }

            canvasGroup.alpha = toAlpha;
        }

        // フェードイン完了（alpha == 0）なら Raycast を通す。
        bool isFullyTransparent = Mathf.Approximately(toAlpha, 0f);
        canvasGroup.blocksRaycasts = !isFullyTransparent;

        IsFading = false;
        activeFadeCoroutine = null;

        onComplete?.Invoke();
    }

    /// フェード用の Canvas / CanvasGroup / Image をコードで動的に生成する。
    private void BuildFadeCanvas()
    {
        // Canvas を持つ子オブジェクトを作成する。
        GameObject fadeCanvasObj = new GameObject("FadeCanvas");
        fadeCanvasObj.transform.SetParent(transform, false);

        Canvas canvas = fadeCanvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999;

        // CanvasScaler は入れておくが特に解像度は指定しない。
        fadeCanvasObj.AddComponent<CanvasScaler>();

        // GraphicRaycaster は Raycast ブロック用。
        fadeCanvasObj.AddComponent<GraphicRaycaster>();

        // CanvasGroup で alpha を一括制御する。
        canvasGroup = fadeCanvasObj.AddComponent<CanvasGroup>();

        // 画面全体を覆う黒パネル。
        GameObject panelObj = new GameObject("FadePanel");
        panelObj.transform.SetParent(fadeCanvasObj.transform, false);

        Image panelImage = panelObj.AddComponent<Image>();
        panelImage.color = Color.black;
        panelImage.raycastTarget = true;

        // RectTransform を画面全体にストレッチさせる。
        RectTransform panelRect = panelObj.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
    }
}
