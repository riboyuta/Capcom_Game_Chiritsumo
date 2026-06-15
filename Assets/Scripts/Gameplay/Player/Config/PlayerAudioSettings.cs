using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerController))]
public sealed class PlayerAudioSettings : MonoBehaviour
{
    private enum WalkAudioMode
    {
        WalkInterval,
        WalkStartStop
    }

    [Header("参照")]
    [Tooltip("音声イベントを通知する対象の PlayerController です。未設定の場合は同じ GameObject から自動取得します。")]
    [SerializeField] private PlayerController playerController;

    [Header("ジャンプ音を使う")]
    [Tooltip("通常ジャンプに成功したとき、Jump イベントを通知するかを設定します。")]
    [SerializeField] private bool enableJump = true;

    [Header("壁ジャンプ音を使う")]
    [Tooltip("壁キックまたは壁捕まりジャンプに成功したとき、WallJump イベントを通知するかを設定します。")]
    [SerializeField] private bool enableWallJump = true;

    [Header("通常着地音を使う")]
    [Tooltip("強い着地条件を満たさない着地時に、Land イベントを通知するかを設定します。")]
    [SerializeField] private bool enableNormalLanding = true;

    [Header("強い着地音を使う")]
    [Tooltip("滞空時間や落下距離の条件を満たした着地時に、StrongLand イベントを通知するかを設定します。")]
    [SerializeField] private bool enableStrongLanding = true;

    [Header("壁滑り音を使う")]
    [Tooltip("壁滑りの開始時に WallSlideStart、終了時に WallSlideStop イベントを通知するかを設定します。")]
    [SerializeField] private bool enableWallSlide = true;

    [Header("地上ダッシュ音を使う")]
    [Tooltip("接地中にダッシュを開始したとき、GroundDash イベントを通知するかを設定します。")]
    [SerializeField] private bool enableGroundDash = true;

    [Header("空中ダッシュ音を使う")]
    [Tooltip("空中でダッシュを開始したとき、AirDash イベントを通知するかを設定します。")]
    [SerializeField] private bool enableAirDash = true;

    [Header("ダメージ死亡音を使う")]
    [Tooltip("ダメージ扱いで死亡したとき、DamageDeath イベントを通知するかを設定します。")]
    [SerializeField] private bool enableDamageDeath = true;

    [Header("ハザード死亡音を使う")]
    [Tooltip("落下や即死ギミックなどのハザード扱いで死亡したとき、HazardDeath イベントを通知するかを設定します。")]
    [SerializeField] private bool enableHazardDeath = true;

    [Header("リスポーン音を使う")]
    [Tooltip("チェックポイントへのリスポーン演出が完了したとき、Respawn イベントを通知するかを設定します。")]
    [SerializeField] private bool enableRespawn = true;

    [Header("歩行音を使う")]
    [Tooltip("接地中に横移動している間、歩行音イベントを通知するかを設定します。通知方式は下の歩行音の再生方式で選びます。")]
    [SerializeField] private bool enableWalk = true;

    [Header("歩行音の再生方式")]
    [Tooltip("歩行音の通知方式です。短い足音素材は Walk を一定間隔で通知、長い歩行音素材は WalkStart / WalkStop で開始と停止を分けます。")]
    [SerializeField] private WalkAudioMode walkAudioMode = WalkAudioMode.WalkStartStop;

    [Header("登り音を使う")]
    [Tooltip("壁捕まり中の上下移動、または崖乗り上げ中に、一定間隔で Climb イベントを通知するかを設定します。")]
    [SerializeField] private bool enableClimb = true;

    [Header("掴み音を使う")]
    [Tooltip("壁捕まり状態に入った瞬間、Grab イベントを通知するかを設定します。")]
    [SerializeField] private bool enableGrab = true;

    [Header("強い着地: 滞空時間条件を使う")]
    [Tooltip("StrongLand 判定に、ジャンプや落下で空中にいた時間の下限を使うかを設定します。")]
    [SerializeField] private bool useStrongLandingMinAirTime = true;

    [Header("強い着地: 最小滞空時間")]
    [Tooltip("StrongLand とみなすために必要な最小滞空時間です。値が大きいほど、短い落下では通常着地音になります。")]
    [SerializeField, Min(0f)] private float strongLandingMinAirTime = 0.20f;

    [Header("強い着地: 落下距離条件を使う")]
    [Tooltip("StrongLand 判定に、空中へ出てから着地するまでの落下距離下限を使うかを設定します。")]
    [SerializeField] private bool useStrongLandingMinFallHeight = true;

    [Header("強い着地: 最小落下距離")]
    [Tooltip("StrongLand とみなすために必要な最小落下距離です。値が大きいほど、高い場所から落ちたときだけ強い着地音になります。")]
    [SerializeField, Min(0f)] private float strongLandingMinFallHeight = 6.00f;

    [Header("歩行音の通知間隔")]
    [Tooltip("歩行音の再生方式が WalkInterval のときに、Walk イベントを連続通知する間隔です。0.01 秒未満にはできません。")]
    [SerializeField, Min(0.01f)] private float walkInterval = 0.30f;

    [Header("登り音の通知間隔")]
    [Tooltip("Climb イベントを連続通知する間隔です。壁登りや崖乗り上げ中の音のテンポを調整します。0.01 秒未満にはできません。")]
    [SerializeField, Min(0.01f)] private float climbInterval = 0.35f;

    private float walkTimer;
    private float climbTimer;
    private bool isWalkSoundPlaying;

    private void Awake()
    {
        if (playerController == null)
        {
            playerController = GetComponent<PlayerController>();
        }

        if (playerController != null)
        {
            playerController.SetAudioController(this);
        }
    }

    public void PlayJump()
    {
        if (!enableJump) return;
        AudioEvent.Emit(this, "Jump");
    }

    public void PlayWallJump()
    {
        if (!enableWallJump) return;
        AudioEvent.Emit(this, "WallJump");
    }

    public void PlayLanding(float airborneTime, float fallHeight)
    {
        bool passesAirTime = !useStrongLandingMinAirTime || airborneTime >= strongLandingMinAirTime;
        bool passesFallHeight = !useStrongLandingMinFallHeight || fallHeight >= strongLandingMinFallHeight;
        bool shouldPlayStrongLanding = enableStrongLanding && passesAirTime && passesFallHeight;

        if (shouldPlayStrongLanding)
        {
            AudioEvent.Emit(this, "StrongLand");
            return;
        }

        if (!enableNormalLanding) return;
        AudioEvent.Emit(this, "Land");
    }

    public void StartWallSlideSound()
    {
        if (!enableWallSlide) return;
        AudioEvent.Emit(this, "WallSlideStart");
    }

    public void StopWallSlideSound()
    {
        AudioEvent.Emit(this, "WallSlideStop");
    }

    public void PlayGroundDash()
    {
        if (!enableGroundDash) return;
        AudioEvent.Emit(this, "GroundDash");
    }

    public void PlayAirDash()
    {
        if (!enableAirDash) return;
        AudioEvent.Emit(this, "AirDash");
    }

    public void UpdateWalk(bool isWalking, float deltaTime)
    {
        if (!enableWalk)
        {
            StopWalkStartStopIfNeeded();
            walkTimer = 0f;
            return;
        }

        if (walkAudioMode == WalkAudioMode.WalkStartStop)
        {
            UpdateWalkStartStop(isWalking);
            return;
        }

        StopWalkStartStopIfNeeded();
        if (ShouldEmitLoopEvent(isWalking, ref walkTimer, walkInterval, deltaTime))
        {
            AudioEvent.Emit(this, "Walk");
        }
    }

    public void UpdateClimb(bool isClimbing, float deltaTime)
    {
        if (ShouldEmitLoopEvent(isClimbing && enableClimb, ref climbTimer, climbInterval, deltaTime))
        {
            AudioEvent.Emit(this, "Climb");
        }
    }

    public void PlayGrab()
    {
        if (!enableGrab) return;
        AudioEvent.Emit(this, "Grab");
    }

    public void PlayDeath(PlayerDeathCause cause)
    {
        StopWalkStartStopIfNeeded();
        AudioEvent.Emit(this, "WallSlideStop");

        if (cause == PlayerDeathCause.Hazard)
        {
            if (!enableHazardDeath) return;
            AudioEvent.Emit(this, "HazardDeath");
            return;
        }

        if (!enableDamageDeath) return;
        AudioEvent.Emit(this, "DamageDeath");
    }

    public void PlayRespawn()
    {
        if (!enableRespawn) return;
        AudioEvent.Emit(this, "Respawn");
    }

    public void StopAllSounds()
    {
        StopWalkStartStopIfNeeded();
        ResetLoopTimers();
        AudioEvent.Emit(this, "WallSlideStop");
    }

    private void UpdateWalkStartStop(bool isWalking)
    {
        walkTimer = 0f;

        if (!isWalking)
        {
            StopWalkStartStopIfNeeded();
            return;
        }

        if (isWalkSoundPlaying) return;

        // 長めの歩行音は、歩き始めと歩き終わりを別イベントにして重複再生を避ける。
        isWalkSoundPlaying = true;
        AudioEvent.Emit(this, "WalkStart");
    }

    private void StopWalkStartStopIfNeeded()
    {
        if (!isWalkSoundPlaying) return;

        isWalkSoundPlaying = false;
        AudioEvent.Emit(this, "WalkStop");
    }

    private bool ShouldEmitLoopEvent(
        bool isActive,
        ref float timer,
        float interval,
        float deltaTime)
    {
        if (!isActive)
        {
            timer = 0f;
            return false;
        }

        timer -= deltaTime;
        if (timer > 0f)
        {
            return false;
        }

        timer = Mathf.Max(0.01f, interval);
        return true;
    }

    private void ResetLoopTimers()
    {
        walkTimer = 0f;
        climbTimer = 0f;
    }
}
