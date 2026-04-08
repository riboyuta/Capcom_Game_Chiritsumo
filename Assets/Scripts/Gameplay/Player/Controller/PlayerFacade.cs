using UnityEngine;
using static PlayerController;

// PlayerController への公開窓口だけを担当する。
// ギミック・敵・イベントは PlayerController を直接触らず、原則この窓口だけを使う。
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerController))]
public sealed class PlayerFacade : MonoBehaviour
{
    // 実際のプレイヤー制御本体
    private PlayerController playerController;

    // 必須コンポーネントを取得してキャッシュする
    private void Awake()
    {
        playerController = GetComponent<PlayerController>();
    }

    // 現在ダッシュ中か。
    // 用途例:
    // - セレステ風の「ダッシュ中に触れると壊れるブロック」の判定
    // - ダッシュ中のみ反応するスイッチや敵の弱点判定
    public bool IsDashActive => playerController.IsDashActive;

    // 今この瞬間にダッシュ開始できるか。
    // 主用途:
    // - 「今ダッシュ不可か」を見るための窓口
    // - 特に残気不足、入力禁止、クールタイム、行動不能などで
    //   ダッシュが使えない状態かどうかを判定する
    // 用途例:
    // - ダッシュ回復ギミックが「いま回復が必要か」を見る
    // - チュートリアルやUIで「現在ダッシュ可能か」を表示する
    // 注意:
    // - 残気だけでなく、各種禁止条件を含めた最終結果を返す
    public bool CanUseDashNow => playerController.CanUseDashNow;

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
    // - 空中時だけ反応するバブル、リング、風エリア
    // - 空中でのみ有効な補助ギミック
    public bool IsAirborne => playerController.IsAirborne;

    // 向き。
    // 右向き = 1、左向き = -1 を想定。
    // 用途例:
    // - プレイヤーが向いている方向へ射出するギミック
    // - 向きに応じてエフェクトや配置方向を変える
    public int Facing => playerController.Facing;

    // 現在の移動入力方向。
    // 用途例:
    // - セレステ風 8 方向入力チュートリアル
    // - 入力方向に応じてギミックの反応を変える
    public Vector2 MoveInputDirection => playerController.MoveInputDirection;

    // 現在の移動入力が斜めか。
    // 用途例:
    // - 「斜め入力できているか」をUIやデバッグ表示で伝える
    // - 斜め入力時のみ反応するギミックの判定
    public bool IsMoveInputDiagonal => playerController.IsMoveInputDiagonal;

    // ダッシュ回復を試みる。
    // 用途例:
    // - セレステ風ダッシュ回復クリスタル
    // - 特定ギミックに触れた瞬間のダッシュ回復
    // 注意:
    // - 残数や内部 state を直接触らず、この窓口経由で回復させる
    public bool TryRefillDash(DashRefillReason reason)
    {
        return playerController.TryRefillDash(reason);
    }

    // このフレームだけ入力ブロックを要求する。
    // 用途例:
    // - レール中は Move / Dash を禁止
    // - 大砲搭乗中は Move / Jump / Dash / Grab を禁止
    // - 会話中や演出中だけ入力を制限
    // 重要:
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
    // - ダッシュしていないと突破できない障害物
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
    // - セレステ風のバネ床や打ち上げパッド
    // - 外部から上方向速度を与えた直後に、通常ジャンプ由来の処理と競合しないようにする
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
    // - 氷床で加速感を変える
    // - 低重力エリアで gravity を弱める
    // 注意:
    // - PlayerMovementSettings を直接書き換えず、補正 request として送る
    // - 複数ギミックが同時に存在しても、Player 側で最終値を解決する
    public void RequestLocomotionModifierThisTick(PlayerLocomotionModifierRequest request)
    {
        playerController.RequestLocomotionModifierThisTick(request);
    }
}