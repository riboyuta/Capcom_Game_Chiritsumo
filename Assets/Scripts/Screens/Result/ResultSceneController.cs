using Game.Input;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class ResultSceneController : MonoBehaviour
{
    [Header("Result UI")]
    [SerializeField] private TMP_Text clearElapsedTimeText;
    [SerializeField] private string clearElapsedTimeFormat = "Clear Time: {0:F2}s";
    [Tooltip("タイムがカウントアップする時間（秒）")]
    [SerializeField] private float countUpDuration = 1.5f;
    [Header("入力参照")]
    [Tooltip("タイトル画面で使用する生入力取得元。未設定の場合は同じGameObjectから自動取得を試みる。")]
    [SerializeField] private RawInputSource rawInputSource;

    [Header("入力設定")]
    [Tooltip("タイトル画面のメニュー操作に使う入力バインド定義。上下移動と決定操作の割り当てをここで管理する。")]
    [SerializeField] private SystemInputBindings inputBindings = new SystemInputBindings();

    private SystemInputReader systemInputReader;
    private bool isTransitioning;
    private bool isCountingUp;
    private float targetClearTime;
    private void Awake()
    {
        if (!TryResolveRawInputSource())
        {
            return;
        }

        systemInputReader = new SystemInputReader(rawInputSource, inputBindings);
    }

    private void Start()
    {
        // Result シーンの BGM を再生する。
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.FadeIn("BGM_result", 1.0f);
        }

        Debug.Log("[ResultSceneController] Start - Scene initialized.");
        Debug.Log($"[ResultSceneController] clearElapsedTimeText assigned: {clearElapsedTimeText != null}");

        if (clearElapsedTimeText != null)
        {
            Debug.Log($"[ResultSceneController] Text component name: {clearElapsedTimeText.gameObject.name}");
        }

        if (FadeController.Instance != null)
        {
            Debug.Log("[ResultSceneController] FadeController found. Starting fade in.");
            FadeController.Instance.FadeIn();
        }
        else
        {
            Debug.LogWarning("[ResultSceneController] FadeController not found.");
        }

        if (ResultSceneTransitData.TryConsumeClearElapsedTime(out float clearElapsedTime))
        {
            Debug.Log($"[ResultSceneController] clearElapsedTime received={clearElapsedTime:F2}s");
            ApplyClearElapsedTime(clearElapsedTime);
            return;
        }

        Debug.LogWarning("[ResultSceneController] clearElapsedTime is not provided. Displaying default value.");

        if (clearElapsedTimeText != null)
        {
            clearElapsedTimeText.text = "--.--";
            Debug.Log("[ResultSceneController] Default time text set to '--.--'");
        }
        else
        {
            Debug.LogError("[ResultSceneController] Cannot display default time - clearElapsedTimeText is null!");
        }
    }

    private void Update()
    {
        if (isTransitioning)
        {
            return;
        }
        systemInputReader.Update();
        Debug.Log($"[ResultSceneController] SubmitPressed={systemInputReader.SubmitPressed}");

        if (systemInputReader.SubmitPressed)
        {
            if (isCountingUp)
            {
                // カウントアップのアニメーションをスキップして即座に最終結果を表示
                isCountingUp = false;
                clearElapsedTimeText.text = string.Format(clearElapsedTimeFormat, targetClearTime);
                Debug.Log("[ResultSceneController] Count up skipped.");
            }
            else
            {
                ReturnToTitle();
            }
        }
    }

    public void ReturnToTitle()
    {
        // BGM を停止する。
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.Stop("BGM_result");
        }
        if (isTransitioning)
        {
            Debug.LogWarning("[ResultSceneController] ReturnToTitle called but already transitioning.");
            return;
        }

        isTransitioning = true;
        Debug.Log("[ResultSceneController] ReturnToTitle - Starting transition to Boot scene.");

        if (FadeController.Instance != null)
        {
            Debug.Log("[ResultSceneController] Starting fade out...");
            FadeController.Instance.FadeOut(onComplete: () =>
            {
                Debug.Log("[ResultSceneController] Fade out complete. Loading Boot scene.");
                SceneFlow.LoadTitle();
            });
        }
        else
        {
            Debug.LogWarning("[ResultSceneController] FadeController not found. Loading boot without fade.");
            SceneFlow.LoadTitle();
        }
    }

    private void ApplyClearElapsedTime(float clearElapsedTime)
    {
        Debug.Log($"[ResultSceneController] ApplyClearElapsedTime called with time={clearElapsedTime:F2}s");
        Debug.Log($"[ResultSceneController] clearElapsedTimeText is null: {clearElapsedTimeText == null}");

        if (clearElapsedTimeText == null)
        {
            Debug.LogError("[ResultSceneController] clearElapsedTimeText is not assigned in Inspector!");
            Debug.LogError("[ResultSceneController] Please assign a TMP_Text component in the Inspector.");
            return;
        }

        Debug.Log($"[ResultSceneController] Text object: {clearElapsedTimeText.gameObject.name}, Active: {clearElapsedTimeText.gameObject.activeInHierarchy}");

        targetClearTime = clearElapsedTime;
        StartCoroutine(CountUpClearTime(clearElapsedTime));
    }

    private System.Collections.IEnumerator CountUpClearTime(float targetTime)
    {
        isCountingUp = true;
        float elapsedTime = 0f;

        while (elapsedTime < countUpDuration && isCountingUp)
        {
            elapsedTime += Time.deltaTime;
            
            // 0から1へ正規化された時間
            float t = Mathf.Clamp01(elapsedTime / countUpDuration);
            // EaseOutCubic（最後に向かってゆっくりになるイージング）
            float tEased = 1f - Mathf.Pow(1f - t, 3f);
            
            float currentTime = Mathf.Lerp(0f, targetTime, tEased);

            clearElapsedTimeText.text = string.Format(clearElapsedTimeFormat, currentTime);
            yield return null;
        }

        if (isCountingUp)
        {
            // スキップされずにアニメーションが終わった場合、最終値を正確に設定
            clearElapsedTimeText.text = string.Format(clearElapsedTimeFormat, targetTime);
            isCountingUp = false;
        }

        Debug.Log($"[ResultSceneController] ✓ Time displayed: {clearElapsedTimeText.text}");
    }
    private bool TryResolveRawInputSource()
    {
        if (rawInputSource != null)
        {
            return true;
        }

        rawInputSource = GetComponent<RawInputSource>();

        if (rawInputSource != null)
        {
            return true;
        }

        Debug.LogError("[ResultSceneController] RawInputSource is required.", this);
        enabled = false;
        return false;
    }

}


