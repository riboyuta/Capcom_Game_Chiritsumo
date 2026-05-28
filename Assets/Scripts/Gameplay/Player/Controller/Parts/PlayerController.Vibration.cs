using UnityEngine;

public sealed partial class PlayerController
{
    // 実際の振動再生を担当するコンポーネント。
    // この partial は「いつ振動イベントを送るか」を決める役割で、
    // 実際のモーター制御は PlayerVibrationController 側に任せる。
    private PlayerVibrationController vibrationController;

    // 前フレームの状態保持。
    // 「今フレームで変化したか」を判定するために使う。
    private bool wasGrounded;
    private bool wasDashing;
    private bool wasWallSliding;

    // 空中に出てからの経過時間(秒)。
    // 着地時に PlayerVibrationController 側へ渡す。
    private float airborneTimer;

    // 空中中に記録した「足元 Y の最高値」。
    // 着地時に最高点からの落差を算出するために使う。
    private float highestAirborneFootY;

    // 接地→ジャンプが同一 FixedUpdate で起きても、
    // 着地イベントを失わないためのスナップショット。
    private bool landingOccurredThisFrame;
    private float landingAirborneTime;
    private float landingFallHeight;

    // 既存 Awake の最後で呼ぶ。
    // 振動関連の比較用状態を初期化する。
    private void InitializeVibrationState()
    {
        // 初期比較用として、現在状態をそのまま保存しておく。
        wasGrounded = runtimeState.isGrounded;
        wasDashing = runtimeState.isDashing;
        wasWallSliding = runtimeState.isWallSliding;
        airborneTimer = 0f;
        highestAirborneFootY = GetCurrentFootY();
        landingOccurredThisFrame = false;
        landingAirborneTime = 0f;
        landingFallHeight = 0f;
    }

    // 既存 FixedUpdate の最後の方で呼ぶ。
    // そのフレームの移動状態が確定したあとに、振動イベントを通知する。
    private void UpdateVibrationEvents()
    {
        // 振動コンポーネントが無ければ何もしない。
        if (vibrationController == null)
        {
            return;
        }

        // 状態変化に応じて各種振動イベントを判定する。
        UpdateWallSlideVibration();
        UpdateDashVibration();
        UpdateAirborneMetrics();
        UpdateLandingVibration();

        // 最後に今回の状態を保存し、次フレームとの比較に使う。
        CacheVibrationState();
    }

    // 壁滑り開始/終了に応じて微振動を開始/停止する。
    private void UpdateWallSlideVibration()
    {
        // 前フレームでは壁滑りしておらず、今フレームで壁滑りに入った。
        if (!wasWallSliding && runtimeState.isWallSliding)
        {
            vibrationController.StartWallSlideRumble();
        }
        // 前フレームでは壁滑りしていたが、今フレームで壁滑りを抜けた。
        else if (wasWallSliding && !runtimeState.isWallSliding)
        {
            vibrationController.StopWallSlideRumble();
        }
    }

    // ダッシュ開始時に、地上/空中で振動を分ける。
    private void UpdateDashVibration()
    {
        // 「今フレームでダッシュ開始した瞬間」だけ振動させたい。
        // すでに前フレームからダッシュ中なら鳴らさない。
        // 今フレームでダッシュしていないなら当然鳴らさない。
        if (wasDashing || !runtimeState.isDashing)
        {
            return;
        }

        // 壁滑りからダッシュに入るケースでは、
        // 微振動を止めてから単発振動へ切り替える。
        vibrationController.StopWallSlideRumble();

        // ダッシュ開始時点の接地状態で、地上用と空中用の振動を分ける。
        if (runtimeState.isGrounded)
        {
            vibrationController.PlayGroundDash();
        }
        else
        {
            vibrationController.PlayAirDash();
        }
    }

    // 強着地のみ振動させる。
    // 空中時間と空中中の最高足元Yを更新する。
    private void UpdateAirborneMetrics()
    {
        // 接地中は計測しない。
        if (runtimeState.isGrounded)
        {
            return;
        }

        float currentFootY = GetCurrentFootY();

        // 地上→空中へ遷移した瞬間に初期化する。
        if (wasGrounded)
        {
            airborneTimer = 0f;
            highestAirborneFootY = currentFootY;
        }

        airborneTimer += Time.fixedDeltaTime;
        if (currentFootY > highestAirborneFootY)
        {
            highestAirborneFootY = currentFootY;
        }
    }

    // 接地判定直後(ApplyJump 前)の着地情報を保存する。
    // 同フレームで isGrounded が false に戻っても、着地イベントを維持できる。
    private void CaptureLandingSnapshot()
    {
        landingOccurredThisFrame = !wasGrounded && runtimeState.isGrounded;
        justLandedThisFrame = landingOccurredThisFrame;
        if (!landingOccurredThisFrame)
        {
            return;
        }

        float landingFootY = GetCurrentFootY();
        landingAirborneTime = airborneTimer;
        landingFallHeight = Mathf.Max(0f, highestAirborneFootY - landingFootY);
    }

    // 着地振動を再生する。
    private void UpdateLandingVibration()
    {
        // 接地最終状態ではなく、ApplyJump 前に保存したスナップショットで判定する。
        if (!landingOccurredThisFrame)
        {
            return;
        }

        vibrationController.PlayLanding(landingAirborneTime, landingFallHeight);
    }

    // 壁キック成功地点から直接呼ぶ用。
    private void PlayWallKickVibration()
    {
        // 振動コンポーネントが無ければ何もしない。
        if (vibrationController == null)
        {
            return;
        }

        // 壁滑り微振動が残っている可能性があるので止めてから、
        // 壁キックの単発振動を再生する。
        vibrationController.StopWallSlideRumble();
        vibrationController.PlayWallKick();
    }
    // 死亡開始時の振動を再生する。
    // 「いつ鳴らすか」は Controller 側で決め、実際の再生内容は VibrationController 側へ委譲する。
    private void PlayDeathVibration(PlayerDeathCause cause)
    {
        if (vibrationController == null)
        {
            return;
        }

        vibrationController.PlayDeath(cause);
    }
    internal void SetVibrationController(PlayerVibrationController controller)
    {
        vibrationController = controller;
    }

    // フレーム末尾で、次回比較用の状態を保存する。
    private void CacheVibrationState()
    {
        wasGrounded = runtimeState.isGrounded;
        wasDashing = runtimeState.isDashing;
        wasWallSliding = runtimeState.isWallSliding;
    }

    // 現在フレーム時点の足元Yを取得する。
    private float GetCurrentFootY()
    {
        if (capsuleCollider == null)
        {
            return transform.position.y;
        }

        return capsuleCollider.bounds.min.y;
    }
}