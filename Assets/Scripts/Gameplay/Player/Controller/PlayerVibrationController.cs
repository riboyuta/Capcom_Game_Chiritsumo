using System;
using UnityEngine;
using UnityEngine.InputSystem;

// 同じ GameObject に複数付けて、振動制御が競合しないようにする。
[DisallowMultipleComponent]

// PlayerController と同一 GameObject 上での利用を前提にする。
[RequireComponent(typeof(PlayerController))]
public sealed class PlayerVibrationController : MonoBehaviour
{
    [Header("振動: コントローラー参照")]
    [Tooltip("プレイヤー操作に応じたコントローラー振動を送る PlayerController 参照です。未設定時は Awake 初期化時に同一 GameObject から取得を試みます。")]
    [SerializeField]
    private PlayerController playerController;

    // 振動の優先度。
    // 数字が大きいほど強い優先度として扱う。
    private enum RumblePriority
    {
        WallSlide = 0,
        Step = 1,
        NormalLanding = 2,
        StrongLanding = 3,
        WallKick = 4,
        Death = 5,
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

    [Header("振動イベント有効化: 地上前ステ")]
    [Tooltip("有効時、地上での前ステップ開始時に単発振動を再生します。移動系の振動比較や一時的な無効化に使います。")]
    [SerializeField]
    private bool enableGroundStep = true;

    [Header("振動イベント有効化: 空中前ステ")]
    [Tooltip("有効時、空中での前ステップ開始時に単発振動を再生します。地上前ステとの違いを確認したいときに使います。")]
    [SerializeField]
    private bool enableAirStep = true;

    [Header("振動イベント有効化: 壁キック")]
    [Tooltip("有効時、壁キック発生時に単発振動を再生します。壁アクション系の気持ちよさ確認や比較調整に使います。")]
    [SerializeField]
    private bool enableWallKick = true;

    [Header("振動イベント有効化: 壁滑り")]
    [Tooltip("有効時、壁滑り中に微振動を断続的に再生します。壁接触感の有無を切り替えて比較したいときに使います。")]
    [SerializeField]
    private bool enableWallSlide = true;

    [Header("振動イベント有効化: 通常着地")]
    [Tooltip("有効時、通常着地時に単発振動を再生します。基本となる着地感の有無を調整するときに使います。")]
    [SerializeField]
    private bool enableNormalLanding = true;

    [Header("振動イベント有効化: 強着地")]
    [Tooltip("有効時、強着地判定を満たした着地で強着地用の単発振動を再生します。通常着地との差分確認に使います。")]
    [SerializeField]
    private bool enableStrongLanding = true;

    [Header("振動イベント有効化: 死亡(Damage)")]
    [Tooltip("有効時、Damage 死亡開始時に単発振動を再生します。")]
    [SerializeField]
    private bool enableDamageDeath = true;

    [Header("振動イベント有効化: 死亡(Hazard)")]
    [Tooltip("有効時、Hazard 死亡開始時に単発振動を再生します。")]
    [SerializeField]
    private bool enableHazardDeath = true;


    [Header("強着地判定: 空中時間チェック有効")]
    [Tooltip("有効時、強着地判定に最小空中時間の条件を使用します。短い段差着地を強着地扱いしたくない場合に使います。")]
    [SerializeField]
    private bool useStrongLandingMinAirTime = true;

    [Header("強着地判定: 最小空中時間")]
    [Tooltip("強着地として扱うために必要な最小空中時間(秒)です。値を上げるほど、長く空中にいた着地だけが強着地になります。")]
    [SerializeField, Min(0f)]
    private float strongLandingMinAirTime = 0.20f;

    [Header("強着地判定: 落差チェック有効")]
    [Tooltip("有効時、強着地判定に最高点から着地点までの落差条件を使用します。見た目より低い落下を強着地扱いしたくない場合に使います。")]
    [SerializeField]
    private bool useStrongLandingMinFallHeight = true;

    [Header("強着地判定: 最小落差")]
    [Tooltip("強着地として扱う最高点から着地点までの最小落差です。値を上げるほど、高い位置からの落下だけが強着地になります。")]
    [SerializeField, Min(0f)]
    private float strongLandingMinFallHeight = 6.00f;


    [Header("単発振動設定: 地上前ステ")]
    [Tooltip("地上での前ステップ時に再生する単発振動の設定です。素早い移動感を出しつつ、重くなりすぎない触感に調整する用途です。")]
    [SerializeField]
    private OneShotRumbleSettings groundStep = new OneShotRumbleSettings
    {
        lowFrequency = 0.20f,
        highFrequency = 0.30f,
        duration = 0.06f
    };

    [Header("単発振動設定: 空中前ステ")]
    [Tooltip("空中での前ステップ時に再生する単発振動の設定です。地上前ステより軽く鋭い印象を出したい場合に調整します。")]
    [SerializeField]
    private OneShotRumbleSettings airStep = new OneShotRumbleSettings
    {
        lowFrequency = 0.10f,
        highFrequency = 0.40f,
        duration = 0.05f
    };

    [Header("単発振動設定: 壁キック")]
    [Tooltip("壁キック発生時に再生する単発振動の設定です。強めかつ短めにすると、跳ね返る気持ちよさを出しやすくなります。")]
    [SerializeField]
    private OneShotRumbleSettings wallKick = new OneShotRumbleSettings
    {
        lowFrequency = 0.24f,
        highFrequency = 0.82f,
        duration = 0.09f
    };

    [Header("微振動設定: 壁滑り")]
    [Tooltip("壁滑り中に断続的に再生する微振動の設定です。鳴らしっぱなしではなく、短いパルスで接触感を表現するために使います。")]
    [SerializeField]
    private WallSlideRumbleSettings wallSlide = new WallSlideRumbleSettings();

    [Header("微振動設定: 敵接近(Proximity)")]
    [Tooltip("敵が近接した際に鳴らす持続的な振動設定。画面揺れやVignetteの強度に連動させることを想定しています。")]
    [SerializeField]
    private bool enableProximityRumble = true;
    [SerializeField, Range(0f, 1f)]
    private float maxProximityLowFrequency = 0.5f;
    [SerializeField, Range(0f, 1f)]
    private float maxProximityHighFrequency = 0.5f;

    [Header("単発振動設定: 通常着地")]
    [Tooltip("通常着地時に再生する単発振動の設定です。基準となる弱めの着地感として調整します。")]
    [SerializeField]
    private OneShotRumbleSettings normalLanding = new OneShotRumbleSettings
    {
        lowFrequency = 0.22f,
        highFrequency = 0.10f,
        duration = 0.06f
    };

    [Header("単発振動設定: 強着地")]
    [Tooltip("強着地時に再生する単発振動の設定です。低周波を強めると重い衝撃感を出しやすく、通常着地との差別化に使えます。")]
    [SerializeField]
    private OneShotRumbleSettings strongLanding = new OneShotRumbleSettings
    {
        lowFrequency = 0.72f,
        highFrequency = 0.18f,
        duration = 0.11f
    };

    [Header("単発振動設定: 死亡(Damage)")]
    [Tooltip("Damage 死亡開始時に再生する単発振動の設定です。死亡開始を確実に知覚できるよう、既存の移動系振動より優先して鳴らします。")]
    [SerializeField]
    private OneShotRumbleSettings damageDeath = new OneShotRumbleSettings
    {
        lowFrequency = 0.80f,
        highFrequency = 0.35f,
        duration = 0.16f
    };

    [Header("単発振動設定: 死亡(Hazard)")]
    [Tooltip("Hazard 死亡開始時に再生する単発振動の設定です。Damage 死亡と分けて調整できます。")]
    [SerializeField]
    private OneShotRumbleSettings hazardDeath = new OneShotRumbleSettings
    {
        lowFrequency = 0.58f,
        highFrequency = 0.75f,
        duration = 0.12f
    };



    [Header("デバッグ表示: 有効化")]
    [Tooltip("有効にすると、Inspector 上で現在の振動状態やゲームパッド追跡用の情報を確認しやすくなります。通常は調整時のみ有効化します。")]
    [SerializeField]
    private bool showDebugState = false;

    [Header("Runtime(Debug): 壁滑り要求状態")]
    [Tooltip("現在、壁滑り微振動の再生要求が有効かどうかを示す実行時デバッグ値です。通常は挙動確認専用です。")]
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
    [Tooltip("現在再生中の単発振動があと何秒続くかを示す実行時デバッグ値です。0 以下のときは単発振動を再生していません。")]
    [SerializeField]
    private float oneShotTimer;

    [Header("Runtime(Debug): 敵接近振動強度")]
    [Tooltip("敵接近時の持続的な振動の現在強度です。他から毎フレーム設定され、徐々に減衰します。")]
    [SerializeField]
    private float debugProximityIntensity;

    [Header("Runtime(Debug): 現在の振動名")]
    [Tooltip("現在どの振動状態を再生中かを識別するための名前です。挙動確認やログ照合時の目印として使います。")]
    [SerializeField]
    
    private string activeRumbleName = "None";

    // 現在再生中の単発振動の優先度。
    // 低優先度の振動で上書きされないようにするために使う。
    private RumblePriority activeOneShotPriority;

    // 現在の Gamepad が一時的に取れなくなっても、最後に使った Gamepad を使えるように保持する。
    private Gamepad cachedGamepad;

    private void Awake()
    {
        if (playerController == null)
        {
            playerController = GetComponent<PlayerController>();
        }

        if (playerController != null)
        {
            playerController.SetVibrationController(this);
        }

        // 初期状態では単発振動なし扱いにする。
        activeOneShotPriority = RumblePriority.WallSlide;
    }

    private void Update()
    {
        // 単発振動の残り時間を進める。
        UpdateOneShotTimer();

        // 壁滑り微振動のパルス制御を進める。
        UpdateWallSlidePulse();

        // 敵接近時の持続的な振動制御を進める。
        UpdateProximityRumble();

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
        if (!enableWallKick)
        {
            return;
        }

        PlayOneShot(wallKick, RumblePriority.WallKick, "WallKick");
    }

    // 着地振動を再生する。
    // 主判定は fallHeight で、airborneTime は誤爆防止の保険として使う。
    public void PlayLanding(float airborneTime, float fallHeight)
    {
        bool passesAirTime = !useStrongLandingMinAirTime || airborneTime >= strongLandingMinAirTime;
        bool passesFallHeight = !useStrongLandingMinFallHeight || fallHeight >= strongLandingMinFallHeight;
        bool shouldPlayStrongLanding = enableStrongLanding && passesAirTime && passesFallHeight;

        if (shouldPlayStrongLanding)
        {
            PlayOneShot(strongLanding, RumblePriority.StrongLanding, "StrongLanding");
            return;
        }

        if (!enableNormalLanding)
        {
            return;
        }

        PlayOneShot(normalLanding, RumblePriority.NormalLanding, "NormalLanding");
    }

    // 地上前ステ振動を再生する。
    public void PlayGroundStep()
    {
        if (!enableGroundStep)
        {
            return;
        }

        PlayOneShot(groundStep, RumblePriority.Step, "GroundStep");
    }

    // 空中前ステ振動を再生する。
    // 前ステ振動を再生する。(空中)
    public void PlayAirStep()
    {
        if (!enableAirStep)
        {
            return;
        }

        PlayOneShot(airStep, RumblePriority.Step, "AirStep");
    }

    // 敵接近などによる継続的な振動強度を設定する。
    // 毎フレーム呼ばれることを想定しており、一定時間呼ばれないと自動で減衰・停止する。
    public void SetContinuousProximityIntensity(float intensity)
    {
        if (!enableProximityRumble)
        {
            return;
        }

        if (playerController != null && playerController.IsDeadState)
        {
            return;
        }

        // フレーム内で複数回呼ばれた場合は強い方を採用する
        debugProximityIntensity = Mathf.Max(debugProximityIntensity, Mathf.Clamp01(intensity));
    }

    // 壁滑り微振動を開始する。
    // 実際の振動開始は UpdateWallSlidePulse 側で行う。
    public void StartWallSlideRumble()
    {
        if (!enableWallSlide)
        {
            return;
        }

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
        activeOneShotPriority = RumblePriority.WallSlide;
        activeRumbleName = "None";

        ResetCurrentGamepadHaptics();
    }
    // 死亡振動を再生する。
    // 死亡開始時は最優先で、残留振動を必ず停止してから単発再生する。
    public void PlayDeath(PlayerController.DeathCause cause)
    {
        StopAllRumble();

        OneShotRumbleSettings settings;
        string rumbleName;

        if (cause == PlayerController.DeathCause.Hazard)
        {
            if (!enableHazardDeath)
            {
                return;
            }

            settings = hazardDeath;
            rumbleName = "HazardDeath";
        }
        else
        {
            if (!enableDamageDeath)
            {
                return;
            }

            settings = damageDeath;
            rumbleName = "DamageDeath";
        }

        PlayOneShot(settings, RumblePriority.Death, rumbleName);
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

    // 敵接近時の持続的な振動制御を行う。
    private void UpdateProximityRumble()
    {
        if (!enableProximityRumble)
        {
            return;
        }

        // 死亡状態のときは受け付けず強度をリセットする
        if (playerController != null && playerController.IsDeadState)
        {
            debugProximityIntensity = 0f;
            return;
        }

        // 単発振動や壁滑りパルスが優先される場合は、接近強度は減衰させるだけにしてモーターは弄らない
        if (IsOneShotPlaying() || (wallSlideActive && wallSlidePulsePlaying))
        {
            debugProximityIntensity = Mathf.Lerp(debugProximityIntensity, 0f, Time.deltaTime * 5f);
            return;
        }

        if (debugProximityIntensity > 0.01f)
        {
            if (TryGetGamepad(out Gamepad gamepad))
            {
                float low = maxProximityLowFrequency * debugProximityIntensity;
                float high = maxProximityHighFrequency * debugProximityIntensity;
                gamepad.SetMotorSpeeds(low, high);
            }
            activeRumbleName = "ProximityContinuous";

            // 毎フレーム更新されない場合は自動的に0へ向かって減衰する
            debugProximityIntensity = Mathf.Lerp(debugProximityIntensity, 0f, Time.deltaTime * 5f);

            // 十分に小さくなったら停止を確実にする
            if (debugProximityIntensity < 0.01f)
            {
                debugProximityIntensity = 0f;
                ResetCurrentGamepadHaptics();
                if (activeRumbleName == "ProximityContinuous")
                {
                    activeRumbleName = "None";
                }
            }
        }
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