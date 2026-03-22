using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class TitleSceneController : MonoBehaviour
{

    //入力連打で二重遷移防止

    private bool isTransitioning;

    private void Start()
    {
        Debug.Log("[TitleSceneController] Title scene started.");

        // シーン開始時に明転させる。
        if (FadeController.Instance != null)
        {
            FadeController.Instance.FadeIn(0.5f);
        }

        AudioManager.Instance.FadeIn("BGM_title", 1.0f);
    }

    private void Update()
    {
        if (isTransitioning)
        {
            return;
        }

        if (BootSceneController.Instance.DebugInput.NextScenePressed)
        {
            StartGame();
        }

    }

    public void StartGame()
    {
        if (isTransitioning)
        {
            return;
        }

        isTransitioning = true;
        StartCoroutine(StartGameCoroutine());
    }

    private IEnumerator StartGameCoroutine()
    {
        AudioManager.Instance.PlayOverlap("SFX_button_enter");
        AudioManager.Instance.FadeOut("BGM_title",1.0f);

        // フェードアウト（暗転）しながらSEの余韻を待つ。
        if (FadeController.Instance != null)
        {
            FadeController.Instance.FadeOut(0.5f);
        }

        yield return new WaitForSeconds(0.5f);

        SceneFlow.LoadGame();
    }

    public void ExitGame()
    {
        if (isTransitioning) return;
        isTransitioning = true;
        
        Debug.Log("[TitleSceneController] ExitGame requested.");
        StartCoroutine(ExitGameCoroutine());
    }

    private IEnumerator ExitGameCoroutine()
    {
        AudioManager.Instance.PlayOverlap("SFX_button_back");

        // Wait a bit for the sound
        yield return new WaitForSeconds(0.4f);

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}