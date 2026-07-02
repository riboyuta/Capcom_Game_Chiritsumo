using System;
using System.Collections.Generic;
using UnityEngine;

public enum GameEffectType
{
    OneShot,
    Loop,
    Interval
}

[CreateAssetMenu(
    fileName = "GameEffectConfig",
    menuName = "Game/Effects/Game Effect Config")]
public sealed class GameEffectConfig : ScriptableObject
{
    [Header("エフェクト一覧")]
    [SerializeField] private List<GameEffectEntry> effects = new();

    private Dictionary<GameEffectKey, GameEffectEntry> effectMap;

    public bool TryGet(GameEffectKey key, out GameEffectEntry entry)
    {
        BuildMapIfNeeded();
        return effectMap.TryGetValue(key, out entry);
    }

    private void BuildMapIfNeeded()
    {
        if (effectMap != null)
        {
            return;
        }

        effectMap = new Dictionary<GameEffectKey, GameEffectEntry>();

        foreach (var effect in effects)
        {
            if (effect == null || effect.Key == GameEffectKey.None)
            {
                continue;
            }

            effectMap[effect.Key] = effect;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        effectMap = null;
    }
#endif
}

[Serializable]
public sealed class GameEffectEntry
{
    [Header("識別")]
    [SerializeField] private GameEffectKey key;
    [SerializeField] private GameEffectType type;

    [Header("Prefab")]
    [SerializeField] private GameObject prefab;

    [Header("Transform")]
    [SerializeField] private Vector3 offset;
    [SerializeField] private Vector3 scale = Vector3.one;
    [SerializeField] private bool followTarget;
    [SerializeField] private bool inheritRotation;

    [Header("再生")]
    [SerializeField, Min(0.01f)] private float playbackSpeed = 1f;
    [SerializeField, Min(0f)] private float interval = 0f;
    [SerializeField, Min(0.01f)] private float lifetime = 2f;

    public GameEffectKey Key => key;
    public GameEffectType Type => type;
    public GameObject Prefab => prefab;
    public Vector3 Offset => offset;
    public Vector3 Scale => scale;
    public bool FollowTarget => followTarget;
    public bool InheritRotation => inheritRotation;
    public float PlaybackSpeed => playbackSpeed;
    public float Interval => interval;
    public float Lifetime => lifetime;
}