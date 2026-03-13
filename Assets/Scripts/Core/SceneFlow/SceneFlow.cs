using UnityEngine.SceneManagement;
using UnityEngine;

public static class SceneFlow
{
    private const string TitleSceneName = "Title";
    private const string GameSceneName = "Game";
    private const string ResultSceneName = "Result";

    public static void LoadTitle()
    {
        Debug.Log("[SceneFlow] LoadTitle requested.");
        SceneManager.LoadScene(TitleSceneName, LoadSceneMode.Single);
    }

    public static void LoadGame()
    {
        Debug.Log("[SceneFlow] LoadGame requested.");
        SceneManager.LoadScene(GameSceneName, LoadSceneMode.Single);
    }
    public static void LoadResult()
    {
        Debug.Log("[SceneFlow] LoadResult requested.");
        SceneManager.LoadScene(ResultSceneName, LoadSceneMode.Single);
    }

    public static void ReloadGame()
    {
        Debug.Log("[SceneFlow] ReloadGame requested.");
        SceneManager.LoadScene(GameSceneName, LoadSceneMode.Single);
    }
}