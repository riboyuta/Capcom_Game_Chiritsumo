using UnityEngine;

/// ゴールシーン用コントローラー。
/// DebugGoalToResult シーンおよび将来的なゴール処理を持つシーンで使用する。
/// デバッグ入力（NextScenePressed）で ResultScene へ即遷移する機能も持つ。
public sealed class GoalSceneController : MonoBehaviour
{
    // 入力連打で二重遷移防止。
    private bool isTransitioning;

    [Header("Fade Settings")]
    [SerializeField, Min(0f)] private float fadeInDuration = 0.5f;

    private void Start()
    {
        Debug.Log("[GoalSceneController] Goal scene started.");

        // シーン開始時にフェードインする。
        if (FadeController.Instance != null)
        {
            FadeController.Instance.FadeIn(fadeInDuration);
        }
    }

    private void Update()
    {
        if (isTransitioning)
        {
            return;
        }

        // デバッグ入力で ResultScene へ遷移する。
        if (BootSceneController.Instance != null &&
            BootSceneController.Instance.DebugInput.NextScenePressed)
        {
            TransitionToResult();
        }
    }

    /// ResultScene へ遷移する。
    public void TransitionToResult()
    {
        if (isTransitioning)
        {
            return;
        }

        isTransitioning = true;
        SceneFlow.LoadResult();
    }
}
