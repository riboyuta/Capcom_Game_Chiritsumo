using UnityEngine;

public sealed class GameRoot : MonoBehaviour
{
    private enum State
    {
        Ready,
        Playing,
        Result
    }

    [Header("Flow Settings")]
    [SerializeField, Min(0f)] private float readyDuration = 1.0f;
    [SerializeField, Min(0f)] private float playDuration = 10.0f;

    private State currentState;
    private float playTimer;
    private float readyTimer;
    private bool isTransitioning;

    private InputReader inputReader;

    private void Awake()
    {
        inputReader = new InputReader();
    }

    private void Start()
    {
        EnterReady();
    }

    private void Update()
    {
        inputReader.Update();

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
        if (inputReader.DebugTransitionPressed)
        {
            EnterResult();
            return;
        }

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