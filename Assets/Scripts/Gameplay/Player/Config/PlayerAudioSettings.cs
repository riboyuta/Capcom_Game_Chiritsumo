using System;
using UnityEngine;

// 同じ GameObject に複数付けて、音声制御が競合しないようにする。
[DisallowMultipleComponent]

// PlayerController と同一 GameObject 上での利用を前提にする。
[RequireComponent(typeof(PlayerController))]
public sealed class PlayerAudioSettings : MonoBehaviour
{
    [Header("音声コントローラー参照")]
    [Tooltip("プレイヤー操作に応じた音声を再生する PlayerController 参照です。未設定時は Awake 初期化時に同一 GameObject から取得を試みます。")]
    [SerializeField]
    private PlayerController playerController;

    // ======================================================================
    //  イベント有効化トグル
    // ======================================================================

    [Header("音声イベント有効化: ジャンプ")]
    [Tooltip("有効時、通常ジャンプ開始時に音声を再生します。")]
    [SerializeField]
    private bool enableJump = true;

    [Header("音声イベント有効化: 壁ジャンプ")]
    [Tooltip("有効時、壁ジャンプ発生時に音声を再生します。")]
    [SerializeField]
    private bool enableWallJump = true;

    [Header("音声イベント有効化: 通常着地")]
    [Tooltip("有効時、通常着地時に音声を再生します。")]
    [SerializeField]
    private bool enableNormalLanding = true;

    [Header("音声イベント有効化: 強着地")]
    [Tooltip("有効時、強着地判定を満たした着地で強着地用の音声を再生します。")]
    [SerializeField]
    private bool enableStrongLanding = true;

    [Header("音声イベント有効化: 壁滑り")]
    [Tooltip("有効時、壁滑り中にループ音声を再生します。")]
    [SerializeField]
    private bool enableWallSlide = true;

    [Header("音声イベント有効化: 地上ダッシュ")]
    [Tooltip("有効時、地上でのダッシュ開始時に音声を再生します。")]
    [SerializeField]
    private bool enableGroundDash = true;

    [Header("音声イベント有効化: 空中ダッシュ")]
    [Tooltip("有効時、空中でのダッシュ開始時に音声を再生します。")]
    [SerializeField]
    private bool enableAirDash = true;

    [Header("音声イベント有効化: 死亡(Damage)")]
    [Tooltip("有効時、Damage 死亡開始時に音声を再生します。")]
    [SerializeField]
    private bool enableDamageDeath = true;

    [Header("音声イベント有効化: 死亡(Hazard)")]
    [Tooltip("有効時、Hazard 死亡開始時に音声を再生します。")]
    [SerializeField]
    private bool enableHazardDeath = true;

    // ======================================================================
    //  Audio ID 設定
    // ======================================================================

    [Header("Audio ID: ジャンプ")]
    [Tooltip("通常ジャンプ時に再生する AudioDef の ID です。")]
    [SerializeField]
    private string jumpAudioId = "";

    [Header("Audio ID: 壁ジャンプ")]
    [Tooltip("壁ジャンプ時に再生する AudioDef の ID です。")]
    [SerializeField]
    private string wallJumpAudioId = "";

    [Header("Audio ID: 通常着地")]
    [Tooltip("通常着地時に再生する AudioDef の ID です。")]
    [SerializeField]
    private string normalLandingAudioId = "";

    [Header("Audio ID: 強着地")]
    [Tooltip("強着地時に再生する AudioDef の ID です。")]
    [SerializeField]
    private string strongLandingAudioId = "";

    [Header("Audio ID: 壁滑り")]
    [Tooltip("壁滑り中にループ再生する AudioDef の ID です。")]
    [SerializeField]
    private string wallSlideAudioId = "";

    [Header("Audio ID: 地上ダッシュ")]
    [Tooltip("地上ダッシュ開始時に再生する AudioDef の ID です。")]
    [SerializeField]
    private string groundDashAudioId = "";

    [Header("Audio ID: 空中ダッシュ")]
    [Tooltip("空中ダッシュ開始時に再生する AudioDef の ID です。")]
    [SerializeField]
    private string airDashAudioId = "";

    [Header("Audio ID: 死亡(Damage)")]
    [Tooltip("Damage 死亡開始時に再生する AudioDef の ID です。")]
    [SerializeField]
    private string damageDeathAudioId = "";

    [Header("Audio ID: 死亡(Hazard)")]
    [Tooltip("Hazard 死亡開始時に再生する AudioDef の ID です。")]
    [SerializeField]
    private string hazardDeathAudioId = "";

    // ======================================================================
    //  強着地判定しきい値
    // ======================================================================

    [Header("強着地判定: 空中時間チェック有効")]
    [Tooltip("有効時、強着地判定に最小空中時間の条件を使用します。")]
    [SerializeField]
    private bool useStrongLandingMinAirTime = true;

    [Header("強着地判定: 最小空中時間")]
    [Tooltip("強着地として扱うために必要な最小空中時間(秒)です。")]
    [SerializeField, Min(0f)]
    private float strongLandingMinAirTime = 0.20f;

    [Header("強着地判定: 落差チェック有効")]
    [Tooltip("有効時、強着地判定に最高点から着地点までの落差条件を使用します。")]
    [SerializeField]
    private bool useStrongLandingMinFallHeight = true;

    [Header("強着地判定: 最小落差")]
    [Tooltip("強着地として扱う最高点から着地点までの最小落差です。")]
    [SerializeField, Min(0f)]
    private float strongLandingMinFallHeight = 6.00f;

    // ======================================================================
    //  Lifecycle
    // ======================================================================

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

    // ======================================================================
    //  Public API — PlayerController.Audio.cs から呼ばれる
    // ======================================================================

    // 通常ジャンプ音を再生する。
    public void PlayJump()
    {
        if (!enableJump) return;
        PlayOverlapSafe(jumpAudioId);
    }

    // 壁ジャンプ音を再生する。
    public void PlayWallJump()
    {
        if (!enableWallJump) return;
        PlayOverlapSafe(wallJumpAudioId);
    }

    // 着地音を再生する。
    // airborneTime と fallHeight から通常/強着地を判定する。
    public void PlayLanding(float airborneTime, float fallHeight)
    {
        bool passesAirTime = !useStrongLandingMinAirTime || airborneTime >= strongLandingMinAirTime;
        bool passesFallHeight = !useStrongLandingMinFallHeight || fallHeight >= strongLandingMinFallHeight;
        bool shouldPlayStrongLanding = enableStrongLanding && passesAirTime && passesFallHeight;

        if (shouldPlayStrongLanding)
        {
            PlayOverlapSafe(strongLandingAudioId);
            return;
        }

        if (!enableNormalLanding) return;
        PlayOverlapSafe(normalLandingAudioId);
    }

    // 壁滑りループ音を開始する。
    public void StartWallSlideSound()
    {
        if (!enableWallSlide) return;
        PlaySafe(wallSlideAudioId);
    }

    // 壁滑りループ音を停止する。
    public void StopWallSlideSound()
    {
        StopSafe(wallSlideAudioId);
    }

    // 地上ダッシュ音を再生する。
    public void PlayGroundDash()
    {
        if (!enableGroundDash) return;
        PlayOverlapSafe(groundDashAudioId);
    }

    // 空中ダッシュ音を再生する。
    public void PlayAirDash()
    {
        if (!enableAirDash) return;
        PlayOverlapSafe(airDashAudioId);
    }

    // 死亡音を再生する。
    // 死亡開始時は壁滑りループを止めてから再生する。
    public void PlayDeath(PlayerDeathCause cause)
    {
        StopSafe(wallSlideAudioId);

        if (cause == PlayerDeathCause.Hazard)
        {
            if (!enableHazardDeath) return;
            PlayOverlapSafe(hazardDeathAudioId);
        }
        else
        {
            if (!enableDamageDeath) return;
            PlayOverlapSafe(damageDeathAudioId);
        }
    }

    // すべての管理音を停止する。
    // 復帰時などに呼ばれる。
    public void StopAllSounds()
    {
        StopSafe(wallSlideAudioId);
    }

    // ======================================================================
    //  Private — AudioManager ラッパー
    // ======================================================================

    // ID が空でなければ PlayOverlap で再生する。
    private void PlayOverlapSafe(string audioId)
    {
        if (string.IsNullOrEmpty(audioId)) return;
        if (AudioManager.Instance == null) return;
        AudioManager.Instance.PlayOverlap(audioId);
    }

    // ID が空でなければ Play で再生する（ループ用）。
    private void PlaySafe(string audioId)
    {
        if (string.IsNullOrEmpty(audioId)) return;
        if (AudioManager.Instance == null) return;
        AudioManager.Instance.Play(audioId);
    }

    // ID が空でなければ Stop する。
    private void StopSafe(string audioId)
    {
        if (string.IsNullOrEmpty(audioId)) return;
        if (AudioManager.Instance == null) return;
        AudioManager.Instance.Stop(audioId);
    }
}
