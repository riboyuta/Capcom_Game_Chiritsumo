using UnityEngine;

/// ゴールオブジェクト。
/// Player（Tag="Player"）が Trigger に接触すると
/// フェードアウト後に ResultScene へ遷移する。
[RequireComponent(typeof(Collider))]
public sealed class GoalObject : MonoBehaviour
{

    // 二重遷移防止フラグ。
    private bool isTriggered;
    [SerializeField] private GameController gameController;

    private void Awake()
    {
        if (gameController == null)
        {
            gameController = FindFirstObjectByType<GameController>();
            if (gameController == null)
            {
                Debug.LogWarning("[GoalObject] GameController not found.");
            }
        }
    }
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

        if (gameController == null)
        {
            Debug.LogWarning("[GoalObject] Goal detected but GameController is missing.");
            return;
        }
        bool accepted = gameController.RequestGoalClear();
        if (!accepted)
        {
            return;
        }

        isTriggered = true;
        Debug.Log("[GoalObject] Player reached the goal. Request accepted.");
    }
}
