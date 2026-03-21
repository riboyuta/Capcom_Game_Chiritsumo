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
        CollectTargets();
        CaptureInitialStateAll();
    }

    public void ResetAllToRespawnState()
    {
        if (!hasCapturedInitialState)
        {
            CaptureInitialStateAll();
        }

        Debug.Log("[StageResetSystem] Stage reset started", this);

        for (int i = 0; i < resetTargets.Count; i++)
        {
            resetTargets[i].ResetToRespawnState();
        }

        Debug.Log("[StageResetSystem] Stage reset complete", this);
    }

    private void CollectTargets()
    {
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
        for (int i = 0; i < resetTargets.Count; i++)
        {
            resetTargets[i].CaptureInitialState();
        }

        hasCapturedInitialState = true;
    }
}