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

    private bool isTransitioning;
    private bool isCountingUp;
    private float targetClearTime;

    private void Start()
    {
        Debug.Log("[ResultSceneController] Start - Scene initialized.");
        Debug.Log($"[ResultSceneController] clearElapsedTimeText assigned: {clearElapsedTimeText != null}");

        if (clearElapsedTimeText != null)
        {
            Debug.Log($"[ResultSceneController] Text component name: {clearElapsedTimeText.gameObject.name}");
        }

        if (FadeController.Instance != null)
        {
            Debug.Log("[ResultSceneController] FadeController found. Starting fade in.");
            FadeController.Instance.FadeIn(0.5f);
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

        bool spacePressed = Input.GetKeyDown(KeyCode.Space);
        bool gamepadConnected = Gamepad.current != null;
        bool gamepadAPressed = gamepadConnected && Gamepad.current.buttonSouth.wasPressedThisFrame;

        // Gamepad状態の定期ログ（入力時のみ）
        if (gamepadAPressed || spacePressed )
        {
            Debug.Log($"[ResultSceneController] Input state - Gamepad connected: {gamepadConnected}");
        }

        if (spacePressed)
        {
            Debug.Log("[ResultSceneController] Space key pressed.");
        }

        if (gamepadAPressed)
        {
            Debug.Log("[ResultSceneController] Gamepad A button pressed.");
        }


        if (spacePressed || gamepadAPressed)
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
            FadeController.Instance.FadeOut(0.5f, () =>
            {
                Debug.Log("[ResultSceneController] Fade out complete. Loading Boot scene.");
                SceneFlow.LoadBoot();
            });
        }
        else
        {
            Debug.LogWarning("[ResultSceneController] FadeController not found. Loading boot without fade.");
            SceneFlow.LoadBoot();
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
}