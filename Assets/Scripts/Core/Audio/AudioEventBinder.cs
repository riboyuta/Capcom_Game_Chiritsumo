using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class AudioEventBinder : MonoBehaviour
{
    public enum PlayMode
    {
        Play,
        PlayOverlap,
        PlayAtPoint,
        PlayOverlapAtPoint,
        Stop,
        FadeIn,
        FadeOut
    }

    [Serializable]
    public sealed class AudioEventAction
    {
        [Tooltip("この音だけ一時的に鳴らさない場合はオフにします。設定内容は残ります。")]
        public bool enabled = true;

        [Tooltip("AudioDef に登録されている再生 ID です。ここを差し替えると、コードを触らずに鳴る音を変更できます。")]
        public string audioId = "";

        [Tooltip("この音の再生方法です。単発の効果音は基本的に PlayOverlap を使います。")]
        public PlayMode playMode = PlayMode.PlayOverlap;

        [Tooltip("このアクションだけ AudioDef の既定音量と違う音量にしたい場合はオンにします。通常はオフで問題ありません。")]
        public bool overrideVolume;

        [Tooltip("Override Volume がオンの時だけ使う音量です。0 で無音、1 で最大です。")]
        [Range(0f, 1f)]
        public float volume = 1f;

        [Tooltip("イベントを受け取ってから、この音を鳴らすまでの待ち時間です。0 なら即再生します。")]
        [Min(0f)]
        public float delay;

        [Tooltip("この音を再び鳴らせるようになるまでの間隔です。0 ならイベントのたびに毎回鳴ります。")]
        [Min(0f)]
        public float cooldown;

        [Tooltip("Play Mode が FadeIn / FadeOut の時に使うフェード時間です。")]
        [Min(0f)]
        public float fadeDuration = 1f;

        [Tooltip("3D 再生時の音の発生位置です。未設定なら Default Spatial Origin、それも未設定ならこの GameObject の位置を使います。")]
        public Transform spatialOrigin;

        [NonSerialized] public float nextAllowedTime;
    }

    [Serializable]
    public sealed class AudioEventBinding
    {
        [Tooltip("スクリプトから AudioEvent.Emit(this, \"...\") で通知されるイベント名です。AudioDef の再生 ID ではありません。例: Jump, Land, Break。")]
        public string eventName = "";

        [Tooltip("このイベントを受け取った時に実行する音の設定です。1つのイベントに複数の音を割り当てることもできます。")]
        public AudioEventAction[] actions = Array.Empty<AudioEventAction>();
    }

    [Header("Default Settings")]
    [Tooltip("3D 再生時の基本位置です。各 Action の Spatial Origin が未設定の場合に使います。未設定ならこの GameObject の位置になります。")]
    [SerializeField] private Transform defaultSpatialOrigin;

    [Tooltip("Bindings に存在しない Event Name が通知された時に警告を出します。設定漏れを見つけたい間はオン推奨です。")]
    [SerializeField] private bool warnMissingEvent = true;

    [Tooltip("AudioManager がシーン上に見つからない時に警告を出します。通常はオン推奨です。")]
    [SerializeField] private bool warnMissingAudioManager = true;

    [Header("Bindings")]
    [Tooltip("イベント名と実際に鳴らす音の対応表です。音担当者は基本的にここで Audio ID や再生方法を設定します。")]
    [SerializeField] private AudioEventBinding[] bindings = Array.Empty<AudioEventBinding>();

    private readonly Dictionary<string, List<AudioEventAction>> actionLookup =
        new Dictionary<string, List<AudioEventAction>>(StringComparer.Ordinal);

    private void Awake()
    {
        RebuildLookup();
    }

    private void OnValidate()
    {
        if (bindings == null)
        {
            bindings = Array.Empty<AudioEventBinding>();
        }
    }

    public void Emit(string eventName)
    {
        EmitInternal(eventName, null);
    }

    public void EmitAt(string eventName, Vector3 position)
    {
        EmitInternal(eventName, position);
    }

    public bool HasEvent(string eventName)
    {
        if (actionLookup.Count == 0 && bindings != null && bindings.Length > 0)
        {
            RebuildLookup();
        }

        return !string.IsNullOrEmpty(eventName) && actionLookup.ContainsKey(eventName);
    }

    public void RebuildLookup()
    {
        actionLookup.Clear();

        if (bindings == null)
        {
            return;
        }

        for (int i = 0; i < bindings.Length; i++)
        {
            AudioEventBinding binding = bindings[i];
            if (binding == null || string.IsNullOrWhiteSpace(binding.eventName))
            {
                continue;
            }

            if (!actionLookup.TryGetValue(binding.eventName, out List<AudioEventAction> actions))
            {
                actions = new List<AudioEventAction>();
                actionLookup.Add(binding.eventName, actions);
            }

            if (binding.actions == null)
            {
                continue;
            }

            for (int j = 0; j < binding.actions.Length; j++)
            {
                AudioEventAction action = binding.actions[j];
                if (action != null)
                {
                    actions.Add(action);
                }
            }
        }
    }

    private void EmitInternal(string eventName, Vector3? explicitPosition)
    {
        if (!isActiveAndEnabled || string.IsNullOrWhiteSpace(eventName))
        {
            return;
        }

        if (actionLookup.Count == 0 && bindings != null && bindings.Length > 0)
        {
            RebuildLookup();
        }

        if (!actionLookup.TryGetValue(eventName, out List<AudioEventAction> actions))
        {
            if (warnMissingEvent)
            {
                Debug.LogWarning($"[AudioEventBinder] Event \"{eventName}\" is not bound.", this);
            }

            return;
        }

        for (int i = 0; i < actions.Count; i++)
        {
            TryExecuteAction(actions[i], explicitPosition);
        }
    }

    private void TryExecuteAction(AudioEventAction action, Vector3? explicitPosition)
    {
        if (action == null || !action.enabled || string.IsNullOrWhiteSpace(action.audioId))
        {
            return;
        }

        if (action.cooldown > 0f && Time.time < action.nextAllowedTime)
        {
            return;
        }

        if (action.cooldown > 0f)
        {
            action.nextAllowedTime = Time.time + action.cooldown;
        }

        if (action.delay > 0f)
        {
            StartCoroutine(ExecuteDelayed(action, explicitPosition));
            return;
        }

        ExecuteNow(action, explicitPosition);
    }

    private IEnumerator ExecuteDelayed(AudioEventAction action, Vector3? explicitPosition)
    {
        yield return new WaitForSeconds(action.delay);
        ExecuteNow(action, explicitPosition);
    }

    private void ExecuteNow(AudioEventAction action, Vector3? explicitPosition)
    {
        AudioManager audioManager = AudioManager.Instance;
        if (audioManager == null)
        {
            if (warnMissingAudioManager)
            {
                Debug.LogWarning("[AudioEventBinder] AudioManager.Instance is missing.", this);
            }

            return;
        }

        string audioId = action.audioId;
        float? volume = action.overrideVolume ? action.volume : null;
        Vector3 position = explicitPosition ?? ResolvePosition(action);

        switch (action.playMode)
        {
            case PlayMode.Play:
                if (volume.HasValue)
                {
                    audioManager.Play(audioId, volume.Value);
                }
                else
                {
                    audioManager.Play(audioId);
                }
                break;

            case PlayMode.PlayOverlap:
                audioManager.PlayOverlap(audioId, volume);
                break;

            case PlayMode.PlayAtPoint:
                audioManager.PlayAtPoint(audioId, position, volume);
                break;

            case PlayMode.PlayOverlapAtPoint:
                audioManager.PlayOverlapAtPoint(audioId, position, volume);
                break;

            case PlayMode.Stop:
                audioManager.Stop(audioId);
                break;

            case PlayMode.FadeIn:
                audioManager.FadeIn(audioId, action.fadeDuration);
                break;

            case PlayMode.FadeOut:
                audioManager.FadeOut(audioId, action.fadeDuration);
                break;
        }
    }

    private Vector3 ResolvePosition(AudioEventAction action)
    {
        if (action.spatialOrigin != null)
        {
            return action.spatialOrigin.position;
        }

        if (defaultSpatialOrigin != null)
        {
            return defaultSpatialOrigin.position;
        }

        return transform.position;
    }
}
