using System.Collections.Generic;
using UnityEngine;

public interface IRespawnResettable
{
    void CaptureInitialState();
    void ResetToRespawnState();
}

public sealed class StageResetSystem : MonoBehaviour
{
    private readonly List<IRespawnResettable> resetTargets = new();
    private bool hasCapturedInitialState;

    private void Awake()
    {
        // リセット対象を収集します（初期状態の保存は Start で実施）。
        CollectTargets();
    }

    private void Start()
    {
        // 全ギミックの Awake 完了後に初期状態を一度だけ保存します。
        CaptureInitialStateAll();
    }

    public void ResetAllToRespawnState()
    {
        if (!hasCapturedInitialState)
        {
            Debug.LogWarning("[StageResetSystem] Initial state has not been captured. Reset skipped to avoid capturing runtime state.", this);
            return;
        }

        Debug.Log("[StageResetSystem] Stage reset started", this);

        for (int i = 0; i < resetTargets.Count; i++)
        {
            if (!IsTargetValid(resetTargets[i]))
            {
                continue;
            }

            resetTargets[i].ResetToRespawnState();
        }

        Debug.Log("[StageResetSystem] Stage reset complete", this);
    }

    public void RecollectAndCaptureInitialState()
    {
        // MapLoader などが生成を完了した直後に、最新のReset対象を初期状態として保存する。
        hasCapturedInitialState = false;
        CollectTargets();
        CaptureInitialStateAll();
    }

    private void CollectTargets()
    {
        // シーン内の IRespawnResettable を収集して対象リストを構築します。
        resetTargets.Clear();

        MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IRespawnResettable resettable)
            {
                resetTargets.Add(resettable);
            }
        }

        Debug.Log($"[StageResetSystem] Stage reset targets captured: {resetTargets.Count}", this);
    }

    private void CaptureInitialStateAll()
    {
        // 初期状態は一度だけ保存し、途中状態で上書きしないようにします。
        if (hasCapturedInitialState)
        {
            return;
        }

        for (int i = 0; i < resetTargets.Count; i++)
        {
            if (!IsTargetValid(resetTargets[i]))
            {
                continue;
            }

            resetTargets[i].CaptureInitialState();
        }

        hasCapturedInitialState = true;
        Debug.Log("[StageResetSystem] Initial state capture complete", this);
    }

    private static bool IsTargetValid(IRespawnResettable target)
    {
        if (target == null)
        {
            return false;
        }

        if (target is Object unityObject)
        {
            return unityObject != null;
        }

        return true;
    }
}
