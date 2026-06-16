using UnityEngine;

// シーン開始時・終了時の音声イベント通知を担当する。
// どのBGMを鳴らすか・フェードアウトするかは同じ GameObject の AudioEventBinder 側で設定する。
[DisallowMultipleComponent]
[RequireComponent(typeof(AudioEventBinder))]
public sealed class SceneAudioEmitter : MonoBehaviour
{
    private void Start()
    {
        AudioEvent.Emit(this, "SceneStart");
    }

    // シーンアンロード時にフェードアウト等を実行するためのイベント通知。
    // AudioManager は DontDestroyOnLoad で生存するため、フェードアウトコルーチンは完走する。
    private void OnDestroy()
    {
        AudioEvent.Emit(this, "SceneEnd");
    }
}
