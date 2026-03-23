using TMPro;
using UnityEngine;

public sealed class ResultSceneController : MonoBehaviour
{
    [Header("Result UI")]
    [SerializeField] private TMP_Text clearElapsedTimeText;
    [SerializeField] private string clearElapsedTimeFormat = "Clear Time: {0:F2}s";

    private bool isTransitioning;

    private void Start()
    {
        if (FadeController.Instance != null)
        {
            FadeController.Instance.FadeIn(0.5f);
        }

        if (ResultSceneTransitData.TryConsumeClearElapsedTime(out float clearElapsedTime))
        {
            Debug.Log($"[ResultSceneController] clearElapsedTime={clearElapsedTime:F2}s");
            ApplyClearElapsedTime(clearElapsedTime);
            return;
        }

        Debug.LogWarning("[ResultSceneController] clearElapsedTime is not provided.");

        if (clearElapsedTimeText != null)
        {
            clearElapsedTimeText.text = "--.--";
        }
    }

    private void Update()
    {
        if (isTransitioning)
        {
            return;
        }

        if (BootSceneController.Instance != null && BootSceneController.Instance.DebugInput.NextScenePressed)
        {
            ReturnToTitle();
        }
    }

    public void ReturnToTitle()
    {
        if (isTransitioning)
        {
            return;
        }

        isTransitioning = true;

        if (FadeController.Instance != null)
        {
            FadeController.Instance.FadeOut(0.5f, () =>
            {
                SceneFlow.LoadTitle();
            });
        }
        else
        {
            Debug.LogWarning("[ResultSceneController] FadeController not found. Loading title without fade.");
            SceneFlow.LoadTitle();
        }
    }

    private void ApplyClearElapsedTime(float clearElapsedTime)
    {
        if (clearElapsedTimeText == null)
        {
            Debug.LogWarning("[ResultSceneController] clearElapsedTimeText is not assigned.");
            return;
        }

        clearElapsedTimeText.text = string.Format(clearElapsedTimeFormat, clearElapsedTime);
    }
}