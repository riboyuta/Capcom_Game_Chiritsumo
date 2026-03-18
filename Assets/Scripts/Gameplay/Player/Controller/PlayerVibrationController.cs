using System;
using UnityEngine;
using UnityEngine.InputSystem;

// 同じ GameObject に複数付けて、振動制御が競合しないようにする。
[DisallowMultipleComponent]

// PlayerController と同一 GameObject 上での利用を前提にする。
[RequireComponent(typeof(PlayerController))]
public sealed class PlayerVibrationController : MonoBehaviour
{
    // 振動の優先度。
    // 数字が大きいほど強い優先度として扱う。
    private enum RumblePriority
    {
        WallSlide = 0,
        Step = 1,
        StrongLanding = 2,
        WallKick = 3,
    }

    // 単発振動用の設定データ。
    // 壁キック、強着地、ステップなど「一定時間だけ鳴らす」振動に使う。
    [Serializable]
    private sealed class OneShotRumbleSettings
    {
        [Header("単発振動: 低周波")]
        [Tooltip("低周波モーター(左寄り)の強さです。重さ・鈍さ・衝撃感を担当します。強い着地や重いアクションほど高めに調整します。")]
        [Range(0f, 1f)]
        public float lowFrequency = 0.2f;

        [Header("単発振動: 高周波")]
        [Tooltip("高周波モーター(右寄り)の強さです。軽さ・鋭さ・素早さの印象を担当します。壁キックや空中ステップなど、キレを出したいときに調整します。")]
        [Range(0f, 1f)]
        public float highFrequency = 0.5f;

        [Header("単発振動: 再生時間")]
        [Tooltip("この単発振動を鳴らし続ける時間(秒)です。短いほどキレのある触感になり、長いほど余韻のある触感になります。")]
        [Min(0f)]
        public float duration = 0.08f;
    }

    // 壁滑り用の微振動設定データ。
    // 壁滑りは単発ではなく「短く鳴らす → 少し止める」を繰り返すパルス方式にしている。
    [Serializable]
    private sealed class WallSlideRumbleSettings
    {
        [Header("壁滑り微振動: 低周波")]
        [Tooltip("壁滑り中の低周波モーター(左寄り)の強さです。基本はかなり弱めにして、重すぎないザラつき感を作ります。")]
        [Range(0f, 1f)]
        public float lowFrequency = 0.015f;

        [Header("壁滑り微振動: 高周波")]
        [Tooltip("壁滑り中の高周波モーター(右寄り)の強さです。擦れている細かい感触を出したいときに調整します。低周波より少し高めにすると壁との接触感が出しやすいです。")]
        [Range(0f, 1f)]
        public float highFrequency = 0.05f;

        [Header("壁滑り微振動: パルス再生時間")]
        [Tooltip("1回の壁滑りパルスを鳴らす時間(秒)です。短いほど細かい断続振動になり、長いほど連続感が強くなります。")]
        [Min(0f)]
        public float pulseDuration = 0.03f;

        [Header("壁滑り微振動: パルス間隔")]
        [Tooltip("壁滑りパルスを止めている時間(秒)です。短いほど連続的に感じ、長いほど控えめで軽い触感になります。")]
        [Min(0f)]
        public float pulseInterval = 0.10f;
    }

    [Header("壁キック設定")]
    [Tooltip("壁キック発生時に再生する単発振動の設定です。強め・短めにすると気持ちよさが出しやすいです。")]
    [SerializeField]
    private OneShotRumbleSettings wallKick = new OneShotRumbleSettings
    {
        lowFrequency = 0.24f,
        highFrequency = 0.82f,
        duration = 0.09f
    };

    [Header("強い着地設定")]
    [Tooltip("強い着地時に再生する単発振動の設定です。低周波を強めると重い着地感を表現しやすくなります。")]
    [SerializeField]
    private OneShotRumbleSettings strongLanding = new OneShotRumbleSettings
    {
        lowFrequency = 0.72f,
        highFrequency = 0.18f,
        duration = 0.11f
    };

    [Header("地上前ステ設定")]
    [Tooltip("地上での前ステップ時に再生する単発振動の設定です。素早い移動感を出しつつ、重すぎない強さに調整する用途です。")]
    [SerializeField]
    private OneShotRumbleSettings groundStep = new OneShotRumbleSettings
    {
        lowFrequency = 0.20f,
        highFrequency = 0.30f,
        duration = 0.06f
    };

    [Header("空中前ステ設定")]
    [Tooltip("空中での前ステップ時に再生する単発振動の設定です。地上前ステより軽く鋭い印象にしたい場合に使います。")]
    [SerializeField]
    private OneShotRumbleSettings airStep = new OneShotRumbleSettings
    {
        lowFrequency = 0.10f,
        highFrequency = 0.40f,
        duration = 0.05f
    };

    [Header("壁滑り微振動設定")]
    [Tooltip("壁滑り中に断続的に再生する微振動の設定です。鳴らしっぱなしではなく、短いパルスで接触感を出すために使います。")]
    [SerializeField]
    private WallSlideRumbleSettings wallSlide = new WallSlideRumbleSettings();

    [Header("デバッグ表示設定")]
    [Tooltip("有効にすると、Inspector 上で現在の振動状態や使用中ゲームパッドの追跡用情報を確認しやすくなります。")]
    [SerializeField]
    private bool showDebugState = false;

    [Header("Runtime(Debug): 壁滑り要求状態")]
    [Tooltip("現在、壁滑り微振動の再生要求が有効かどうかを示す実行時デバッグ値です。通常は実行中の確認専用です。")]
    [SerializeField]
    private bool wallSlideActive;

    [Header("Runtime(Debug): 壁滑りパルス再生中")]
    [Tooltip("現在、壁滑り微振動の『鳴っている区間』かどうかを示す実行時デバッグ値です。false の場合は停止区間です。")]
    [SerializeField]
    private bool wallSlidePulsePlaying;

    [Header("Runtime(Debug): 壁滑りタイマー")]
    [Tooltip("壁滑り微振動の次の状態切り替えまでの残り時間です。パルス再生中または停止中の両方で使われます。")]
    [SerializeField]
    private float wallSlideTimer;

    [Header("Runtime(Debug): 単発振動タイマー")]
    [Tooltip("現在再生中の単発振動があと何秒続くかを示す実行時デバッグ値です。0 以下で単発振動なしです。")]
    [SerializeField]
    private float oneShotTimer;

    [Header("Runtime(Debug): 現在の振動名")]
    [Tooltip("現在どの振動状態を再生中かを識別するための名前です。挙動確認やデバッグログ確認時に使います。")]
    [SerializeField]
    private string activeRumbleName = "None";

    // 現在再生中の単発振動の優先度。
    // 低優先度の振動で上書きされないようにするために使う。
    private RumblePriority activeOneShotPriority;

    // 現在の Gamepad が一時的に取れなくなっても、最後に使った Gamepad を使えるように保持する。
    private Gamepad cachedGamepad;

    private void Awake()
    {
        // 初期状態では単発振動なし扱いにする。
        activeOneShotPriority = RumblePriority.WallSlide;
    }

    private void Update()
    {
        // 単発振動の残り時間を進める。
        UpdateOneShotTimer();

        // 壁滑り微振動のパルス制御を進める。
        UpdateWallSlidePulse();

        // デバッグ用に現在の Gamepad をキャッシュ更新する。
        UpdateDebugGamepadCache();
    }

    private void OnDisable()
    {
        // 無効化時に振動が残らないよう、必ず停止する。
        StopAllRumble();
    }

    private void OnDestroy()
    {
        // 破棄時も同様に振動を止める。
        StopAllRumble();
    }

    // 壁キック振動を再生する。
    public void PlayWallKick()
    {
        PlayOneShot(wallKick, RumblePriority.WallKick, "WallKick");
    }

    // 強着地振動を再生する。
    public void PlayStrongLanding()
    {
        PlayOneShot(strongLanding, RumblePriority.StrongLanding, "StrongLanding");
    }

    // 地上前ステ振動を再生する。
    public void PlayGroundStep()
    {
        PlayOneShot(groundStep, RumblePriority.Step, "GroundStep");
    }

    // 空中前ステ振動を再生する。
    public void PlayAirStep()
    {
        PlayOneShot(airStep, RumblePriority.Step, "AirStep");
    }

    // 壁滑り微振動を開始する。
    // 実際の振動開始は UpdateWallSlidePulse 側で行う。
    public void StartWallSlideRumble()
    {
        wallSlideActive = true;
        wallSlidePulsePlaying = false;
        wallSlideTimer = 0f;

        // 単発再生中でなければ、次フレームからすぐパルス開始可能にする。
        if (!IsOneShotPlaying())
        {
            activeRumbleName = "WallSlide";
        }
    }

    // 壁滑り微振動を停止する。
    public void StopWallSlideRumble()
    {
        wallSlideActive = false;
        wallSlidePulsePlaying = false;
        wallSlideTimer = 0f;

        // 単発振動が鳴っていないなら、その場でモーターも止める。
        if (!IsOneShotPlaying())
        {
            ResetCurrentGamepadHaptics();
            activeRumbleName = "None";
        }
    }

    // すべての振動状態を停止する。
    public void StopAllRumble()
    {
        wallSlideActive = false;
        wallSlidePulsePlaying = false;
        wallSlideTimer = 0f;
        oneShotTimer = 0f;
        activeRumbleName = "None";

        ResetCurrentGamepadHaptics();
    }

    // 単発振動を再生する共通処理。
    // priority により、今鳴っている単発振動を上書きしてよいかを判断する。
    private void PlayOneShot(OneShotRumbleSettings settings, RumblePriority priority, string rumbleName)
    {
        // Gamepad が取れなければ何もしない。
        if (!TryGetGamepad(out Gamepad gamepad))
        {
            return;
        }

        // より高い優先度の単発振動が再生中なら上書きしない。
        if (IsOneShotPlaying() && priority < activeOneShotPriority)
        {
            return;
        }

        // 壁滑り微振動は、単発再生時に一旦止める。
        wallSlidePulsePlaying = false;
        wallSlideTimer = 0f;

        // コントローラーのモーター速度を設定し、単発振動を開始する。
        gamepad.SetMotorSpeeds(settings.lowFrequency, settings.highFrequency);
        oneShotTimer = settings.duration;
        activeOneShotPriority = priority;
        activeRumbleName = rumbleName;
    }

    // 単発振動の時間管理を行う。
    private void UpdateOneShotTimer()
    {
        if (!IsOneShotPlaying())
        {
            return;
        }

        // 残り時間を減らす。
        oneShotTimer -= Time.deltaTime;
        if (oneShotTimer > 0f)
        {
            return;
        }

        // 単発振動終了。
        oneShotTimer = 0f;
        activeOneShotPriority = RumblePriority.WallSlide;

        // 壁滑り中なら次の UpdateWallSlidePulse で微振動へ戻す。
        if (!wallSlideActive)
        {
            ResetCurrentGamepadHaptics();
            activeRumbleName = "None";
        }
    }

    // 壁滑り微振動の「鳴らす / 止める」をパルス制御する。
    private void UpdateWallSlidePulse()
    {
        if (!wallSlideActive)
        {
            return;
        }

        // 単発振動が優先。
        if (IsOneShotPlaying())
        {
            return;
        }

        if (wallSlidePulsePlaying)
        {
            // 再生区間の残り時間を減らす。
            wallSlideTimer -= Time.deltaTime;
            if (wallSlideTimer > 0f)
            {
                return;
            }

            // パルス再生区間が終わったので、次は無音区間へ入る。
            wallSlidePulsePlaying = false;
            wallSlideTimer = wallSlide.pulseInterval;
            ResetCurrentGamepadHaptics();
            activeRumbleName = "WallSlideInterval";
            return;
        }

        // 今は無音区間。次のパルス開始まで待つ。
        wallSlideTimer -= Time.deltaTime;
        if (wallSlideTimer > 0f)
        {
            return;
        }

        // Gamepad が取れないなら開始できない。
        if (!TryGetGamepad(out Gamepad gamepad))
        {
            return;
        }

        // 次の壁滑りパルスを開始する。
        gamepad.SetMotorSpeeds(wallSlide.lowFrequency, wallSlide.highFrequency);
        wallSlidePulsePlaying = true;
        wallSlideTimer = wallSlide.pulseDuration;
        activeRumbleName = "WallSlidePulse";
    }

    // 単発振動が再生中かどうか。
    private bool IsOneShotPlaying()
    {
        return oneShotTimer > 0f;
    }

    // 使用する Gamepad を取得する。
    // 基本は Gamepad.current を優先し、取れない場合は最後に使えた cachedGamepad を使う。
    private bool TryGetGamepad(out Gamepad gamepad)
    {
        gamepad = Gamepad.current != null ? Gamepad.current : cachedGamepad;
        if (gamepad == null)
        {
            return false;
        }

        cachedGamepad = gamepad;
        return true;
    }

    // 現在の Gamepad の振動を停止する。
    private void ResetCurrentGamepadHaptics()
    {
        if (Gamepad.current != null)
        {
            Gamepad.current.ResetHaptics();
            cachedGamepad = Gamepad.current;
            return;
        }

        if (cachedGamepad != null)
        {
            cachedGamepad.ResetHaptics();
        }
    }

    // デバッグ表示中だけ、Gamepad のキャッシュを更新する。
    private void UpdateDebugGamepadCache()
    {
        if (!showDebugState)
        {
            return;
        }

        if (Gamepad.current != null)
        {
            cachedGamepad = Gamepad.current;
        }
    }
}