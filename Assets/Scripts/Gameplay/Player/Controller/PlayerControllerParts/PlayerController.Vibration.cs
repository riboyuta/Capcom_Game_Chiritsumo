using UnityEngine;

public sealed partial class PlayerController
{
    [Header("振動: コントローラー参照")]
    [Tooltip("プレイヤー操作に応じたコントローラー振動を再生するコンポーネント参照です。未設定時は Awake 初期化時に同一 GameObject から取得を試みます。")]
    // 実際の振動再生を担当するコンポーネント。
    // この partial は「いつ振動イベントを送るか」を決める役割で、
    // 実際のモーター制御は PlayerVibrationController 側に任せる。
    [SerializeField] private PlayerVibrationController vibrationController;

    // 前フレームの状態保持。
    // 「今フレームで変化したか」を判定するために使う。
    private bool wasGrounded;
    private bool wasStepping;
    private bool wasWallSliding;

    // 空中に出てからの経過時間(秒)。
    // 着地時に PlayerVibrationController 側へ渡す。
    private float airborneTimer;

    // 空中中に記録した「足元 Y の最高値」。
    // 着地時に最高点からの落差を算出するために使う。
    private float highestAirborneFootY;
    // 既存 Awake の最後で呼ぶ。
    // 振動関連の比較用状態を初期化する。
    private void InitializeVibrationState()
    {
        // Inspector 未設定なら同一 GameObject から取得する。
        if (vibrationController == null)
        {
            vibrationController = GetComponent<PlayerVibrationController>();
        }

        // 初期比較用として、現在状態をそのまま保存しておく。
        wasGrounded = isGrounded;
        wasStepping = isStepping;
        wasWallSliding = isWallSliding;
        airborneTimer = 0f;
        highestAirborneFootY = GetCurrentFootY();
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
        UpdateStepVibration();
        UpdateAirborneMetrics();
        UpdateLandingVibration();

        // 最後に今回の状態を保存し、次フレームとの比較に使う。
        CacheVibrationState();
    }

    // 壁滑り開始/終了に応じて微振動を開始/停止する。
    private void UpdateWallSlideVibration()
    {
        // 前フレームでは壁滑りしておらず、今フレームで壁滑りに入った。
        if (!wasWallSliding && isWallSliding)
        {
            vibrationController.StartWallSlideRumble();
        }
        // 前フレームでは壁滑りしていたが、今フレームで壁滑りを抜けた。
        else if (wasWallSliding && !isWallSliding)
        {
            vibrationController.StopWallSlideRumble();
        }
    }

    // 前ステ開始時に、地上/空中で振動を分ける。
    private void UpdateStepVibration()
    {
        // 「今フレームで前ステ開始した瞬間」だけ振動させたい。
        // すでに前フレームから前ステ中なら鳴らさない。
        // 今フレームで前ステしていないなら当然鳴らさない。
        if (wasStepping || !isStepping)
        {
            return;
        }

        // 壁滑りから前ステに入るケースでは、
        // 微振動を止めてから単発振動へ切り替える。
        vibrationController.StopWallSlideRumble();

        // 前ステ開始時点の接地状態で、地上用と空中用の振動を分ける。
        if (isGrounded)
        {
            vibrationController.PlayGroundStep();
        }
        else
        {
            vibrationController.PlayAirStep();
        }
    }

    // 強着地のみ振動させる。
    // 空中時間と空中中の最高足元Yを更新する。
    private void UpdateAirborneMetrics()
    {
        // 接地中は計測しない。
        if (isGrounded)
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

    // 着地振動を再生する。
    private void UpdateLandingVibration()
    {
        // 「今フレームで着地した瞬間」だけ判定したい。
        // すでに前フレームから接地中なら着地イベントではない。
        // 今フレームで未接地なら着地していない。
        if (wasGrounded || !isGrounded)
        {
            return;
        }

        float landingFootY = GetCurrentFootY();
        float fallHeightFromApex = Mathf.Max(0f, highestAirborneFootY - landingFootY);
        vibrationController.PlayLanding(airborneTimer, fallHeightFromApex);
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

    // フレーム末尾で、次回比較用の状態を保存する。
    private void CacheVibrationState()
    {
        wasGrounded = isGrounded;
        wasStepping = isStepping;
        wasWallSliding = isWallSliding;
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