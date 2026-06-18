using UnityEngine;

/// オーディオのカテゴリ分類。AudioMixer の Group と 1:1 対応する。
public enum AudioChannel
{
    BGM,
    SFX,
    Voice
}

/// 音声アセットの事前登録定義 (ScriptableObject)。
[CreateAssetMenu(fileName = "NewAudioDef", menuName = "Audio/AudioDef")]
public sealed class AudioDef : ScriptableObject
{
    [System.Serializable]
    public class Entry
    {
        [Tooltip("AudioManager が再生に使う ID です。AudioEventBinder の Action > Audio ID には、この値を指定します。")]
        public string id;

        [Tooltip("実際に再生する音声クリップです。")]
        public AudioClip clip;

        [Tooltip("この音を流すカテゴリです。Mixer Group と音量設定の分類に使います。")]
        public AudioChannel channel;

        [Tooltip("この音の基本音量です。AudioEventBinder 側で Override Volume をオンにすると個別に上書きできます。")]
        [Range(0f, 1f)]
        public float defaultVolume = 1f;

        [Tooltip("再生時にループするかどうかです。BGM や継続音で使います。")]
        public bool loop;
    }

    [Tooltip("プロジェクトで使う音声 ID と AudioClip の登録一覧です。AudioManager 起動時に読み込まれます。")]
    public Entry[] entries;
}
