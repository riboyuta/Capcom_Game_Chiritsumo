using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

/// AudioMixer ベースの一元オーディオ管理クラス（シングルトン）。
///
///   - ID ベースの再生・停止・状態問い合わせ
///   - カテゴリ別ボリューム制御 (BGM / SFX / Voice)
///   - プリロード辞書 (AudioDef ScriptableObject)
///
/// Boot シーンに GameObject として配置し、DontDestroyOnLoad で永続化する。

public sealed class AudioManager : MonoBehaviour
{
    // ======================================================================
    //  Singleton
    // ======================================================================

    public static AudioManager Instance { get; private set; }

    // ======================================================================
    //  Inspector
    // ======================================================================

    [Header("AudioMixer")]
    [Tooltip("全体の音量管理に使う AudioMixer アセットです。")]
    [SerializeField] private AudioMixer _mixer;

    [Tooltip("BGM を出力する Mixer Group です。AudioDef の Channel が BGM の音に使います。")]
    [SerializeField] private AudioMixerGroup _bgmGroup;

    [Tooltip("効果音を出力する Mixer Group です。AudioDef の Channel が SFX の音に使います。")]
    [SerializeField] private AudioMixerGroup _sfxGroup;

    [Tooltip("ボイスを出力する Mixer Group です。AudioDef の Channel が Voice の音に使います。")]
    [SerializeField] private AudioMixerGroup _voiceGroup;

    [Header("AudioDef (プリロード)")]
    [Tooltip("起動時に読み込む AudioDef 一覧です。ここに登録された ID を AudioEventBinder の Audio ID から参照します。")]
    [SerializeField] private AudioDef[] _audioDefs;

    [Header("Pool")]
    [Tooltip("SFX / Voice の同時再生に使う AudioSource の初期数です。不足した場合は実行中に追加されます。")]
    [SerializeField] private int _initialPoolSize = 8;

    // Mixer Exposed Parameter 名 (Mixer 側で Expose した名前と一致させる)
    private const string ParamMaster = "MasterVolume";
    private const string ParamBGM    = "BGMVolume";
    private const string ParamSFX    = "SFXVolume";
    private const string ParamVoice  = "VoiceVolume";

    // ======================================================================
    //  Internal State
    // ======================================================================

    // プリロードディクショナリ
    private readonly Dictionary<string, AudioDef.Entry> _registry
        = new Dictionary<string, AudioDef.Entry>();

    // 再生中トラッキング
    private readonly Dictionary<string, AudioHandle> _active
        = new Dictionary<string, AudioHandle>();

    // BGM 専用ソース
    private AudioSource _bgmSourceA;
    private AudioSource _bgmSourceB;
    private AudioSource _currentBgm;

    // SFX / Voice 用オブジェクトプール
    private readonly Queue<AudioSource> _pool = new Queue<AudioSource>();
    private Transform _poolRoot;

    // 重複再生トラッキング（PlayOverlap 用）
    private readonly List<AudioSource> _overlaps = new List<AudioSource>();

    // リニアボリューム保持 (0〜1)
    private float _masterVol = 1f;
    private float _bgmVol    = 1f;
    private float _sfxVol    = 1f;
    private float _voiceVol  = 1f;

    // ======================================================================
    //  Lifecycle
    // ======================================================================

    private void Awake()
    {
        // --- Singleton ---
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        InitBGMSources();
        InitPool();
        LoadRegistry();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    // ======================================================================
    //  Playback API  
    // ======================================================================

    /// 登録済みの ID を既定設定で再生する。
    public void Play(string id)
    {
        if (!TryGetEntry(id, out var entry)) return;
        Play(id, entry.defaultVolume, entry.loop);
    }

    /// 登録済みの ID を指定音量・ループ設定で再生する。
    public void Play(string id, float volume, bool? loop = null)
    {
        if (!TryGetEntry(id, out var entry)) return;

        bool shouldLoop = loop ?? entry.loop;

        switch (entry.channel)
        {
            case AudioChannel.BGM:
                PlayBGM(id, entry, volume, shouldLoop);
                break;

            case AudioChannel.SFX:
            case AudioChannel.Voice:
                PlayPooled(id, entry, volume, shouldLoop);
                break;
        }
    }

    /// 3D 空間の指定位置で再生する（SFX 向け）。
    public void PlayAtPoint(string id, Vector3 position, float? volume = null)
    {
        if (!TryGetEntry(id, out var entry)) return;

        float vol = volume ?? entry.defaultVolume;
        AudioSource src = RentSource();

        src.clip            = entry.clip;
        src.volume          = vol;
        src.loop            = entry.loop;
        src.outputAudioMixerGroup = GetGroup(entry.channel);
        src.spatialBlend     = 1f; // 完全 3D
        src.transform.position = position;
        src.Play();

        var handle = new AudioHandle
        {
            Id         = id,
            Channel    = entry.channel,
            Source     = src,
            BaseVolume = vol,
            Loop       = entry.loop,
        };
        TrackHandle(id, handle);
    }

    /// 同じ SFX を重ねて再生する（重複再生可）。再生完了後に自動回収される。
    public void PlayOverlap(string id, float? volume = null)
    {
        if (!TryGetEntry(id, out var entry)) return;

        float vol = volume ?? entry.defaultVolume;
        AudioSource src = RentSource();

        src.clip            = entry.clip;
        src.volume          = vol;
        src.loop            = false; // 重複再生はループ不可
        src.outputAudioMixerGroup = GetGroup(entry.channel);
        src.spatialBlend     = 0f;
        src.Play();

        _overlaps.Add(src);
    }

    /// 同じ SFX を 3D 空間の指定位置で重ねて再生する（重複再生可）。
    public void PlayOverlapAtPoint(string id, Vector3 position, float? volume = null)
    {
        if (!TryGetEntry(id, out var entry)) return;

        float vol = volume ?? entry.defaultVolume;
        AudioSource src = RentSource();

        src.clip            = entry.clip;
        src.volume          = vol;
        src.loop            = false;
        src.outputAudioMixerGroup = GetGroup(entry.channel);
        src.spatialBlend     = 1f;
        src.transform.position = position;
        src.Play();

        _overlaps.Add(src);
    }

    // ======================================================================
    //  Control API
    // ======================================================================

    /// 指定 ID を停止する。
    public void Stop(string id)
    {
        if (!_active.TryGetValue(id, out var handle)) return;

        handle.Source.Stop();

        // BGM ソースはプールに返さない
        if (handle.Channel != AudioChannel.BGM)
        {
            ReturnSource(handle.Source);
        }

        _active.Remove(id);
    }

    /// 全サウンドを停止する。
    public void StopAll()
    {
        foreach (var handle in _active.Values)
        {
            handle.Source.Stop();
            if (handle.Channel != AudioChannel.BGM)
            {
                ReturnSource(handle.Source);
            }
        }
        _active.Clear();

        // 重複再生も停止
        foreach (var src in _overlaps)
        {
            ReturnSource(src);
        }
        _overlaps.Clear();
    }

    /// 指定チャンネルのサウンドをすべて停止する。
    public void StopAll(AudioChannel channel)
    {
        var toRemove = new List<string>();

        foreach (var kvp in _active)
        {
            if (kvp.Value.Channel == channel)
            {
                kvp.Value.Source.Stop();
                if (kvp.Value.Channel != AudioChannel.BGM)
                {
                    ReturnSource(kvp.Value.Source);
                }
                toRemove.Add(kvp.Key);
            }
        }

        foreach (var key in toRemove)
        {
            _active.Remove(key);
        }
    }

    // ======================================================================
    //  Fade API
    // ======================================================================

    /// 指定 ID のサウンドをフェードインで再生する。
    /// まだ再生されていなければ Play してから音量を徐々に上げる。
    public void FadeIn(string id, float duration = 1f)
    {
        // まだ再生中でなければ音量 0 で再生開始
        if (!IsPlaying(id))
        {
            Play(id, 0f);
        }

        if (!_active.TryGetValue(id, out var handle)) return;

        float targetVol = handle.BaseVolume > 0f ? handle.BaseVolume : 1f;

        // 既登録のエントリがあれば defaultVolume を目標値にする
        if (_registry.TryGetValue(id, out var entry))
        {
            targetVol = entry.defaultVolume;
        }

        StartCoroutine(FadeInCoroutine(handle, targetVol, duration));
    }

    /// 指定 ID のサウンドをフェードアウトで停止する。
    public void FadeOut(string id, float duration = 1f)
    {
        if (!_active.TryGetValue(id, out var handle)) return;
        StartCoroutine(FadeOutCoroutine(id, handle, duration));
    }

    private IEnumerator FadeInCoroutine(AudioHandle handle, float targetVolume, float duration)
    {
        handle.Source.volume = 0f;

        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            handle.Source.volume = Mathf.Lerp(0f, targetVolume, t / duration);
            yield return null;
        }

        handle.Source.volume = targetVolume;
        handle.BaseVolume = targetVolume;
    }

    private IEnumerator FadeOutCoroutine(string id, AudioHandle handle, float duration)
    {
        float startVol = handle.Source.volume;

        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            handle.Source.volume = Mathf.Lerp(startVol, 0f, t / duration);
            yield return null;
        }

        handle.Source.volume = 0f;
        Stop(id);
    }

    // ======================================================================
    //  Query API
    // ======================================================================

    /// 指定 ID が再生中かどうか。
    public bool IsPlaying(string id)
    {
        return _active.TryGetValue(id, out var h) && h.Source.isPlaying;
    }

    // ======================================================================
    //  Volume API  
    // ======================================================================

    /// マスターボリュームを設定 (0〜1)。
    public void SetMasterVolume(float volume)
    {
        _masterVol = Mathf.Clamp01(volume);
        SetMixerVolume(ParamMaster, _masterVol);
    }

    /// チャンネル別ボリュームを設定 (0〜1)。
    public void SetChannelVolume(AudioChannel channel, float volume)
    {
        float v = Mathf.Clamp01(volume);

        switch (channel)
        {
            case AudioChannel.BGM:
                _bgmVol = v;
                SetMixerVolume(ParamBGM, v);
                break;
            case AudioChannel.SFX:
                _sfxVol = v;
                SetMixerVolume(ParamSFX, v);
                break;
            case AudioChannel.Voice:
                _voiceVol = v;
                SetMixerVolume(ParamVoice, v);
                break;
        }
    }

    /// 個別 ID のボリュームを変更する。
    public void SetVolume(string id, float volume)
    {
        if (_active.TryGetValue(id, out var handle))
        {
            handle.BaseVolume = Mathf.Clamp01(volume);
            handle.Source.volume = handle.BaseVolume;
        }
    }

    /// 現在のマスターボリュームを取得 (0〜1)。
    public float GetMasterVolume() => _masterVol;

    /// 現在のチャンネルボリュームを取得 (0〜1)。
    public float GetChannelVolume(AudioChannel channel)
    {
        return channel switch
        {
            AudioChannel.BGM   => _bgmVol,
            AudioChannel.SFX   => _sfxVol,
            AudioChannel.Voice => _voiceVol,
            _ => 1f,
        };
    }

    // ======================================================================
    //  Private — BGM
    // ======================================================================

    private void PlayBGM(string id, AudioDef.Entry entry, float volume, bool loop)
    {
        // 現在の BGM を停止
        if (_currentBgm != null && _currentBgm.isPlaying)
        {
            _currentBgm.Stop();
        }

        // 交代
        _currentBgm = (_currentBgm == _bgmSourceA) ? _bgmSourceB : _bgmSourceA;

        _currentBgm.clip   = entry.clip;
        _currentBgm.volume = volume;
        _currentBgm.loop   = loop;
        _currentBgm.outputAudioMixerGroup = _bgmGroup;
        _currentBgm.spatialBlend = 0f; // 2D
        _currentBgm.Play();

        var handle = new AudioHandle
        {
            Id         = id,
            Channel    = AudioChannel.BGM,
            Source     = _currentBgm,
            BaseVolume = volume,
            Loop       = loop,
        };
        TrackHandle(id, handle);
    }

    // ======================================================================
    //  Private — Pooled (SFX / Voice)
    // ======================================================================

    private void PlayPooled(string id, AudioDef.Entry entry, float volume, bool loop)
    {
        AudioSource src = RentSource();

        src.clip            = entry.clip;
        src.volume          = volume;
        src.loop            = loop;
        src.outputAudioMixerGroup = GetGroup(entry.channel);
        src.spatialBlend     = 0f; // 2D
        src.Play();

        var handle = new AudioHandle
        {
            Id         = id,
            Channel    = entry.channel,
            Source     = src,
            BaseVolume = volume,
            Loop       = loop,
        };
        TrackHandle(id, handle);
    }

    // ======================================================================
    //  Private — Pool Management
    // ======================================================================

    private void InitPool()
    {
        _poolRoot = new GameObject("[AudioPool]").transform;
        _poolRoot.SetParent(transform);

        for (int i = 0; i < _initialPoolSize; i++)
        {
            _pool.Enqueue(CreatePooledSource());
        }
    }

    private AudioSource CreatePooledSource()
    {
        var go  = new GameObject("PooledAudioSource");
        go.transform.SetParent(_poolRoot);
        var src = go.AddComponent<AudioSource>();
        src.playOnAwake = false;
        go.SetActive(false);
        return src;
    }

    private AudioSource RentSource()
    {
        AudioSource src = (_pool.Count > 0) ? _pool.Dequeue() : CreatePooledSource();
        src.gameObject.SetActive(true);
        // リセット
        src.clip         = null;
        src.volume       = 1f;
        src.loop         = false;
        src.spatialBlend = 0f;
        src.transform.localPosition = Vector3.zero;
        return src;
    }

    private void ReturnSource(AudioSource src)
    {
        src.Stop();
        src.clip = null;
        src.gameObject.SetActive(false);
        _pool.Enqueue(src);
    }

    // ======================================================================
    //  Private — BGM Sources
    // ======================================================================

    private void InitBGMSources()
    {
        _bgmSourceA = CreateBGMSource("BGM_A");
        _bgmSourceB = CreateBGMSource("BGM_B");
        _currentBgm = _bgmSourceA;
    }

    private AudioSource CreateBGMSource(string name)
    {
        var go  = new GameObject(name);
        go.transform.SetParent(transform);
        var src = go.AddComponent<AudioSource>();
        src.playOnAwake  = false;
        src.spatialBlend = 0f;
        src.outputAudioMixerGroup = _bgmGroup;
        return src;
    }

    // ======================================================================
    //  Private — Registry
    // ======================================================================

    private void LoadRegistry()
    {
        if (_audioDefs == null) return;

        foreach (var def in _audioDefs)
        {
            if (def == null || def.entries == null) continue;

            foreach (var entry in def.entries)
            {
                if (string.IsNullOrEmpty(entry.id))
                {
                    Debug.LogWarning("[AudioManager] AudioDef entry with empty ID — skipped.");
                    continue;
                }

                if (_registry.ContainsKey(entry.id))
                {
                    Debug.LogWarning($"[AudioManager] Duplicate audio ID \"{entry.id}\" — overwritten.");
                }

                _registry[entry.id] = entry;
            }
        }

        Debug.Log($"[AudioManager] Loaded {_registry.Count} audio entries.");
    }

    private bool TryGetEntry(string id, out AudioDef.Entry entry)
    {
        if (_registry.TryGetValue(id, out entry))
        {
            return true;
        }

        Debug.LogWarning($"[AudioManager] Audio ID \"{id}\" not found in registry.");
        entry = null;
        return false;
    }

    // ======================================================================
    //  Private — Helpers
    // ======================================================================

    private AudioMixerGroup GetGroup(AudioChannel channel)
    {
        return channel switch
        {
            AudioChannel.BGM   => _bgmGroup,
            AudioChannel.SFX   => _sfxGroup,
            AudioChannel.Voice => _voiceGroup,
            _ => null,
        };
    }

    /// ハンドルを追跡ディクショナリに登録する。同一 ID が再生中なら先に停止する。
    private void TrackHandle(string id, AudioHandle handle)
    {
        // 同一 ID の既存ハンドルを停止
        if (_active.TryGetValue(id, out var old))
        {
            old.Source.Stop();
            if (old.Channel != AudioChannel.BGM)
            {
                ReturnSource(old.Source);
            }
        }

        _active[id] = handle;
    }

    /// リニアボリューム (0〜1) を AudioMixer の dB に変換して設定する。
    private void SetMixerVolume(string paramName, float linearVolume)
    {
        if (_mixer == null) return;

        // 0 の場合は -80dB (ミュート)、それ以外は対数変換
        float dB = (linearVolume > 0f)
            ? Mathf.Log10(linearVolume) * 20f
            : -80f;

        _mixer.SetFloat(paramName, dB);
    }

    // ======================================================================
    //  Cleanup — 再生完了した非ループ音を自動回収
    // ======================================================================

    private readonly List<string> _finishedKeys = new List<string>();

    private void Update()
    {
        // --- _active の回収 ---
        _finishedKeys.Clear();

        foreach (var kvp in _active)
        {
            var h = kvp.Value;

            // ループ音はスキップ
            if (h.Loop) continue;

            // 再生が終了している場合
            if (!h.Source.isPlaying)
            {
                _finishedKeys.Add(kvp.Key);
            }
        }

        foreach (var key in _finishedKeys)
        {
            if (_active.TryGetValue(key, out var h))
            {
                if (h.Channel != AudioChannel.BGM)
                {
                    ReturnSource(h.Source);
                }
                _active.Remove(key);
            }
        }

        // --- 重複再生 (PlayOverlap) の回収 ---
        for (int i = _overlaps.Count - 1; i >= 0; i--)
        {
            if (!_overlaps[i].isPlaying)
            {
                ReturnSource(_overlaps[i]);
                _overlaps.RemoveAt(i);
            }
        }
    }

    // ======================================================================
    //  AudioHandle — 再生中サウンド 1 件の管理情報
    // ======================================================================

    private sealed class AudioHandle
    {
        public string       Id;
        public AudioChannel Channel;
        public AudioSource  Source;
        public float        BaseVolume;
        public bool         Loop;
    }
}
