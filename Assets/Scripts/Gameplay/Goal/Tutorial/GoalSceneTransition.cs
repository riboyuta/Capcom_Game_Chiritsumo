using UnityEngine;


// ゴールオブジェクト用のシーン遷移スクリプト。
// Player（Tag="Player"）が Trigger に接触すると Stage1 へ遷移する。
[RequireComponent(typeof(Collider))]
public sealed class GoalSceneTransition : MonoBehaviour
{
    private const float DefaultFadeOutDuration = 1.0f;

    [Header("Fade Settings")]
    [SerializeField, Tooltip("フェードアウト時間（秒）")]
    private float fadeOutDuration = 1.0f;

    [SerializeField, Tooltip("フェード後の待機時間（秒）")]
    private float waitAfterFade = 0.5f;

    // 二重遷移防止フラグ
    private bool isTriggered;

    private void OnTriggerEnter(Collider other)
    {
        // 既にゴール処理開始済みなら無視する
        if (isTriggered)
        {
            return;
        }

        // Player 以外のオブジェクトは無視する
        if (!other.CompareTag("Player"))
        {
            return;
        }

        isTriggered = true;
        Debug.Log("[GoalSceneTransition] Player reached the goal. Transitioning to Stage1.");

        StartCoroutine(TransitionToStage1());
    }

    private System.Collections.IEnumerator TransitionToStage1()
    {
        float actualFadeOutDuration = fadeOutDuration > 0f ? fadeOutDuration : DefaultFadeOutDuration;
        bool fadeComplete = false;
        FadeController.EnsureInstance().FadeOut(actualFadeOutDuration, onComplete: () => fadeComplete = true);
        yield return new UnityEngine.WaitUntil(() => fadeComplete);
        yield return new UnityEngine.WaitForSeconds(waitAfterFade);

        SceneFlow.LoadGame();
    }
}
