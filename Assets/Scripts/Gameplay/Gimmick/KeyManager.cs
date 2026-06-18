using System.Collections.Generic;
using UnityEngine;

public class KeyManager : MonoBehaviour, IRespawnResettable
{
    [Header("Keys")]
    [SerializeField] private KeyCollectible[] keys;

    private int collectedCount;
    private int totalKeysCount;
    private bool hasCapturedInitialState;

    private int initialCollectedCount;
    private bool initialIsCompleted;

    public bool IsCompleted { get; private set; }

    private void Awake()
    {
        if (keys == null || keys.Length == 0)
        {
            keys = GetComponentsInChildren<KeyCollectible>(true);
        }

        EnsureStableKeys();

        totalKeysCount = keys.Length;

        foreach (KeyCollectible key in keys)
        {
            if (key != null)
            {
                key.Initialize(this);
            }
        }
    }

    private void EnsureStableKeys()
    {
        if (keys == null)
        {
            keys = System.Array.Empty<KeyCollectible>();
            return;
        }

        bool hasNull = false;
        for (int i = 0; i < keys.Length; i++)
        {
            if (keys[i] == null)
            {
                hasNull = true;
                break;
            }
        }

        if (!hasNull)
        {
            return;
        }

        List<KeyCollectible> sanitizedKeys = new List<KeyCollectible>(keys.Length);
        for (int i = 0; i < keys.Length; i++)
        {
            if (keys[i] != null)
            {
                sanitizedKeys.Add(keys[i]);
            }
        }

        Debug.LogWarning($"[{nameof(KeyManager)}] Removed null KeyCollectible references: {name}", this);
        keys = sanitizedKeys.ToArray();
    }

    public void NotifyKeyCollected()
    {
        collectedCount++;

        if (totalKeysCount <= 0 || collectedCount < totalKeysCount)
        {
            return;
        }

        IsCompleted = true;
        AudioEvent.Emit(this, "Completed");
    }

    public void CaptureInitialState()
    {
        if (hasCapturedInitialState)
        {
            return;
        }

        initialCollectedCount = collectedCount;
        initialIsCompleted = IsCompleted;
        hasCapturedInitialState = true;
    }

    public void ResetToRespawnState()
    {
        if (!hasCapturedInitialState)
        {
            CaptureInitialState();
        }

        collectedCount = initialCollectedCount;
        IsCompleted = initialIsCompleted;
    }

}
