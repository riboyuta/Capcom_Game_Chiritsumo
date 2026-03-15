using UnityEngine;

/// 音声アセットの事前登録定義 (ScriptableObject)。
[CreateAssetMenu(fileName = "NewAudioDef", menuName = "Audio/AudioDef")]
public sealed class AudioDef : ScriptableObject
{
    [System.Serializable]
    public class Entry
    {
        [Tooltip("再生ID。スクリプトからこの文字列で再生する")]
        public string id;

        [Tooltip("音声クリップ")]
        public AudioClip clip;

        [Tooltip("BGM / SFX / Voice")]
        public AudioChannel channel;

        [Tooltip("既定の音量 (0〜1)")]
        [Range(0f, 1f)]
        public float defaultVolume = 1f;

        [Tooltip("ループ再生するか")]
        public bool loop;
    }

    [Tooltip("登録する音声エントリの一覧")]
    public Entry[] entries;
}
