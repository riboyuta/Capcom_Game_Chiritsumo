using UnityEngine;

/// <summary>
/// Result シーンへ渡す一時データ。
/// 値は読み取り時に消費され、次回遷移へ持ち越さない。
/// </summary>
public static class ResultSceneTransitData
{
    private static float? clearElapsedTime;

    public static void SetClearElapsedTime(float elapsedSeconds)
    {
        clearElapsedTime = Mathf.Max(0f, elapsedSeconds);
    }

    public static bool TryConsumeClearElapsedTime(out float elapsedSeconds)
    {
        if (!clearElapsedTime.HasValue)
        {
            elapsedSeconds = 0f;
            return false;
        }

        elapsedSeconds = clearElapsedTime.Value;
        clearElapsedTime = null;
        return true;
    }

    public static void Clear()
    {
        clearElapsedTime = null;
    }
}