using UnityEngine;

/// ゴールオブジェクト。
/// Player（Tag="Player"）が Trigger に接触すると
/// フェードアウト後に ResultScene へ遷移する。
[RequireComponent(typeof(Collider))]
public sealed class GoalObject : MonoBehaviour
{
    [Header("Fade Settings")]
    [SerializeField, Min(0f)] private float fadeOutDuration = 0.5f;

    // 二重遷移防止フラグ。
    private bool isTriggered;

    private void OnTriggerEnter(Collider other)
    {
        // 既にゴール処理開始済みなら無視する。
        if (isTriggered)
        {
            return;
        }

        // Player 以外のオブジェクトは無視する。
        if (!other.CompareTag("Player"))
        {
            return;
        }

        isTriggered = true;
        Debug.Log("[GoalObject] Player reached the goal.");

        // FadeManager が存在する場合はフェードアウト後に遷移する。
        if (FadeController.Instance != null)
        {
            FadeController.Instance.FadeOut(fadeOutDuration, () =>
            {
                SceneFlow.LoadResult();
            });
        }
        else
        {
            // FadeManager が無い場合は即座に遷移する。
            Debug.LogWarning("[GoalObject] FadeManager not found. Transitioning without fade.");
            SceneFlow.LoadResult();
        }
    }
}
