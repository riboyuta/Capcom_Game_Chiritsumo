using UnityEngine;

public sealed partial class PlayerController
{
    [Header("振動: コントローラー参照")]
    [Tooltip("プレイヤー操作に応じたコントローラー振動を再生するコンポーネント参照です。未設定時は Awake 初期化時に同一 GameObject から取得を試みます。")]
    // 実際の振動再生を担当するコンポーネント。
    // この partial は「いつ振動イベントを送るか」を決める役割で、
    // 実際のモーター制御は PlayerVibrationController 側に任せる。
    [SerializeField] private PlayerVibrationController vibrationController;

    [Header("振動: 強着地判定")]
    [Tooltip("強着地振動を発生させる最小落下速度のしきい値です。着地直前の Y 速度がこの値以上に下向きなら強着地として扱います。")]
    // 強着地と見なすための最小落下速度。
    // previousVerticalVelocity が -10 以下なら「十分強く落下してきた」と判定する。
    [SerializeField, Min(0f)] private float strongLandingMinFallSpeed = 10f;

    // 前フレームの状態保持。
    // 「今フレームで変化したか」を判定するために使う。
    private bool wasGrounded;
    private bool wasStepping;
    private bool wasWallSliding;

    // 前フレーム終了時点の Y 速度。
    // 着地した瞬間に「落ちてきた勢い」がどれくらいだったかを見るために使う。
    private float previousVerticalVelocity;

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
        previousVerticalVelocity = rb.linearVelocity.y;
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
    private void UpdateLandingVibration()
    {
        // 「今フレームで着地した瞬間」だけ判定したい。
        // すでに前フレームから接地中なら着地イベントではない。
        // 今フレームで未接地なら着地していない。
        if (wasGrounded || !isGrounded)
        {
            return;
        }

        // 着地直前の落下速度がしきい値以上なら強着地と見なす。
        // Y 下方向は負値なので、-strongLandingMinFallSpeed 以下を見る。
        if (previousVerticalVelocity <= -strongLandingMinFallSpeed)
        {
            vibrationController.PlayStrongLanding();
        }
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

        // 次フレームで着地判定に使えるよう、現在の Y 速度を保存する。
        previousVerticalVelocity = rb.linearVelocity.y;
    }
}