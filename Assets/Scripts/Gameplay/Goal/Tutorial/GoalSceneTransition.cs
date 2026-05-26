using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// ゴールオブジェクト用のシーン遷移スクリプト。
/// Player（Tag="Player"）が Trigger に接触すると
/// 指定されたシーンへ遷移する。
/// </summary>
[RequireComponent(typeof(Collider))]
public sealed class GoalSceneTransition : MonoBehaviour
{
    [Header("Scene Settings")]
    [SerializeField, Tooltip("遷移先のシーン名またはパス")]
    private string targetScenePath = "Assets/Scenes/DebugScenes/Tomoya/DebugMapScenes/TestPlay/DebugMapTestPlay_v4";

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
        Debug.Log($"[GoalSceneTransition] Player reached the goal. Transitioning to: {targetScenePath}");

        StartCoroutine(TransitionToScene());
    }

    private System.Collections.IEnumerator TransitionToScene()
    {
        // フェードアウト処理
        if (FadeController.Instance != null)
        {
            FadeController.Instance.FadeOut(fadeOutDuration);
            yield return new WaitForSeconds(fadeOutDuration + waitAfterFade);
        }
        else
        {
            Debug.LogWarning("[GoalSceneTransition] FadeController not found. Skipping fade effect.");
            yield return new WaitForSeconds(0.5f);
        }

        // シーン名を取得（パスからファイル名のみ抽出）
        string sceneName = System.IO.Path.GetFileNameWithoutExtension(targetScenePath);

        Debug.Log($"[GoalSceneTransition] Loading scene: {sceneName}");
        SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
    }
}
