using UnityEngine;

/// 再生中サウンド 1 件の管理情報。
internal sealed class AudioHandle
{
    public string       Id;
    public AudioChannel Channel;
    public AudioSource  Source;       // Unity AudioSource コンポーネント
    public float        BaseVolume;   // Play 時に指定された音量
    public bool         Loop;
}
