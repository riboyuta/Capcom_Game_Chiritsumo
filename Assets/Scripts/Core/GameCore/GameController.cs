using System.Collections;
using UnityEngine;

public sealed class GameController : MonoBehaviour
{
    private enum State
    {
        Ready,
        Playing,
        Result
    }

    private const string StageIntroBgmCueName = "BGM_main_beforechase";
    private const string StageChaseBgmCueName = "BGM_main_afterchase";
    private const string SceneAudioStageEnemyBgmCueName = "BGM_STAGE1_ENEMY";
    private const float StageBgmFadeDuration = 2.0f;

    [Header("進行フロー: Ready時間")]
    [Tooltip("Ready 状態を維持する時間(秒)です。開始演出や待機時間の長さを調整します。")]
    [SerializeField, Min(0f)] private float readyDuration = 1.0f;

    [Header("進行フロー: Play時間")]
    [Tooltip("Play 状態を維持する時間(秒)です。実際にプレイ可能な制限時間を調整します。")]
    [SerializeField, Min(0f)] private float playDuration = 10.0f;
    [Header("プレイヤー参照")]
    [Tooltip("死亡受理イベントを購読する PlayerFacade です。未設定時は実行時にシーン内から自動取得します。")]
    [SerializeField] private PlayerFacade playerFacade;
    private State currentState;
    private float playTimer;
    private float readyTimer;
    private float elapsedTime;
    private int deathCount;
    private bool isElapsedTimeRunning;
    private bool hasElapsedTimeStarted;
    private bool isTransitioning;
    private bool goalClearAccepted;
    private bool isPlayerDeathEventSubscribed;
    private float lastLoggedTime;
    private void Awake()
    {
        ResolvePlayerFacadeIfNeeded();
    }

    private void OnEnable()
    {
        SubscribePlayerDeathEventIfNeeded();
    }

    private void OnDisable()
    {
        UnsubscribePlayerDeathEventIfNeeded();
    }

    private void Start()
    {
        ResolvePlayerFacadeIfNeeded();
        SubscribePlayerDeathEventIfNeeded();

        // シーン開始時に明転させる。
        FadeController.EnsureInstance().FadeIn();

        StartCoroutine(StartStageIntroBgmAfterSceneStart());

        elapsedTime = 0f;
        deathCount = 0;
        isElapsedTimeRunning = false;
        hasElapsedTimeStarted = false;
        goalClearAccepted = false;
        lastLoggedTime = -1f;

        EnterReady();
    }

    private void Update()
    {
        if (isTransitioning)
        {
            return;
        }

        // 経過時間は、開始通知を受けた後だけ進める。
        UpdateElapsedTime();

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

    // EnemySpawnTrigger から開始通知を受けた後だけ経過時間を加算する。
    private void UpdateElapsedTime()
    {
        if (!isElapsedTimeRunning)
        {
            return;
        }

        elapsedTime += Time.unscaledDeltaTime;

        // 1秒ごとにログ出力する。
        if (Mathf.FloorToInt(elapsedTime) > Mathf.FloorToInt(lastLoggedTime))
        {
            lastLoggedTime = elapsedTime;
            Debug.Log($"[GameRoot] Elapsed Time: {elapsedTime:F2}s");
        }
    }

    private IEnumerator StartStageIntroBgmAfterSceneStart()
    {
        yield return null;

        if (hasElapsedTimeStarted)
        {
            yield break;
        }

        AudioManager audioManager = AudioManager.Instance;
        if (audioManager == null)
        {
            yield break;
        }

        audioManager.Stop(SceneAudioStageEnemyBgmCueName);
        audioManager.FadeIn(StageIntroBgmCueName, StageBgmFadeDuration);
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
    }

    private void EnterResult()
    {
        // Result に入ったら経過時間の加算を止める。
        isElapsedTimeRunning = false;
        currentState = State.Result;

        // BGM を停止する。
        AudioManager audioManager = AudioManager.Instance;
        if (audioManager != null)
        {
            audioManager.Stop(StageIntroBgmCueName);
            audioManager.Stop(StageChaseBgmCueName);
            audioManager.Stop(SceneAudioStageEnemyBgmCueName);
            // Result 遷移時に Stage 側のループSEを残さない。
            audioManager.StopAll(AudioChannel.SFX);
        }
    }

    private void UpdateResult()
    {
        isTransitioning = true;

        FadeController.EnsureInstance().FadeOut(onComplete: SceneFlow.LoadResult);
    }

    // EnemySpawnTrigger から最初の有効発動時に呼ばせる。
    public void StartOrResumeElapsedTime()
    {
        if (!hasElapsedTimeStarted)
        {
            elapsedTime = 0f;
            hasElapsedTimeStarted = true;
            lastLoggedTime = -1f;

            // BGM を変更する。
            AudioManager audioManager = AudioManager.Instance;
            if (audioManager != null)
            {
                audioManager.Stop(StageIntroBgmCueName);
                audioManager.Stop(SceneAudioStageEnemyBgmCueName);
                audioManager.FadeIn(StageChaseBgmCueName, StageBgmFadeDuration);

                // SE:最初のスポーン時
                //AudioManager.Instance.PlayOverlap("SFX_boss_spawn");
            }
        }

        isElapsedTimeRunning = true;
    }

    // 既存の外部参照を残しつつ、新しい開始/再開処理へ委譲する。
    public void StartElapsedTimeIfNeeded()
    {
        StartOrResumeElapsedTime();
    }

    // 累積値は保持したまま、次の安全エリア退出まで計測を止める。
    public void PauseElapsedTime()
    {
        isElapsedTimeRunning = false;
    }

    // 死亡受理ごとに死亡回数を加算し、リスポーン待機中の時間を累積しない。
    public void RecordPlayerDeathAndPauseTimer(PlayerDeathCause deathCause)
    {
        deathCount++;
        PauseElapsedTime();
        Debug.Log($"[GameRoot] Player death recorded. cause={deathCause}, deathCount={deathCount}, elapsedTime={elapsedTime:F2}s");
    }

    /// ゴール到達を受け付け、Result遷移を開始する。
    /// Playing 中のみ受理する。
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
        PauseElapsedTime();
        ResultSceneTransitData.SetClearResult(elapsedTime, deathCount);
        Debug.Log($"[GameRoot] GoalClear accepted. elapsedTime={elapsedTime:F2}s, deathCount={deathCount}");

        EnterResult();
        return true;
    }

    public string GetCurrentStateName() { return currentState.ToString(); }
    public float GetRemainingPlayTime() { return playTimer; }
    public float GetElapsedTime() { return elapsedTime; }
    public int GetDeathCount() { return deathCount; }

    private void ResolvePlayerFacadeIfNeeded()
    {
        if (playerFacade != null)
        {
            return;
        }

        playerFacade = FindFirstObjectByType<PlayerFacade>();
    }

    private void SubscribePlayerDeathEventIfNeeded()
    {
        if (isPlayerDeathEventSubscribed)
        {
            return;
        }

        ResolvePlayerFacadeIfNeeded();

        if (playerFacade == null)
        {
            return;
        }

        playerFacade.DeathAccepted += OnPlayerDeathAccepted;
        isPlayerDeathEventSubscribed = true;
    }

    private void UnsubscribePlayerDeathEventIfNeeded()
    {
        if (!isPlayerDeathEventSubscribed)
        {
            return;
        }

        if (playerFacade != null)
        {
            playerFacade.DeathAccepted -= OnPlayerDeathAccepted;
        }

        isPlayerDeathEventSubscribed = false;
    }

    private void OnPlayerDeathAccepted(PlayerDeathCause deathCause)
    {
        RecordPlayerDeathAndPauseTimer(deathCause);
    }
}
