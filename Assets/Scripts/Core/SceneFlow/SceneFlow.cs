using UnityEngine;
using UnityEngine.SceneManagement;

public static class SceneFlow
{
    private const string BootSceneName = "Boot";
    private const string TitleSceneName = "Title";
    private const string GameSceneName = "Stage1";
    private const string ResultSceneName = "Result";

    public static void LoadBoot()
    {
        Debug.Log("[SceneFlow] LoadBoot requested.");
        SceneManager.LoadScene(BootSceneName, LoadSceneMode.Single);
    }

    public static void LoadTitle()
    {
        Debug.Log("[SceneFlow] LoadTitle requested.");
        SceneManager.LoadScene(TitleSceneName, LoadSceneMode.Single);
    }

    public static void LoadGame()
    {
        Debug.Log("[SceneFlow] LoadGame requested.");
        SceneManager.LoadScene(GameSceneName, LoadSceneMode.Single);

        // Title からの遷移では直前に FadeOut 済みなので、Stage1 読み込み後に画面を明転させる。
        if (FadeController.Instance != null)
        {
            FadeController.Instance.FadeIn();
        }
    }

    public static void LoadResult()
    {
        Debug.Log("[SceneFlow] LoadResult requested.");
        SceneManager.LoadScene(ResultSceneName, LoadSceneMode.Single);
    }

    public static void ReloadCurrent()
    {
        string currentSceneName = SceneManager.GetActiveScene().name;
        Debug.Log($"[SceneFlow] ReloadCurrent requested. scene={currentSceneName}");
        SceneManager.LoadScene(currentSceneName, LoadSceneMode.Single);
    }

    public static void LoadDebugNextScene()
    {
        string currentSceneName = SceneManager.GetActiveScene().name;
        Debug.Log($"[SceneFlow] LoadDebugNextScene requested. current={currentSceneName}");

        switch (currentSceneName)
        {
            case TitleSceneName:
                LoadGame();
                break;

            case GameSceneName:
                LoadResult();
                break;

            case ResultSceneName:
                LoadTitle();
                break;

            default:
                Debug.LogWarning($"[SceneFlow] Unknown scene for debug next: {currentSceneName}");
                break;
        }
    }

    public static void LoadDebugPreviousScene()
    {
        string currentSceneName = SceneManager.GetActiveScene().name;
        Debug.Log($"[SceneFlow] LoadDebugPreviousScene requested. current={currentSceneName}");

        switch (currentSceneName)
        {
            case TitleSceneName:
                LoadResult();
                break;

            case GameSceneName:
                LoadTitle();
                break;

            case ResultSceneName:
                LoadGame();
                break;

            default:
                Debug.LogWarning($"[SceneFlow] Unknown scene for debug previous: {currentSceneName}");
                break;
        }
    }
}
