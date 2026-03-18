using UnityEngine;

// 敵の攻撃の基底クラス
// すべての敵の攻撃パターンはこのクラスを継承して実装する
public abstract class EnemyAttackBase : MonoBehaviour
{
    // 攻撃の状態を表す列挙型
    public enum AttackState
    {
        Idle,      // 待機状態
        WindUp,    // 予備動作中
        Active,    // 攻撃実行中
        Recover    // 硬直状態
    }

    [Header("Base Attack Settings")]
    [Header("攻撃の名前")]
    [SerializeField] protected string attack_name = "Attack";  // 攻撃の名前
    [Header("攻撃の優先度")]
    [SerializeField] protected int priority = 0;                 // 攻撃の優先度（高いほど優先される）
    [Header("攻撃のクールダウン")]
    [SerializeField] protected float cooldown = 1.0f;            // 攻撃のクールダウン時間（秒）

    [Header("Debug")]
    [Header("デバッグログの表示フ")]
    [SerializeField] protected bool show_debug_log = false;      // デバッグログの表示フラグ

    protected bool is_running = false;                           // 攻撃実行中かどうか
    protected float last_attack_time = -999.0f;                  // 最後に攻撃を実行した時刻
    protected AttackState attack_state = AttackState.Idle;       // 現在の攻撃状態

    // プロパティ：外部からアクセス可能な読み取り専用情報
    public string AttackName => attack_name;       // 攻撃名
    public int Priority => priority;               // 優先度
    public bool IsRunning => is_running;           // 実行中フラグ
    public AttackState State => attack_state;      // 現在の状態

    // 初期化処理
    // 攻撃ビジュアルを非表示にして初期化
    protected virtual void Awake()
    {
        SetAttackVisualVisible(false);
        OnInitializeAttackVisual();
    }
 
    // 攻撃を開始できるかどうかを判定
    public bool CanStart(EnemyContext context)
    {
        // 既に実行中の場合は開始できない
        if (is_running)
        {
            return false;
        }

        // クールダウン中は開始できない
        if (Time.time < last_attack_time + cooldown)
        {
            return false;
        }

        // 派生クラスの条件をチェック
        return CheckCanStart(context);
    }

    // 攻撃を開始する
    public void StartAttack(EnemyContext context)
    {
        is_running = true;
        attack_state = AttackState.WindUp;  // 予備動作状態へ

        SetAttackVisualVisible(true);         // ビジュアルを表示
        OnStartAttack(context);

        LogDebug("Start");
    }

    // 攻撃の更新処理（毎フレーム呼ばれる）
    public void TickAttack(EnemyContext context)
    {
        if (!is_running)
        {
            return;
        }

        OnTickAttack(context);
    }

    // 攻撃が終了したかどうかを判定
    public bool IsFinished()
    {
        if (!is_running)
        {
            return true;
        }

        return CheckIsFinished();
    }

    // 攻撃を終了する（正常終了）
    public void FinishAttack(EnemyContext context)
    {
        if (!is_running)
        {
            return;
        }

        is_running = false;
        last_attack_time = Time.time;  // 次のクールダウン計算用に記録
        attack_state = AttackState.Idle;

        OnFinishAttack(context);
        SetAttackVisualVisible(false);   // ビジュアルを非表示

        LogDebug("Finish");
    }

    // 攻撃をキャンセルする（強制中断）
    public virtual void CancelAttack(EnemyContext context)
    {
        is_running = false;
        last_attack_time = Time.time;
        attack_state = AttackState.Idle;

        OnCancelAttack(context);
        SetAttackVisualVisible(false);   // ビジュアルを非表示

        LogDebug("Cancel");
    }

    // 攻撃状態を変更する（派生クラスから呼び出す）
    protected void SetAttackState(AttackState next_state)
    {
        attack_state = next_state;
    }

    // デバッグログを出力（m_show_debug_logがtrueの場合のみ）
        protected void LogDebug(string message)
    {
        if (!show_debug_log)
        {
            return;
        }

        Debug.Log($"[EnemyAttackBase] {name} ({attack_name}) : {message}");
    }

    // 攻撃ビジュアルの初期化処理
    // 必要なら派生クラスでオーバーライドして使用
    protected virtual void OnInitializeAttackVisual()
    {
    }

    // 攻撃ビジュアルの表示/非表示を切り替える
    // 必要な攻撃だけ派生クラスでオーバーライドする
    protected virtual void SetAttackVisualVisible(bool visible)
    {
    }

    // 派生クラスで実装する必要がある抽象メソッド
    protected abstract bool CheckCanStart(EnemyContext context);    // 攻撃開始条件のチェック
    protected abstract void OnStartAttack(EnemyContext context);     // 攻撃開始時の処理
    protected abstract void OnTickAttack(EnemyContext context);      // 攻撃の更新処理
    protected abstract bool CheckIsFinished();                       // 攻撃終了判定
    protected abstract void OnFinishAttack(EnemyContext context);    // 攻撃終了時の処理

    // 攻撃キャンセル時の処理
    // 派生クラスで必要に応じてオーバーライド
    protected virtual void OnCancelAttack(EnemyContext context)
    {
    }
}