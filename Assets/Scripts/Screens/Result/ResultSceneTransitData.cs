using UnityEngine;

/// Result シーンへ渡す一時データ。
/// 値は読み取り時に消費され、次回遷移へ持ち越さない。
public static class ResultSceneTransitData
{
    private static float? clearElapsedTime;
    private static int? deathCount;

    public static void SetClearResult(float elapsedSeconds, int deathCount)
    {
        clearElapsedTime = Mathf.Max(0f, elapsedSeconds);
        ResultSceneTransitData.deathCount = Mathf.Max(0, deathCount);
    }

    public static void SetClearElapsedTime(float elapsedSeconds)
    {
        SetClearResult(elapsedSeconds, 0);
    }

    public static bool TryConsumeClearResult(out float elapsedSeconds, out int deathCount)
    {
        if (!clearElapsedTime.HasValue || !ResultSceneTransitData.deathCount.HasValue)
        {
            elapsedSeconds = 0f;
            deathCount = 0;
            Clear();
            return false;
        }

        elapsedSeconds = clearElapsedTime.Value;
        deathCount = ResultSceneTransitData.deathCount.Value;
        Clear();
        return true;
    }

    public static bool TryConsumeClearElapsedTime(out float elapsedSeconds)
    {
        return TryConsumeClearResult(out elapsedSeconds, out _);
    }

    public static void Clear()
    {
        clearElapsedTime = null;
        deathCount = null;
    }
}