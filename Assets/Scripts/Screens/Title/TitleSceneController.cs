using System.Collections;
using Game.Input;
using UnityEngine;

public sealed class TitleSceneController : MonoBehaviour
{
    public enum TitleMenuItem
    {
        Start = 0,
        Exit = 1,
    }

    [Header("演出時間")]
    [Tooltip("タイトル開始時とシーン遷移時のフェード時間。秒単位で調整する。")]
    [Min(0.0f)]

    [SerializeField] private float titleFadeDuration = 2.0f;

    [Tooltip("タイトルBGMのフェードアウト時間。秒単位で調整する。")]
    [Min(0.0f)]

    [SerializeField] private float titleBgmFadeDuration = 1.0f;

    [Tooltip("終了SE再生後にアプリ終了または再生停止へ進むまでの待機時間。秒単位で調整する。")]
    [Min(0.0f)]
    [SerializeField] private float exitWaitDuration = 0.4f;

    private const string TitleBgmCueName = "BGM_title";
    private const string CursorMoveSeCueName = "SFX_cursor_move";
    private const string StartSeCueName = "SFX_button_enter";
    private const string ExitSeCueName = "SFX_button_back";

    [Header("表示参照")]
    [Tooltip("タイトルメニューの見た目を更新するView。現在の選択項目を表示へ反映するために使用する。")]
    [SerializeField] private TitleMenuView menuView;

    [Header("入力参照")]
    [Tooltip("タイトル画面で使用する生入力取得元。未設定の場合は同じGameObjectから自動取得を試みる。")]
    [SerializeField] private RawInputSource rawInputSource;

    [Header("入力設定")]
    [Tooltip("タイトル画面のメニュー操作に使う入力バインド定義。上下移動と決定操作の割り当てをここで管理する。")]
    [SerializeField] private SystemInputBindings inputBindings = new SystemInputBindings();

    private SystemInputReader systemInputReader;
    private TitleMenuItem currentItem = TitleMenuItem.Start;
    private bool isTransitioning;

    // タイトル画面で必要な参照を解決し、入力リーダーを生成する。
    // 入力元が解決できない場合は、このコンポーネント自体を停止して以降の誤動作を防ぐ。
    private void Awake()
    {
        if (!TryResolveRawInputSource())
        {
            return;
        }

        systemInputReader = new SystemInputReader(rawInputSource, inputBindings);
    }

    // タイトル画面の開始時に初期演出と初期表示を行う。
    // ここではフェードイン、BGM開始、メニュー表示同期を担当する。
    private void Start()
    {
        Debug.Log("[TitleSceneController] Title scene started.", this);

        if (menuView == null)
        {
            Debug.LogWarning("[TitleSceneController] TitleMenuView is not assigned. Menu selection will not be visible.", this);
        }

        FadeController.EnsureInstance().FadeIn();

        AudioManager audioManager = AudioManager.Instance;
        if (audioManager != null)
        {
            audioManager.FadeIn(TitleBgmCueName, titleBgmFadeDuration);
        }

        RefreshView();
    }

    // 毎フレーム入力状態を更新し、メニュー移動と決定処理を実行する。
    // 画面遷移中は二重入力を防ぐため、以降の入力処理を停止する。
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

        HandleMove();
        HandleSubmit();
    }

    // 生入力取得元を確定する。
    // Inspector未設定時は同一GameObjectから取得を試み、見つからなければ停止する。
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

        Debug.LogError("[TitleSceneController] RawInputSource is required.", this);
        enabled = false;
        return false;
    }

    // 上下入力をもとに現在の選択項目を更新する。
    // 選択が変わったときだけView反映とカーソルSE再生を行い、無駄な更新を避ける。
    private void HandleMove()
    {
        int nextIndex = (int)currentItem;

        if (systemInputReader.UpPressed)
        {
            nextIndex--;
        }
        else if (systemInputReader.DownPressed)
        {
            nextIndex++;
        }

        nextIndex = Mathf.Clamp(nextIndex, (int)TitleMenuItem.Start, (int)TitleMenuItem.Exit);

        TitleMenuItem nextItem = (TitleMenuItem)nextIndex;
        if (nextItem == currentItem)
        {
            return;
        }

        currentItem = nextItem;
        RefreshView();

        AudioManager audioManager = AudioManager.Instance;
        if (audioManager != null)
        {
            audioManager.PlayOverlap(CursorMoveSeCueName);
        }
    }

    // 決定入力を受け取り、現在選択中の項目に応じた処理へ分岐する。
    // メニューごとの責務は StartGame / ExitGame 側へ委譲する。
    private void HandleSubmit()
    {
        if (!systemInputReader.SubmitPressed)
        {
            return;
        }

        switch (currentItem)
        {
            case TitleMenuItem.Start:
                StartGame();
                break;

            case TitleMenuItem.Exit:
                ExitGame();
                break;
        }
    }

    // 現在の選択状態をViewへ反映する。
    // View未設定時は何もしないことで、ロジック側の処理継続を優先する。
    private void RefreshView()
    {
        if (menuView == null)
        {
            return;
        }

        menuView.ApplySelection(currentItem);
    }

    // ゲーム開始遷移を開始する。
    // 二重起動を防ぐため、遷移開始後はフラグを立てて再入を禁止する。
    public void StartGame()
    {
        if (isTransitioning)
        {
            return;
        }

        isTransitioning = true;
        StartCoroutine(StartGameCoroutine());
    }

    // 開始SE、BGMフェードアウト、画面フェードアウトを行った後にゲームシーンへ遷移する。
    // 演出とシーン切り替えのタイミングをここで直列化して管理する。
    private IEnumerator StartGameCoroutine()
    {
        AudioManager audioManager = AudioManager.Instance;
        if (audioManager != null)
        {
            audioManager.PlayOverlap(StartSeCueName);
            audioManager.FadeOut(TitleBgmCueName, titleBgmFadeDuration);
        }

        bool fadeComplete = false;
        FadeController.EnsureInstance().FadeOut(titleFadeDuration, onComplete: () => fadeComplete = true);
        yield return new WaitUntil(() => fadeComplete);

        SceneFlow.LoadTutorial();
    }

    // 終了遷移を開始する。
    // 二重実行を防ぐため、すでに遷移中なら何もしない。
    public void ExitGame()
    {
        if (isTransitioning)
        {
            return;
        }

        isTransitioning = true;
        StartCoroutine(ExitGameCoroutine());
    }

    // 終了SEを再生してから、Editor上では再生停止、本番環境ではアプリ終了を行う。
    // Editorとビルド後で終了処理が異なるため、条件コンパイルで分岐する。
    private IEnumerator ExitGameCoroutine()
    {
        AudioManager audioManager = AudioManager.Instance;
        if (audioManager != null)
        {
            audioManager.PlayOverlap(ExitSeCueName);
        }

        yield return new WaitForSeconds(exitWaitDuration);

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
