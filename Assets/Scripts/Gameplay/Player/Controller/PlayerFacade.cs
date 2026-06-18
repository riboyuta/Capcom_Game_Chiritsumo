using UnityEngine;
using static PlayerController;

#region 外部制御の公開型

// 外部制御が「どう移動を支配しているか」を表す。
// 注意:
// - これは停止強度ではなく、運動の種別
public enum ExternalControlMode
{
    // 外部制御なし。
    None,

    // 指定位置・指定姿勢に固定される。
    // 例:
    // - 大砲の中で中心位置に保持
    // - 会話演出で立ち位置固定
    Anchored,

    // 何かに追従する。
    // 例:
    // - 運搬ギミック
    // - 吸着移動
    Attached,

    // 経路に沿って動かされる。
    // 例:
    // - レール移動
    // - 搬送ライン
    PathDriven,

    // 外部から速度を与えられて飛ぶ。
    // 例:
    // - 大砲
    // - 発射台
    // - バネ
    Ballistic,

    // スクリプト主導で移動制御される。
    // 例:
    // - 特殊演出
    // - イベント移動
    ScriptDriven
}

// 外部制御中の物理方針。
public enum ExternalPhysicsPolicy
{
    // 通常の物理挙動を維持する。
    Keep,

    // 物理挙動を停止する。
    // 例:
    // - 大砲の中で完全固定
    Suspend,

    // 外部制御側の移動を優先する。
    // 例:
    // - レール移動
    // - 経路制御
    ExternalDriven
}

// 外部制御中の重力方針。
public enum ExternalGravityPolicy
{
    // 現在の重力設定を維持する。
    Keep,

    // 重力を強制的に有効にする。
    ForceOn,

    // 重力を強制的に無効にする。
    ForceOff
}

// 外部制御中の見た目方針。
public enum ExternalVisualPolicy
{
    // 現在の見た目を維持する。
    Keep,

    // 見た目を隠す。
    // 例:
    // - 大砲内部でプレイヤー表示を消す
    Hide
}

// 外部制御中の速度方針。
public enum ExternalVelocityPolicy
{
    // 現在の速度を維持する。
    Keep,

    // 水平方向速度だけゼロにする。
    ZeroHorizontal,

    // 速度を完全にゼロにする。
    ZeroAll
}

// 外部制御開始時の要求内容をまとめたもの。
// 重要:
// - TryBeginExternalControl は全部停止 API ではない
// - 何を止めるか、どう見せるかはこの request の policy で決める
public struct PlayerExternalControlRequest
{
    // 制御元。
    // 例:
    // - 大砲
    // - レール
    // - 会話イベント
    public Object Owner;

    // 外部制御の運動種別。
    public ExternalControlMode Mode;

    // どの入力を止めるか。
    // 例:
    // - レール中は Move / Dash を止めるが Jump は止めない
    public InputBlockFlags InputBlockFlags;

    // 物理の扱い。
    public ExternalPhysicsPolicy PhysicsPolicy;

    // 重力の扱い。
    public ExternalGravityPolicy GravityPolicy;

    // 見た目の扱い。
    public ExternalVisualPolicy VisualPolicy;

    // 速度の扱い。
    public ExternalVelocityPolicy VelocityPolicy;
}

// ワープ時の補助指定。
public struct WarpOptions
{
    // ワープ時に速度をゼロへ戻すか。
    public bool ClearVelocity;

    // ワープ後に向きを更新するか。
    public bool UpdateFacing;

    // UpdateFacing が true のときに使う向き。
    // 右向き = 1、左向き = -1 を想定。
    public int Facing;
}

// 外部ギミックからの入力非依存な固定射出要求。
// バネ床専用ではなく、発射台・ノックバック等にも使う汎用 request。
public struct PlayerFixedLaunchRequest
{
    // 要求元。デバッグや将来の優先度判定に使う。
    public Object Owner;

    // 射出方向。内部で正規化して使う。
    public Vector3 Direction;

    // 射出速度。0 未満は 0 として扱う。
    public float Speed;

    // 射出方向に直交する既存速度をどれだけ残すか（0〜1）。
    public float TangentVelocityKeepRate;

    // 射出直後に一時的にブロックする入力種別。
    public InputBlockFlags InputBlockFlags;

    // 入力ブロック継続秒数。
    public float InputBlockDuration;

    // 外部射出として保護する秒数。外部射出通知の継続に使う。
    public float LaunchProtectionDuration;

    // 射出時にダッシュ中ならキャンセルするか。
    public bool CancelDash;

    // 可変ジャンプカット抑制などのために外部射出通知を行うか。
    public bool NotifyExternalLaunch;

    // 固定射出中に適用する重力補正。不要な場合は Enabled=false にする。
    public PlayerFixedLaunchGravityModifier GravityModifier;

    // 射出時に接地・壁関連状態を解除するためのフック。
    public bool ForceUnground;
}

// 固定射出中に適用する重力補正情報。
public struct PlayerFixedLaunchGravityModifier
{
    // 重力補正を有効にするか。
    public bool Enabled;

    // 補正を適用する最大秒数。
    public float Duration;

    // 上昇中に使う重力倍率。
    public float AscendingMultiplier;

    // 落下中に使う重力倍率。
    public float FallingMultiplier;
}

#endregion

#region 外部制御セッション

// 外部制御セッションの実処理側が満たす内部契約。
// 実装担当は PlayerController 側、またはその補助クラス側を想定。
internal interface IPlayerExternalControlSessionBackend
{
    // 現在この session backend が有効か。
    bool IsValid { get; }

    // このフレームだけプレイヤーを指定位置・指定向きへ拘束する。
    void RequestAnchorPoseThisFrame(Vector3 position, Quaternion rotation);

    // このフレームだけ経路上の位置・向きを要求する。
    void RequestPathPoseThisFrame(Vector3 position, Quaternion rotation);

    // このフレームだけ向きを要求する。
    void RequestFacingThisFrame(int facing);

    // 射出を要求する。
    void RequestLaunch(Vector3 direction, float speed, float maxFlightDistance, LayerMask collisionLayers);

    // このフレームのジャンプ要求を消費する。
    bool ConsumeJumpRequestThisFrame();
    // 外部制御を終了する。
    void EndControl();
}

// 外部制御中の更新要求を送るための公開面。
// 注意:
// - PlayerFacade 本体に全部ぶら下げず、制御権を持つ session に閉じる
// - 無効 session に対する要求は何もしない
public sealed class PlayerExternalControlSession
{
    // 無効 session を表す共有インスタンス。
    private static readonly PlayerExternalControlSession invalidSession = new PlayerExternalControlSession(null);

    // 実処理側。
    private readonly IPlayerExternalControlSessionBackend backend;

    // 実装側からだけ生成させる。
    internal PlayerExternalControlSession(IPlayerExternalControlSessionBackend backend)
    {
        this.backend = backend;
    }

    // 無効 session を返す。
    public static PlayerExternalControlSession Invalid => invalidSession;

    // 現在この session が有効か。
    public bool IsValid => backend != null && backend.IsValid;

    // このフレームだけプレイヤーを指定位置・指定向きへ拘束する。
    // 用途例:
    // - 大砲内で中心位置に保持
    // - 会話演出で立ち位置固定
    public void RequestAnchorPoseThisFrame(Vector3 position, Quaternion rotation)
    {
        if (!IsValid) return;
        backend.RequestAnchorPoseThisFrame(position, rotation);
    }

    // このフレームだけ経路上の位置・向きを要求する。
    // 用途例:
    // - レール移動
    // - 搬送ライン
    public void RequestPathPoseThisFrame(Vector3 position, Quaternion rotation)
    {
        if (!IsValid) return;
        backend.RequestPathPoseThisFrame(position, rotation);
    }

    // このフレームだけ向きを要求する。
    // 用途例:
    // - レール移動中の見た目方向制御
    // - 拘束中の向き固定
    public void RequestFacingThisFrame(int facing)
    {
        if (!IsValid) return;
        backend.RequestFacingThisFrame(facing);
    }

    // 射出を要求する。
    // 用途例:
    // - 大砲
    // - 発射台
    // - 強制打ち上げギミック
    public void RequestLaunch(Vector3 direction, float speed, float maxFlightDistance, LayerMask collisionLayers)
    {
        if (!IsValid) return;
        backend.RequestLaunch(direction, speed, maxFlightDistance, collisionLayers);
    }

    // このフレームのジャンプ要求を消費する。
    public bool ConsumeJumpRequestThisFrame()
    {
        if (!IsValid) return false;
        return backend.ConsumeJumpRequestThisFrame();
    }

    // 外部制御を終了する。
    // 用途例:
    // - レールからジャンプで離脱
    // - 大砲処理完了
    // - owner 側 OnDisable での安全復帰
    public void EndControl()
    {
        if (!IsValid) return;
        backend.EndControl();
    }
}

#endregion

#region プレイヤー公開窓口

// PlayerController への公開窓口だけを担当する。
// ギミック・敵・イベントは PlayerController を直接触らず、原則この窓口だけを使う。
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerController))]
public sealed class PlayerFacade : MonoBehaviour
{
    // 実際のプレイヤー制御本体。
    private PlayerController playerController;
    private bool isDeathEventSubscribed;

    public event System.Action<PlayerDeathCause> DeathAccepted;

    // 必須コンポーネントを取得してキャッシュする。
    private void Awake()
    {
        ResolvePlayerControllerIfNeeded();
    }

    private void OnEnable()
    {
        SubscribeDeathAcceptedIfNeeded();
    }

    private void OnDisable()
    {
        UnsubscribeDeathAcceptedIfNeeded();
    }

    private void ResolvePlayerControllerIfNeeded()
    {
        if (playerController != null)
        {
            return;
        }

        playerController = GetComponent<PlayerController>();
    }

    private void SubscribeDeathAcceptedIfNeeded()
    {
        if (isDeathEventSubscribed)
        {
            return;
        }

        ResolvePlayerControllerIfNeeded();

        if (playerController == null)
        {
            return;
        }

        playerController.DeathAccepted += OnPlayerDeathAccepted;
        isDeathEventSubscribed = true;
    }

    private void UnsubscribeDeathAcceptedIfNeeded()
    {
        if (!isDeathEventSubscribed)
        {
            return;
        }

        if (playerController != null)
        {
            playerController.DeathAccepted -= OnPlayerDeathAccepted;
        }

        isDeathEventSubscribed = false;
    }

    private void OnPlayerDeathAccepted(PlayerDeathCause deathCause)
    {
        DeathAccepted?.Invoke(deathCause);
    }

    // 現在ダッシュ中か。
    // 用途例:
    // - ダッシュ中だけ壊せるブロック
    // - ダッシュ中だけ反応する敵やスイッチ
    public bool IsDashActive => playerController.IsDashActive;

    // このフレームにダッシュを開始したか。
    // 用途例:
    // - ダッシュ開始演出の one-shot 起点
    public bool JustDashStartedThisFrame => playerController.JustDashStartedThisFrameForFacade;

    // 現在のダッシュ方向。
    // 用途例:
    // - ダッシュ方向に応じた演出分岐
    public Vector2 DashDirection => playerController.DashDirectionForFacade;

    // 今この瞬間にダッシュ開始できるか。
    // 用途例:
    // - ダッシュ回復ギミックが「回復が必要か」を見る
    // - UI やチュートリアルで現在ダッシュ可能かを表示する
    public bool CanUseDashNow => playerController.CanUseDashNow();


    // 最後に有効なダッシュ入力が受理されたフレーム。
    // 用途例:
    // - 外部ギミックや敵AIが新しいダッシュ入力の受理を検知する
    // - 開始時のフレーム値と比較して、その後の入力受理を判定する
    public int LastAcceptedDashInputFrame => playerController.LastAcceptedDashInputFrame;

    // 壁掴み中か。
    // 用途例:
    // - 壁掴み中だけ反応する壁ギミック
    // - 壁掴み中のみ危険になるトラップ
    public bool IsWallGrabbing => playerController.IsWallGrabbing;

    // 接地中か。
    // 用途例:
    // - 地上スイッチ
    // - 地上時だけ起動する床ギミック
    public bool IsGrounded => playerController.IsGrounded;

    // 空中状態か。
    // 用途例:
    // - 空中時だけ反応するリングや風エリア
    // - 空中補助系ギミック
    public bool IsAirborne => playerController.IsAirborne;

    // 向き。
    // 右向き = 1、左向き = -1 を想定。
    // 用途例:
    // - プレイヤーが向いている方向へ射出するギミック
    // - 向きに応じて配置や演出方向を変える
    public int Facing => playerController.Facing;

    // 現在の移動入力方向。
    // 用途例:
    // - 8方向入力チュートリアル
    // - 入力方向に応じて挙動を変えるギミック
    public Vector2 MoveInputDirection => playerController.MoveInputDirection;

    // 現在の移動入力が斜めか。
    // 用途例:
    // - 斜め入力の可視化
    // - 斜め入力時のみ反応するギミック
    public bool IsMoveInputDiagonal
    {
        get { return playerController.IsMoveInputDiagonal; }
    }

    // 下入力を保持しているか。
    // 用途例:
    // - すり抜け床の落下判定
    // - 下入力長押しを使うギミック条件判定
    public bool IsDownInputHeld => playerController.DownInputHeldForFacade;

    // 現在速度ベクトル。
    // 用途例:
    // - 速度依存のギミック判定
    // - デバッグ表示やログ出力
    public Vector3 CurrentVelocity => playerController.CurrentVelocityForFacade;

    // 現在速度スカラー。
    // 用途例:
    // - 一定速度以上で反応するオブジェクト判定
    // - 速度条件の簡易チェック
    public float CurrentSpeed => playerController.CurrentSpeedForFacade;

    // 現在、外部制御中か。
    // 用途例:
    // - すでに別ギミックに拘束されているかの確認
    // - 二重搭乗や二重拘束の防止
    public bool IsExternallyControlled => playerController.IsExternallyControlled;

    // 現在の外部制御モード。
    // 用途例:
    // - いま固定中なのか、レール移動中なのか、射出中なのかの診断
    // 注意:
    // - これは「何を止めているか」ではなく、「どう移動を外部が支配しているか」を表す
    public ExternalControlMode CurrentExternalControlMode => playerController.CurrentExternalControlMode;

    // ダッシュ回復を試みる。
    // 用途例:
    // - ダッシュ回復クリスタル
    // - 特定ギミックに触れた瞬間のダッシュ回復
    // 注意:
    // - 残数や内部 state を直接触らず、この窓口経由で回復させる
    public bool TryRefillDash(DashRefillReason reason)
    {
        return playerController.TryRefillDash(reason);
    }

    // このフレームだけ入力ブロックを要求する。
    // 用途例:
    // - 会話中は Move / Dash を禁止
    // - レール中は左右移動だけ禁止し、Jump は許可する
    // 注意:
    // - 毎フレーム要求型。禁止したい間は毎フレーム呼ぶ
    // - 呼ばなくなれば自動解除される
    public void RequestInputBlockThisFrame(InputBlockFlags flags)
    {
        playerController.RequestInputBlockThisFrame(flags);
    }

    // 死亡を要求する。
    // 用途例:
    // - トゲ
    // - 即死レーザー
    // - 条件未達時の危険ギミック
    public void RequestKill(Vector3 damageDirection)
    {
        playerController.RequestKill(damageDirection);
    }

    // 外部打ち上げが発生したことを通知する。
    // 用途例:
    // - バネ床
    // - 打ち上げパッド
    // - 外部から上方向速度を与えた直後の内部整合
    // 注意:
    // - 速度そのものを変える窓口ではなく、「外部 launch が起きた」という通知
    public void NotifyExternalLaunch()
    {
        playerController.NotifyExternalLaunch();
    }

    // この physics tick の移動補正を要求する。
    // 用途例:
    // - 風エリアで重力や移動速度を補正
    // - 泥床で地上移動を鈍くする
    // - 低重力エリアで gravity を弱める
    // 注意:
    // - PlayerMovementSettings を直接書き換えず、補正 request として送る
    // - 複数ギミックが同時に存在しても、Player 側で最終値を解決する
    public void RequestLocomotionModifierThisTick(PlayerLocomotionModifierRequest request)
    {
        playerController.RequestLocomotionModifierThisTick(request);
    }

    // 指定した外部制御要求を、今この瞬間に受け入れ可能かを判定する。
    // 用途例:
    // - 大砲がプレイヤーを格納してよいか確認する
    // - レールが搭乗開始してよいか確認する
    // 注意:
    // - 最終的な受理責任は Player 側に持たせる
    // - ギミック側は「たぶんいける」と決め打ちしない
    public bool CanAcceptExternalControl(in PlayerExternalControlRequest request)
    {
        return playerController.CanAcceptExternalControl(request);
    }

    // 外部制御を開始し、成功時は制御セッションを返す。
    // 用途例:
    // - 大砲搭乗開始
    // - レール搭乗開始
    // - 会話やイベントによる位置拘束開始
    // 重要:
    // - これは「全部停止する API」ではない
    // - 何を止めるかは request の policy で決める
    // - 制御中の更新は返された session 側を使う
    public bool TryBeginExternalControl(
        in PlayerExternalControlRequest request,
        out PlayerExternalControlSession session)
    {
        return playerController.TryBeginExternalControl(request, out session);
    }

    // プレイヤーを指定位置へワープさせることを要求する。
    // 用途例:
    // - ワープ床
    // - ポータル
    // - チェックポイント復帰
    // 注意:
    // - 壁埋まり対策や速度維持有無は WarpOptions 側で指定する
    public void RequestWarp(Vector3 targetPosition, WarpOptions options = default)
    {
        playerController.RequestWarp(targetPosition, options);
    }

    // プレイヤーの向き変更を要求する。
    // 用途例:
    // - 会話開始時に相手方向を向かせる
    // - 搭乗演出直前に向きを揃える
    // - ワープ後に向きを補正する
    // 注意:
    // - これは単発要求
    // - 制御中ずっと向きを固定したい場合は session 側の要求を使う
    public void RequestFacing(int facing)
    {
        playerController.RequestFacing(facing);
    }
    // ──────────────────────────────────────────────
    // 固定射出（外部からの入力非依存 launch 用）
    // ──────────────────────────────────────────────

    // 固定射出中かどうか。
    private bool isFixedLaunch;

    // 固定射出方向。
    private Vector3 fixedLaunchDirection;

    // 固定射出中の入力ブロック残り時間。
    private float fixedLaunchInputBlockTimer;

    // 固定射出保護の残り時間。
    private float fixedLaunchProtectionTimer;

    // 固定射出中に毎 tick ブロックする入力種別。
    private InputBlockFlags fixedLaunchInputBlockFlags;

    // 固定射出中に外部射出通知を継続するか。
    private bool fixedLaunchNotifyExternalLaunch;

    // 固定射出中の重力補正を使うか。
    private bool fixedLaunchUseGravityModifier;

    // 固定射出中の重力補正残り時間。
    private float fixedLaunchGravityModifierTimer;

    // 固定射出中の上昇時重力倍率。
    private float fixedLaunchAscendingGravityMultiplier;

    // 固定射出中の落下時重力倍率。
    private float fixedLaunchFallingGravityMultiplier;

    // 外部から固定射出を試みる。
    public bool TryApplyFixedLaunch(in PlayerFixedLaunchRequest request)
    {
        if (playerController == null)
        {
            return false;
        }

        if (!playerController.CanAcceptFixedLaunch(in request))
        {
            return false;
        }

        ApplyFixedLaunchInternal(in request);
        return true;
    }

    // 外部から固定射出を適用する。
    // 既存呼び出し互換用。
    public void ApplyFixedLaunch(in PlayerFixedLaunchRequest request)
    {
        TryApplyFixedLaunch(in request);
    }

    private void ApplyFixedLaunchInternal(in PlayerFixedLaunchRequest request)
    {
        Rigidbody rb = playerController.Rigidbody;
        if (rb == null) return;
        if (request.Direction.sqrMagnitude <= Mathf.Epsilon) return;

        Vector3 direction = request.Direction.normalized;
        float speed = Mathf.Max(0f, request.Speed);
        float tangentKeepRate = Mathf.Clamp01(request.TangentVelocityKeepRate);

        // ステップ（ダッシュ）中に外部射出へ入る場合、外部射出を優先する。
        if (request.CancelDash && playerController.IsDashActive && playerController.RuntimeState != null)
        {
            playerController.RuntimeState.isDashing = false;
        }

        Vector3 currentVelocity = rb.linearVelocity;
        float currentDirectionSpeed = Vector3.Dot(currentVelocity, direction);
        Vector3 directionVelocity = direction * currentDirectionSpeed;
        Vector3 tangentVelocity = currentVelocity - directionVelocity;
        rb.linearVelocity = direction * speed + tangentVelocity * tangentKeepRate;

        // TODO: ForceUnground を使った接地・壁状態解除は、安全な既存窓口を確認した上で Phase 2 以降で実装する。
        _ = request.ForceUnground;

        if (request.NotifyExternalLaunch)
        {
            playerController.NotifyExternalLaunch();
        }

        isFixedLaunch = true;
        fixedLaunchDirection = direction;
        fixedLaunchInputBlockFlags = request.InputBlockFlags;
        fixedLaunchInputBlockTimer = Mathf.Max(0f, request.InputBlockDuration);
        fixedLaunchProtectionTimer = Mathf.Max(0f, request.LaunchProtectionDuration);
        fixedLaunchNotifyExternalLaunch = request.NotifyExternalLaunch;

        PlayerFixedLaunchGravityModifier gravityModifier = request.GravityModifier;
        float gravityModifierDuration = Mathf.Max(0f, gravityModifier.Duration);
        fixedLaunchUseGravityModifier = gravityModifier.Enabled && gravityModifierDuration > 0f;
        fixedLaunchGravityModifierTimer = fixedLaunchUseGravityModifier ? gravityModifierDuration : 0f;
        fixedLaunchAscendingGravityMultiplier = Mathf.Max(0.1f, gravityModifier.AscendingMultiplier);
        fixedLaunchFallingGravityMultiplier = Mathf.Max(0.1f, gravityModifier.FallingMultiplier);
    }

    // 外部から固定ジャンプを適用する。
    // 用途例:
    // - バネ床
    // - 打ち上げパッド
    // - ジャンプボタンの押下状態に関係なく、常に一定高さだけ跳ねさせたいギミック
    // 注意:
    // - 速度設定と可変ジャンプカット抑制を一括で行う
    // - 呼び出し元で Rigidbody の速度を個別に触る必要はない
    // - inputLockTime を指定すると、その時間は横移動入力をブロックし強制移動させる
    public void ApplyFixedJump(Vector3 velocity, float inputLockTime = 0f)
    {
        float speed = velocity.magnitude;
        if (speed <= Mathf.Epsilon) return;

        PlayerFixedLaunchRequest request = new PlayerFixedLaunchRequest
        {
            Owner = this,
            Direction = velocity / speed,
            Speed = speed,
            TangentVelocityKeepRate = 0f,
            InputBlockFlags = PlayerController.InputBlockFlags.Move,
            InputBlockDuration = inputLockTime,
            LaunchProtectionDuration = Mathf.Max(inputLockTime, 0.6f),
            CancelDash = true,
            NotifyExternalLaunch = true,
            GravityModifier = new PlayerFixedLaunchGravityModifier
            {
                Enabled = false,
                Duration = 0f,
                AscendingMultiplier = 1f,
                FallingMultiplier = 1f
            },
            ForceUnground = true
        };

        TryApplyFixedLaunch(in request);
    }

    private void FixedUpdate()
    {
        UpdateFixedLaunch();
    }

    private void UpdateFixedLaunch()
    {
        if (!isFixedLaunch) return;

        Rigidbody rb = playerController.Rigidbody;

        // ダッシュ（ステップ）入力でいつでも固定状態をキャンセル可能
        if (rb == null || playerController.IsDashActive)
        {
            ClearFixedLaunchState();
            return;
        }

        if (fixedLaunchUseGravityModifier && fixedLaunchGravityModifierTimer > 0f)
        {
            float selectedMultiplier = rb.linearVelocity.y < 0f
                ? fixedLaunchFallingGravityMultiplier
                : fixedLaunchAscendingGravityMultiplier;

            PlayerLocomotionModifierRequest modifier = PlayerLocomotionModifierRequest.Identity;
            modifier.gravityScaleMultiplier = selectedMultiplier;
            playerController.RequestLocomotionModifierThisTick(modifier);

            fixedLaunchGravityModifierTimer -= Time.fixedDeltaTime;
            if (fixedLaunchGravityModifierTimer <= 0f)
            {
                fixedLaunchUseGravityModifier = false;
                fixedLaunchGravityModifierTimer = 0f;
            }
        }

        if (fixedLaunchInputBlockTimer > 0f)
        {
            fixedLaunchInputBlockTimer -= Time.fixedDeltaTime;
            RequestInputBlockThisFrame(fixedLaunchInputBlockFlags);
        }

        if (fixedLaunchProtectionTimer > 0f)
        {
            fixedLaunchProtectionTimer -= Time.fixedDeltaTime;
            float directionSpeed = Vector3.Dot(rb.linearVelocity, fixedLaunchDirection);
            _ = directionSpeed;

            if (fixedLaunchNotifyExternalLaunch)
            {
                playerController.NotifyExternalLaunch();
            }
        }

        if (fixedLaunchInputBlockTimer <= 0f && fixedLaunchProtectionTimer <= 0f)
        {
            ClearFixedLaunchState();
        }
    }

    private void ClearFixedLaunchState()
    {
        isFixedLaunch = false;
        fixedLaunchUseGravityModifier = false;
        fixedLaunchGravityModifierTimer = 0f;
    }

    // ハザード由来の死亡を要求する。
    // 用途例:
    // - トゲ
    // - 落下穴
    public bool RequestHazardDeath()
    {
        return playerController.RequestHazardDeath();
    }

    // 被ダメージ由来の死亡を要求する。
    // 用途例:
    // - 敵攻撃
    // - ダメージ床
    public bool RequestDamageDeath()
    {
        return playerController.RequestDamageDeath();
    }
}



#endregion
