using UnityEngine;
using UnityEngine.InputSystem;
using Game.Input; 

public class AudioTest : MonoBehaviour
{
    [SerializeField] private RawInputSource _rawInputSource;

    void Start()
    {
        // 念のため自動取得（Inspector で設定し忘れた場合）
        if (_rawInputSource == null)
        {
            _rawInputSource = FindAnyObjectByType<RawInputSource>();
            if (_rawInputSource == null)
            {
                Debug.LogError("[AudioTest] RawInputSource がシーンに見つかりません");
                return;
            }
        }

        // シーン開始時にBGMを再生
        AudioManager.Instance.Play("BGM_title_test");
    }

    void Update()
    {
        if (_rawInputSource == null) return;

        if (_rawInputSource.WasKeyPressedThisFrame(Key.Space))
        {
            // SpaceKeyを押したら効果音再生
            AudioManager.Instance.Play("SFX_title_test");
        }

        if (_rawInputSource.WasKeyPressedThisFrame(Key.Enter))
        {
            // EnterKeyを押したら全音声停止
            AudioManager.Instance.StopAll();
        }
    }
}