using UnityEngine;

public sealed partial class PlayerController
{
    // 実際の音声再生を担当するコンポーネント。
    // この partial は「いつ音を鳴らすか」を決める役割で、
    // 実際の AudioManager 呼び出しは PlayerAudioSettings 側に任せる。
    private PlayerAudioSettings audioController;

    // PlayerAudioController.Awake() から呼ばれて自身を登録する。
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
        UpdateDashSound();
        UpdateLandingSound();
    }

    // 壁滑り開始/終了に応じてループ音を開始/停止する。
    // 判定ロジックは Vibration.cs 側と同じ前フレーム比較を使う。
    // wasWallSliding / isWallSliding は Vibration.cs 側で管理済み。
    private void UpdateWallSlideSound()
    {
        // 前フレームでは壁滑りしておらず、今フレームで壁滑りに入った。
        if (!wasWallSliding && isWallSliding)
        {
            audioController.StartWallSlideSound();
        }
        // 前フレームでは壁滑りしていたが、今フレームで壁滑りを抜けた。
        else if (wasWallSliding && !isWallSliding)
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
        if (wasDashing || !isDashing)
        {
            return;
        }

        // 壁滑りからダッシュに入るケースでは、ループ音を止める。
        audioController.StopWallSlideSound();

        // ダッシュ開始時点の接地状態で、地上用と空中用の音声を分ける。
        if (isGrounded)
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
    // 「いつ鳴らすか」は Controller 側で決め、実際の再生内容は AudioController 側へ委譲する。
    private void PlayDeathSound(DeathCause cause)
    {
        if (audioController == null)
        {
            return;
        }

        audioController.PlayDeath(cause);
    }
}
