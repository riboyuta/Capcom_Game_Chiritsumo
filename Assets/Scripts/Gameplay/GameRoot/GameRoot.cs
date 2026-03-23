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

    private State currentState;
    private float playTimer;
    private float readyTimer;
    private bool isTransitioning;



    private void Start()
    {
        // シーン開始時に明転させる。
        if (FadeController.Instance != null)
        {
            FadeController.Instance.FadeIn(0.5f);
        }

        AudioManager.Instance.FadeIn("BGM_main_beforechase",2.0f);

        EnterReady();
    }

    private void Update()
    {

        if (isTransitioning)
        {
            return;
        }

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
        //if (BootSceneController.Instance.DebugInput.NextScenePressed)
        //{
        //    EnterResult();
        //    return;
        //}
        playTimer -= Time.deltaTime;

        if (playTimer > 0f)
        {
            return;
        }

        playTimer = 0f;
        EnterResult();
    }

    private void EnterResult()
    {
        currentState = State.Result;
    }

    private void UpdateResult()
    {
        isTransitioning = true;
        SceneFlow.LoadResult();
    }

    public string GetCurrentStateName() { return currentState.ToString(); }
    public float GetRemainingPlayTime() { return playTimer; }
}