using UnityEngine;

// シーン開始時の音声イベント通知だけを担当する。
// どのBGMを鳴らすかは同じ GameObject の AudioEventBinder 側で設定する。
[DisallowMultipleComponent]
[RequireComponent(typeof(AudioEventBinder))]
public sealed class SceneAudioEmitter : MonoBehaviour
{
    private void Start()
    {
        AudioEvent.Emit(this, "SceneStart");
    }
}
