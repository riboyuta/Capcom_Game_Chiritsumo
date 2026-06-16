using Game.Input;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using unityroom.Api;

public sealed class ResultSceneController : MonoBehaviour
{
    private const int ClearTimeBoardNumber = 1;
    private const int DeathCountBoardNumber = 2;
    private const string ResultBgmCueName = "BGM_result";
    private const float ResultBgmFadeDuration = 1.0f;

    [Header("Result UI")]
    [SerializeField] private TMP_Text clearElapsedTimeText;
    [SerializeField] private string clearElapsedTimeFormat = "Clear Time: {0:F2}s";
    [Tooltip("タイムがカウントアップする時間（秒）")]
    [SerializeField] private float countUpDuration = 1.5f;
    [Header("入力参照")]
    [Tooltip("タイトル画面で使用する生入力取得元。未設定の場合は同じGameObjectから自動取得を試みる。")]
    [SerializeField] private RawInputSource rawInputSource;

    [Header("入力設定")]
    [Tooltip("タイトル画面のメニュー操作に使う入力バインド定義。上下移動と決定操作の割り当てをここで管理する。")]
    [SerializeField] private SystemInputBindings inputBindings = new SystemInputBindings();

    private SystemInputReader systemInputReader;
    private bool isTransitioning;
    private bool isCountingUp;
    private float targetClearTime;
    private int targetDeathCount;
    private void Awake()
    {
        if (!TryResolveRawInputSource())
        {
            return;
        }

        systemInputReader = new SystemInputReader(rawInputSource, inputBindings);
    }

    private void Start()
    {
        // Result シーンの BGM を再生する。
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.FadeIn(ResultBgmCueName, ResultBgmFadeDuration);
        }

        Debug.Log("[ResultSceneController] Start - Scene initialized.");
        Debug.Log($"[ResultSceneController] clearElapsedTimeText assigned: {clearElapsedTimeText != null}");

        if (clearElapsedTimeText != null)
        {
            Debug.Log($"[ResultSceneController] Text component name: {clearElapsedTimeText.gameObject.name}");
        }

        if (ResultSceneTransitData.TryConsumeClearResult(out float clearElapsedTime, out int deathCount))
        {
            Debug.Log($"[ResultSceneController] clearElapsedTime received={clearElapsedTime:F2}s, deathCount={deathCount}");
            ApplyClearResult(clearElapsedTime, deathCount);
            return;
        }

        Debug.LogWarning("[ResultSceneController] clearElapsedTime is not provided. Displaying default value.");

        if (clearElapsedTimeText != null)
        {
            clearElapsedTimeText.text = "--.--";
            Debug.Log("[ResultSceneController] Default time text set to '--.--'");
        }
        else
        {
            Debug.LogError("[ResultSceneController] Cannot display default time - clearElapsedTimeText is null!");
        }
    }

    private void Update()
    {
        if (isTransitioning)
        {
            return;
        }

        if (systemInputReader == null)
        {
            return;
        }

        systemInputReader.Update();

        if (WasAdvancePressedThisFrame())
        {
            if (isCountingUp)
            {
                // カウントアップのアニメーションをスキップして即座に最終結果を表示
                isCountingUp = false;
                clearElapsedTimeText.text = string.Format(clearElapsedTimeFormat, targetClearTime);
                Debug.Log("[ResultSceneController] Count up skipped.");

                // SE: カウントアップ終了(スキップ時)
                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.Stop("SFX_gameresult_drumroll");
                    AudioManager.Instance.PlayOverlap("SFX_game_clear");
                }
            }
            else
            {
                ReturnToTitle();
            }
        }
    }

    private bool WasAdvancePressedThisFrame()
    {
        bool submitPressed = systemInputReader != null && systemInputReader.SubmitPressed;
        bool leftMousePressed = rawInputSource != null && rawInputSource.LeftMouseButtonState.PressedThisFrame;

        return submitPressed || leftMousePressed;
    }

    public void ReturnToTitle()
    {
        // BGM をフェードアウトしてからTitleへ戻る。
        if (isTransitioning)
        {
            Debug.LogWarning("[ResultSceneController] ReturnToTitle called but already transitioning.");
            return;
        }

        isTransitioning = true;
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.FadeOut(ResultBgmCueName, ResultBgmFadeDuration);
        }

        Debug.Log("[ResultSceneController] ReturnToTitle - Starting transition to Title scene.");

        Debug.Log("[ResultSceneController] Starting fade out...");
        FadeController.EnsureInstance().FadeOut(onComplete: () =>
        {
            Debug.Log("[ResultSceneController] Fade out complete. Loading Title scene.");
            SceneFlow.LoadTitle();
        });
    }

    private void ApplyClearElapsedTime(float clearElapsedTime)
    {
        ApplyClearResult(clearElapsedTime, 0);
    }

    private void ApplyClearResult(float clearElapsedTime, int deathCount)
    {
        Debug.Log($"[ResultSceneController] ApplyClearResult called with time={clearElapsedTime:F2}s, deathCount={deathCount}");
        Debug.Log($"[ResultSceneController] clearElapsedTimeText is null: {clearElapsedTimeText == null}");

        if (clearElapsedTimeText == null)
        {
            Debug.LogError("[ResultSceneController] clearElapsedTimeText is not assigned in Inspector!");
            Debug.LogError("[ResultSceneController] Please assign a TMP_Text component in the Inspector.");
            return;

        }

        Debug.Log($"[ResultSceneController] Text object: {clearElapsedTimeText.gameObject.name}, Active: {clearElapsedTimeText.gameObject.activeInHierarchy}");

        targetClearTime = clearElapsedTime;
        targetDeathCount = Mathf.Max(0, deathCount);

        // SE: ドラムロール開始
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.Play("SFX_gameresult_drumroll");
        }

        // タイム確定時に自動的にランキングへ送信
        SendRankingScore();

        StartCoroutine(CountUpClearTime(clearElapsedTime));
    }

    private System.Collections.IEnumerator CountUpClearTime(float targetTime)
    {
        isCountingUp = true;
        float elapsedTime = 0f;

        while (elapsedTime < countUpDuration && isCountingUp)
        {
            elapsedTime += Time.deltaTime;
            
            // 0から1へ正規化された時間
            float t = Mathf.Clamp01(elapsedTime / countUpDuration);
            // EaseOutCubic（最後に向かってゆっくりになるイージング）
            float tEased = 1f - Mathf.Pow(1f - t, 3f);
            
            float currentTime = Mathf.Lerp(0f, targetTime, tEased);

            clearElapsedTimeText.text = string.Format(clearElapsedTimeFormat, currentTime);
            yield return null;
        }

        if (isCountingUp)
        {
            // スキップされずにアニメーションが終わった場合、最終値を正確に設定
            clearElapsedTimeText.text = string.Format(clearElapsedTimeFormat, targetTime);
            isCountingUp = false;

            // SE: カウントアップ終了(自然完了時)
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.Stop("SFX_gameresult_drumroll");
                AudioManager.Instance.PlayOverlap("SFX_game_clear");
            }
        }

        Debug.Log($"[ResultSceneController] ✓ Time displayed: {clearElapsedTimeText.text}");
    }
    private bool TryResolveRawInputSource()
    {
        if (rawInputSource != null)
        {
            return true;
        }

        rawInputSource = GetComponent<RawInputSource>();

        if (rawInputSource != null)
        {
            return true;
        }

        Debug.LogError("[ResultSceneController] RawInputSource is required.", this);
        enabled = false;
        return false;
    }

    /// UnityroomApiClientを用いて、クリアタイムをランキングに自動送信します。
    public void SendRankingScore()
    {
        // ボード番号(1)にスコアを送信します。
        // 第3引数の ScoreboardWriteMode は適宜変更
        Debug.Log($"[ResultSceneController] Send ranking score: clearTime={targetClearTime}s, deathCount={targetDeathCount}");
        UnityroomApiClient.Instance.SendScore(ClearTimeBoardNumber, targetClearTime, ScoreboardWriteMode.HighScoreAsc);
        UnityroomApiClient.Instance.SendScore(DeathCountBoardNumber, targetDeathCount, ScoreboardWriteMode.HighScoreAsc);
    }
}
