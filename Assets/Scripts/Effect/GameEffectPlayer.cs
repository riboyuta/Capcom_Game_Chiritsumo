using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class GameEffectPlayer : MonoBehaviour
{
    [Header("設定")]
    [SerializeField] private GameEffectConfig config;

    [Header("生成先")]
    [SerializeField] private Transform effectRoot;

    private readonly Dictionary<string, float> lastPlayTimes = new();
    private readonly Dictionary<string, GameEffectHandle> activeLoops = new();
    private readonly Dictionary<string, ActiveEffectState> activeStates = new();
    private readonly List<FollowEffect> followEffects = new();
    private readonly List<string> removeStateKeys = new();

    public GameObject Play(GameEffectKey key, Transform origin)
    {
        if (origin == null)
        {
            return null;
        }

        return PlayInternal(key, origin, origin.position, origin.rotation);
    }

    public GameObject PlayAt(GameEffectKey key, Vector3 position, Quaternion rotation)
    {
        if (!TryGetEntry(key, out var entry))
        {
            return null;
        }

        if (entry.Type == GameEffectType.Loop)
        {
            Debug.LogWarning($"{nameof(GameEffectPlayer)}: LoopエフェクトはPlayAtでは再生できません。key={key}", this);
            return null;
        }

        return PlayByEntry(key, entry, null, position, rotation);
    }

    public void SetActive(GameEffectKey key, Transform origin, bool active)
    {
        if (origin == null)
        {
            return;
        }

        if (active)
        {
            StartActive(key, origin);
        }
        else
        {
            Stop(key, origin);
        }
    }

    public void Stop(GameEffectKey key, Transform origin)
    {
        if (origin == null)
        {
            return;
        }

        var cacheKey = CreateCacheKey(key, origin);

        if (activeLoops.TryGetValue(cacheKey, out var handle))
        {
            handle?.Stop();
            activeLoops.Remove(cacheKey);
        }

        activeStates.Remove(cacheKey);
    }

    public void StopAllActiveEffects()
    {
        foreach (var pair in activeLoops)
        {
            pair.Value?.Stop();
        }

        activeLoops.Clear();
        activeStates.Clear();
    }

    private void OnDisable()
    {
        StopAllActiveEffects();
    }

    private void OnDestroy()
    {
        StopAllActiveEffects();
    }

    private void LateUpdate()
    {
        UpdateFollowEffects();
        UpdateActiveStates();
    }

    private GameObject PlayInternal(
        GameEffectKey key,
        Transform origin,
        Vector3 position,
        Quaternion rotation)
    {
        if (!TryGetEntry(key, out var entry))
        {
            return null;
        }

        return PlayByEntry(key, entry, origin, position, rotation);
    }

    private GameObject PlayByEntry(
        GameEffectKey key,
        GameEffectEntry entry,
        Transform origin,
        Vector3 position,
        Quaternion rotation)
    {
        switch (entry.Type)
        {
            case GameEffectType.OneShot:
                return SpawnAndDestroy(entry, origin, position, rotation);

            case GameEffectType.Interval:
                return PlayIntervalByEntry(key, entry, origin, position, rotation);

            case GameEffectType.Loop:
                if (origin == null)
                {
                    Debug.LogWarning($"{nameof(GameEffectPlayer)}: Loopエフェクトにはoriginが必要です。key={key}", this);
                    return null;
                }

                return StartLoopByEntry(key, entry, origin)?.Instance;

            default:
                return null;
        }
    }

    private void StartActive(GameEffectKey key, Transform origin)
    {
        if (!TryGetEntry(key, out var entry))
        {
            return;
        }

        var cacheKey = CreateCacheKey(key, origin);

        switch (entry.Type)
        {
            case GameEffectType.OneShot:
                if (activeStates.ContainsKey(cacheKey))
                {
                    return;
                }

                activeStates[cacheKey] = new ActiveEffectState(key, origin);
                SpawnAndDestroy(entry, origin, origin.position, origin.rotation);
                break;

            case GameEffectType.Interval:
                activeStates[cacheKey] = new ActiveEffectState(key, origin);
                PlayIntervalByEntry(key, entry, origin, origin.position, origin.rotation);
                break;

            case GameEffectType.Loop:
                activeStates[cacheKey] = new ActiveEffectState(key, origin);
                StartLoopByEntry(key, entry, origin);
                break;
        }
    }

    private GameObject SpawnAndDestroy(
        GameEffectEntry entry,
        Transform origin,
        Vector3 position,
        Quaternion rotation)
    {
        var instance = Spawn(entry, origin, position, rotation);

        if (instance == null)
        {
            return null;
        }

        StartCoroutine(DestroyAfterFinished(instance, entry.Lifetime));
        return instance;
    }

    private GameObject PlayIntervalByEntry(
        GameEffectKey key,
        GameEffectEntry entry,
        Transform origin,
        Vector3 position,
        Quaternion rotation)
    {
        if (!CanPlayInterval(key, origin, entry.Interval))
        {
            return null;
        }

        return SpawnAndDestroy(entry, origin, position, rotation);
    }

    private GameEffectHandle StartLoopByEntry(
        GameEffectKey key,
        GameEffectEntry entry,
        Transform origin)
    {
        var cacheKey = CreateCacheKey(key, origin);

        if (activeLoops.TryGetValue(cacheKey, out var currentHandle))
        {
            if (currentHandle != null && currentHandle.IsValid)
            {
                return currentHandle;
            }

            activeLoops.Remove(cacheKey);
        }

        var instance = Spawn(entry, origin, origin.position, origin.rotation);

        if (instance == null)
        {
            return null;
        }

        var handle = new GameEffectHandle(instance);
        activeLoops[cacheKey] = handle;

        return handle;
    }

    private void UpdateActiveStates()
    {
        removeStateKeys.Clear();

        foreach (var pair in activeStates)
        {
            var state = pair.Value;

            if (!state.IsValid)
            {
                removeStateKeys.Add(pair.Key);
                continue;
            }

            if (!config.TryGet(state.Key, out var entry))
            {
                continue;
            }

            if (entry.Type != GameEffectType.Interval)
            {
                continue;
            }

            PlayIntervalByEntry(
                state.Key,
                entry,
                state.Origin,
                state.Origin.position,
                state.Origin.rotation);
        }

        foreach (var key in removeStateKeys)
        {
            activeStates.Remove(key);
        }
    }

    private void UpdateFollowEffects()
    {
        for (var i = followEffects.Count - 1; i >= 0; i--)
        {
            var effect = followEffects[i];

            if (!effect.IsValid)
            {
                followEffects.RemoveAt(i);
                continue;
            }

            effect.Update();
        }
    }

    private bool TryGetEntry(GameEffectKey key, out GameEffectEntry entry)
    {
        entry = null;

        if (config == null)
        {
            Debug.LogWarning($"{nameof(GameEffectPlayer)}: EffectConfigが設定されていません。", this);
            return false;
        }

        if (!config.TryGet(key, out entry))
        {
            Debug.LogWarning($"{nameof(GameEffectPlayer)}: {key} の設定が見つかりません。", this);
            return false;
        }

        if (entry.Prefab == null)
        {
            Debug.LogWarning($"{nameof(GameEffectPlayer)}: {key} のPrefabが設定されていません。", this);
            return false;
        }

        return true;
    }

    private GameObject Spawn(
        GameEffectEntry entry,
        Transform origin,
        Vector3 basePosition,
        Quaternion baseRotation)
    {
        var rotation = entry.InheritRotation ? baseRotation : Quaternion.identity;
        var position = basePosition + rotation * entry.Offset;
        var parent = effectRoot != null ? effectRoot : null;

        var instance = Instantiate(entry.Prefab, position, rotation, parent);
        instance.transform.localScale = entry.Scale;

        ApplyPlaybackSpeed(instance, entry.PlaybackSpeed);

        if (entry.FollowTarget && origin != null)
        {
            followEffects.Add(new FollowEffect(
                instance.transform,
                origin,
                entry.Offset,
                entry.InheritRotation));
        }

        return instance;
    }

    private bool CanPlayInterval(GameEffectKey key, Transform origin, float interval)
    {
        if (interval <= 0f)
        {
            return true;
        }

        var cacheKey = CreateCacheKey(key, origin);

        if (lastPlayTimes.TryGetValue(cacheKey, out var lastTime))
        {
            if (Time.time - lastTime < interval)
            {
                return false;
            }
        }

        lastPlayTimes[cacheKey] = Time.time;
        return true;
    }

    private string CreateCacheKey(GameEffectKey key, Transform origin)
    {
        var ownerId = origin != null ? origin.GetInstanceID() : 0;
        return $"{ownerId}_{key}";
    }

    private void ApplyPlaybackSpeed(GameObject instance, float speed)
    {
        var particles = instance.GetComponentsInChildren<ParticleSystem>(true);

        foreach (var particle in particles)
        {
            var main = particle.main;
            main.simulationSpeed = speed;
            particle.Play(true);
        }

        var animators = instance.GetComponentsInChildren<Animator>(true);

        foreach (var animator in animators)
        {
            animator.speed = speed;
        }
    }

    private IEnumerator DestroyAfterFinished(GameObject instance, float fallbackLifetime)
    {
        if (instance == null)
        {
            yield break;
        }

        var lifetime = CalculateLifetime(instance, fallbackLifetime);
        yield return new WaitForSeconds(lifetime);

        if (instance != null)
        {
            Destroy(instance);
        }
    }

    private float CalculateLifetime(GameObject instance, float fallbackLifetime)
    {
        var maxLifetime = fallbackLifetime;
        var particles = instance.GetComponentsInChildren<ParticleSystem>(true);

        foreach (var particle in particles)
        {
            var main = particle.main;

            if (main.loop)
            {
                continue;
            }

            var duration = main.duration + main.startLifetime.constantMax;

            if (main.simulationSpeed > 0f)
            {
                duration /= main.simulationSpeed;
            }

            maxLifetime = Mathf.Max(maxLifetime, duration);
        }

        return Mathf.Max(0.01f, maxLifetime);
    }

    private sealed class ActiveEffectState
    {
        public GameEffectKey Key { get; }
        public Transform Origin { get; }

        public bool IsValid => Origin != null;

        public ActiveEffectState(GameEffectKey key, Transform origin)
        {
            Key = key;
            Origin = origin;
        }
    }

    private sealed class FollowEffect
    {
        private readonly Transform effect;
        private readonly Transform target;
        private readonly Vector3 offset;
        private readonly bool inheritRotation;

        public bool IsValid => effect != null && target != null;

        public FollowEffect(
            Transform effect,
            Transform target,
            Vector3 offset,
            bool inheritRotation)
        {
            this.effect = effect;
            this.target = target;
            this.offset = offset;
            this.inheritRotation = inheritRotation;
        }

        public void Update()
        {
            var rotation = inheritRotation ? target.rotation : Quaternion.identity;

            effect.position = target.position + rotation * offset;

            if (inheritRotation)
            {
                effect.rotation = target.rotation;
            }
        }
    }
}

public sealed class GameEffectHandle
{
    public GameObject Instance { get; private set; }

    public bool IsValid => Instance != null;

    public GameEffectHandle(GameObject instance)
    {
        Instance = instance;
    }

    public void Stop()
    {
        if (Instance == null)
        {
            return;
        }

        Object.Destroy(Instance);
        Instance = null;
    }
}