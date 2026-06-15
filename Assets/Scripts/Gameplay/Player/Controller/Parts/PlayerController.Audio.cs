using UnityEngine;

public sealed partial class PlayerController
{
    private const float WalkAudioInputThreshold = 0.5f;
    private const float WalkAudioVelocityThreshold = 0.05f;
    private const float ClimbAudioVelocityThreshold = 0.05f;

    // 実際の音声再生を担当するコンポーネント。
    // この partial は「いつ音を鳴らすか」を決める役割で、
    // 実際の AudioManager 呼び出しは PlayerAudioSettings 側に任せる。
    private PlayerAudioSettings audioController;
    private bool wasWallGrabbingForAudio;

    // PlayerAudioSettings.Awake() から呼ばれて自身を登録する。
    internal void SetAudioController(PlayerAudioSettings controller)
    {
        audioController = controller;
    }

    // 既存 FixedUpdate の UpdateVibrationEvents() と同じタイミングで呼ぶ。
    // そのフレームの移動状態が確定したあとに、音声イベントを通知する。
    private void UpdateAudioEvents()
    {
        // 音声コンポーネントが無ければ何もしない。
        if (audioController == null)
        {
            return;
        }

        // 状態変化に応じて各種音声イベントを判定する。
        UpdateWallSlideSound();
        UpdateGrabSound();
        UpdateDashSound();
        UpdateLandingSound();
        UpdateWalkSound();
        UpdateClimbSound();
        CacheAudioState();
    }

    // 壁滑り開始/終了に応じてループ音を開始/停止する。
    // 判定ロジックは Vibration.cs 側と同じ前フレーム比較を使う。
    // wasWallSliding / isWallSliding は Vibration.cs 側で管理済み。
    private void UpdateWallSlideSound()
    {
        // 前フレームでは壁滑りしておらず、今フレームで壁滑りに入った。
        if (!wasWallSliding && runtimeState.isWallSliding)
        {
            audioController.StartWallSlideSound();
        }
        // 前フレームでは壁滑りしていたが、今フレームで壁滑りを抜けた。
        else if (wasWallSliding && !runtimeState.isWallSliding)
        {
            audioController.StopWallSlideSound();
        }
    }

    // ダッシュ開始時に、地上/空中で音声を分ける。
    // 判定ロジックは Vibration.cs 側と同じ。
    // wasDashing / isDashing は Vibration.cs 側で管理済み。
    private void UpdateDashSound()
    {
        // 「今フレームでダッシュ開始した瞬間」だけ鳴らしたい。
        if (wasDashing || !runtimeState.isDashing)
        {
            return;
        }

        // 壁滑りからダッシュに入るケースでは、ループ音を止める。
        audioController.StopWallSlideSound();

        // ダッシュ開始時点の接地状態で、地上用と空中用の音声を分ける。
        if (runtimeState.isGrounded)
        {
            audioController.PlayGroundDash();
        }
        else
        {
            audioController.PlayAirDash();
        }
    }

    // 着地音を再生する。
    // landingOccurredThisFrame は Vibration.cs の CaptureLandingSnapshot() で設定済み。
    private void UpdateLandingSound()
    {
        if (!landingOccurredThisFrame)
        {
            return;
        }

        audioController.PlayLanding(landingAirborneTime, landingFallHeight);
    }

    // 接地中に横移動している間、一定間隔で歩行音を通知する。
    private void UpdateWalkSound()
    {
        audioController.UpdateWalk(IsWalkSoundActive(), Time.fixedDeltaTime);
    }

    private bool IsWalkSoundActive()
    {
        if (!runtimeState.isGrounded || landingOccurredThisFrame)
        {
            return false;
        }

        if (runtimeState.isDashing
            || runtimeState.isWallGrabbing
            || runtimeState.isLedgeClimbing
            || IsExternallyControlled
            || IsActionLocked)
        {
            return false;
        }

        if (playerInputReader == null || rb == null)
        {
            return false;
        }

        if (Mathf.Abs(playerInputReader.Move.x) < WalkAudioInputThreshold)
        {
            return false;
        }

        return Mathf.Abs(rb.linearVelocity.x) > WalkAudioVelocityThreshold;
    }

    // 壁掴み中の上下移動、または崖乗り上げ中に一定間隔で登り音を通知する。
    private void UpdateClimbSound()
    {
        audioController.UpdateClimb(IsClimbSoundActive(), Time.fixedDeltaTime);
    }

    private bool IsClimbSoundActive()
    {
        if (runtimeState.isLedgeClimbing)
        {
            return true;
        }

        if (!runtimeState.isWallGrabbing || rb == null)
        {
            return false;
        }

        return Mathf.Abs(rb.linearVelocity.y) > ClimbAudioVelocityThreshold;
    }

    // 壁掴みに入った瞬間だけ掴み音を通知する。
    private void UpdateGrabSound()
    {
        if (!wasWallGrabbingForAudio && runtimeState.isWallGrabbing)
        {
            audioController.PlayGrab();
        }
    }

    // 壁キック成功地点から直接呼ぶ用。
    private void PlayWallKickSound()
    {
        if (audioController == null)
        {
            return;
        }

        // 壁滑りループ音が残っている可能性があるので止めてから、
        // 壁ジャンプの音声を再生する。
        audioController.StopWallSlideSound();
        audioController.PlayWallJump();
    }

    // 通常ジャンプ成功地点から直接呼ぶ用。
    private void PlayJumpSound()
    {
        if (audioController == null)
        {
            return;
        }

        audioController.PlayJump();
    }

    // 死亡開始時の音声を再生する。
    // 「いつ鳴らすか」は Controller 側で決め、実際の再生内容は PlayerAudioSettings 側へ委譲する。
    private void PlayDeathSound(PlayerDeathCause cause)
    {
        if (audioController == null)
        {
            return;
        }

        audioController.PlayDeath(cause);
    }

    // リスポーン完了時の音声を再生する。
    private void PlayRespawnSound()
    {
        if (audioController == null)
        {
            return;
        }

        audioController.PlayRespawn();
    }

    private void CacheAudioState()
    {
        wasWallGrabbingForAudio = runtimeState.isWallGrabbing;
    }
}
