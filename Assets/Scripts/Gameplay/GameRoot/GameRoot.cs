using UnityEngine;

public sealed class GameRoot : MonoBehaviour
{
    private enum State
    {
        Ready,
        Playing,
        Result
    }

    [Header("進行フロー: Ready時間")]
    [Tooltip("Ready 状態を維持する時間(秒)です。開始演出や待機時間の長さを調整します。")]
    [SerializeField, Min(0f)] private float readyDuration = 1.0f;

    [Header("進行フロー: Play時間")]
    [Tooltip("Play 状態を維持する時間(秒)です。実際にプレイ可能な制限時間を調整します。")]
    [SerializeField, Min(0f)] private float playDuration = 10.0f;

    [Header("Result遷移")]
    [Tooltip("Result 遷移時のフェードアウト時間(秒)です。")]
    [SerializeField, Min(0f)] private float resultFadeOutDuration = 0.5f;

    private State currentState;
    private float playTimer;
    private float readyTimer;
    private float elapsedTime;
    private bool isTransitioning;
    private bool goalClearAccepted;

    private void Start()
    {
        // シーン開始時に明転させる。
        if (FadeController.Instance != null)
        {
            FadeController.Instance.FadeIn(0.5f);
        }

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.FadeIn("BGM_main_beforechase", 2.0f);
        }

        elapsedTime = 0f;
        goalClearAccepted = false;
        EnterReady();
    }

    private void Update()
    {
        if (isTransitioning)
        {
            return;
        }

        elapsedTime += Time.deltaTime;

        switch (currentState)
        {
            case State.Ready:
                UpdateReady();
                break;

            case State.Playing:
                UpdatePlaying();
                break;

            case State.Result:
                UpdateResult();
                break;
        }
    }

    private void EnterReady()
    {
        currentState = State.Ready;
        playTimer = playDuration;
        readyTimer = readyDuration;
    }

    private void UpdateReady()
    {
        readyTimer -= Time.deltaTime;

        if (readyTimer > 0f)
        {
            return;
        }

        EnterPlaying();
    }

    private void EnterPlaying()
    {
        currentState = State.Playing;
    }

    private void UpdatePlaying()
    {
        // playTimer はデバッグ表示/UI向けの残り時間であり、Result遷移条件には使わない。
        playTimer -= Time.deltaTime;
        if (playTimer < 0f)
        {
            playTimer = 0f;
        }

        playTimer = 0f;
        //EnterResult();
    }

    private void EnterResult()
    {
        currentState = State.Result;
    }

    private void UpdateResult()
    {
        isTransitioning = true;

        if (FadeController.Instance != null)
        {
            FadeController.Instance.FadeOut(resultFadeOutDuration, SceneFlow.LoadResult);
            return;
        }

        Debug.LogWarning("[GameRoot] FadeController not found. Transitioning without fade.");
        SceneFlow.LoadResult();
    }

    /// <summary>
    /// ゴール到達を受け付け、Result遷移を開始する。
    /// Playing 中のみ受理する。
    /// </summary>
    public bool RequestGoalClear()
    {
        if (isTransitioning || goalClearAccepted)
        {
            return false;
        }

        if (currentState != State.Playing)
        {
            Debug.Log($"[GameRoot] GoalClear ignored. currentState={currentState}");
            return false;
        }

        goalClearAccepted = true;
        ResultSceneTransitData.SetClearElapsedTime(elapsedTime);
        Debug.Log($"[GameRoot] GoalClear accepted. elapsedTime={elapsedTime:F2}s");

        EnterResult();
        return true;
    }

    public string GetCurrentStateName() { return currentState.ToString(); }
    public float GetRemainingPlayTime() { return playTimer; }
    public float GetElapsedTime() { return elapsedTime; }
}