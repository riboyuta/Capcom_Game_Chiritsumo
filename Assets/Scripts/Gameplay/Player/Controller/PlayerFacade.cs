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

    // 必須コンポーネントを取得してキャッシュする。
    private void Awake()
    {
        playerController = GetComponent<PlayerController>();
    }

    // 現在ダッシュ中か。
    // 用途例:
    // - ダッシュ中だけ壊せるブロック
    // - ダッシュ中だけ反応する敵やスイッチ
    public bool IsDashActive => playerController.IsDashActive;

    // 今この瞬間にダッシュ開始できるか。
    // 用途例:
    // - ダッシュ回復ギミックが「回復が必要か」を見る
    // - UI やチュートリアルで現在ダッシュ可能かを表示する
    public bool CanUseDashNow => playerController.CanUseDashNow();
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
    public Vector2 MoveInputDirection => MoveInputDirection;
    // 現在の移動入力が斜めか。
    // 用途例:
    // - 斜め入力の可視化
    // - 斜め入力時のみ反応するギミック
    public bool IsMoveInputDiagonal
    {
        get { return playerController.IsMoveInputDiagonal; }
    }
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

    // ノックバックを要求する。
    // 用途例:
    // - 敵接触時の弾き飛ばし
    // - 爆風
    // - 強風トラップや衝突ギミック
    public void RequestKnockback(Vector3 force)
    {
        playerController.RequestKnockback(force);
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

}

#endregion