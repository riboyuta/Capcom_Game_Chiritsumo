using UnityEngine;

/// ゴールオブジェクト。
/// Player（Tag="Player"）が Trigger に接触すると
/// フェードアウト後に ResultScene へ遷移する。
[RequireComponent(typeof(Collider))]
public sealed class GoalObject : MonoBehaviour
{

    // 二重遷移防止フラグ。
    private bool isTriggered;
    [SerializeField] private GameRoot gameRoot;

    private void Awake()
    {
        if (gameRoot == null)
        {
            gameRoot = FindFirstObjectByType<GameRoot>();
            if (gameRoot == null)
            {
                Debug.LogWarning("[GoalObject] GameRoot not found.");
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

        if (gameRoot == null)
        {
            Debug.LogWarning("[GoalObject] Goal detected but GameRoot is missing.");
            return;
        }
        bool accepted = gameRoot.RequestGoalClear();
        if (!accepted)
        {
            return;
        }

        isTriggered = true;
        Debug.Log("[GoalObject] Player reached the goal. Request accepted.");
    }
}
