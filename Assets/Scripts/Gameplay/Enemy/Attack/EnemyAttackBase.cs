using UnityEngine;

/// <summary>
/// 敵の攻撃の基底クラス
/// すべての敵の攻撃パターンはこのクラスを継承して実装する
/// </summary>
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
    [SerializeField] protected string m_attack_name = "Attack";  // 攻撃の名前
    [SerializeField] protected int m_priority = 0;                 // 攻撃の優先度（高いほど優先される）
    [SerializeField] protected float m_cooldown = 1.0f;            // 攻撃のクールダウン時間（秒）

    [Header("Debug")]
    [SerializeField] protected bool m_show_debug_log = false;      // デバッグログの表示フラグ

    protected bool m_is_running = false;                           // 攻撃実行中かどうか
    protected float m_last_attack_time = -999.0f;                  // 最後に攻撃を実行した時刻
    protected AttackState m_attack_state = AttackState.Idle;       // 現在の攻撃状態

    // プロパティ：外部からアクセス可能な読み取り専用情報
    public string AttackName => m_attack_name;       // 攻撃名
    public int Priority => m_priority;               // 優先度
    public bool IsRunning => m_is_running;           // 実行中フラグ
    public AttackState State => m_attack_state;      // 現在の状態

    /// <summary>
    /// 初期化処理
    /// 攻撃ビジュアルを非表示にして初期化
    /// </summary>
    protected virtual void Awake()
    {
        SetAttackVisualVisible(false);
        OnInitializeAttackVisual();
    }

    /// <summary>
    /// 攻撃を開始できるかどうかを判定
    /// </summary>
    public bool CanStart(EnemyContext context)
    {
        // 既に実行中の場合は開始できない
        if (m_is_running)
        {
            return false;
        }

        // クールダウン中は開始できない
        if (Time.time < m_last_attack_time + m_cooldown)
        {
            return false;
        }

        // 派生クラスの条件をチェック
        return CheckCanStart(context);
    }

    /// <summary>
    /// 攻撃を開始する
    /// </summary>
    public void StartAttack(EnemyContext context)
    {
        m_is_running = true;
        m_attack_state = AttackState.WindUp;  // 予備動作状態へ

        SetAttackVisualVisible(true);         // ビジュアルを表示
        OnStartAttack(context);

        LogDebug("Start");
    }

    /// <summary>
    /// 攻撃の更新処理（毎フレーム呼ばれる）
    /// </summary>
    public void TickAttack(EnemyContext context)
    {
        if (!m_is_running)
        {
            return;
        }

        OnTickAttack(context);
    }

    /// <summary>
    /// 攻撃が終了したかどうかを判定
    /// </summary>
    public bool IsFinished()
    {
        if (!m_is_running)
        {
            return true;
        }

        return CheckIsFinished();
    }

    /// <summary>
    /// 攻撃を終了する（正常終了）
    /// </summary>
    public void FinishAttack(EnemyContext context)
    {
        if (!m_is_running)
        {
            return;
        }

        m_is_running = false;
        m_last_attack_time = Time.time;  // 次のクールダウン計算用に記録
        m_attack_state = AttackState.Idle;

        OnFinishAttack(context);
        SetAttackVisualVisible(false);   // ビジュアルを非表示

        LogDebug("Finish");
    }

    /// <summary>
    /// 攻撃をキャンセルする（強制中断）
    /// </summary>
    public virtual void CancelAttack(EnemyContext context)
    {
        m_is_running = false;
        m_last_attack_time = Time.time;
        m_attack_state = AttackState.Idle;

        OnCancelAttack(context);
        SetAttackVisualVisible(false);   // ビジュアルを非表示

        LogDebug("Cancel");
    }

    /// <summary>
    /// 攻撃状態を変更する（派生クラスから呼び出す）
    /// </summary>
    protected void SetAttackState(AttackState next_state)
    {
        m_attack_state = next_state;
    }

    /// <summary>
    /// デバッグログを出力（m_show_debug_logがtrueの場合のみ）
    /// </summary>
    protected void LogDebug(string message)
    {
        if (!m_show_debug_log)
        {
            return;
        }

        Debug.Log($"[EnemyAttackBase] {name} ({m_attack_name}) : {message}");
    }

    /// <summary>
    /// 攻撃ビジュアルの初期化処理
    /// 必要なら派生クラスでオーバーライドして使用
    /// </summary>
    protected virtual void OnInitializeAttackVisual()
    {
    }

    /// <summary>
    /// 攻撃ビジュアルの表示/非表示を切り替える
    /// 必要な攻撃だけ派生クラスでオーバーライドする
    /// </summary>
    protected virtual void SetAttackVisualVisible(bool visible)
    {
    }

    // 派生クラスで実装する必要がある抽象メソッド
    protected abstract bool CheckCanStart(EnemyContext context);    // 攻撃開始条件のチェック
    protected abstract void OnStartAttack(EnemyContext context);     // 攻撃開始時の処理
    protected abstract void OnTickAttack(EnemyContext context);      // 攻撃の更新処理
    protected abstract bool CheckIsFinished();                       // 攻撃終了判定
    protected abstract void OnFinishAttack(EnemyContext context);    // 攻撃終了時の処理

    /// <summary>
    /// 攻撃キャンセル時の処理
    /// 派生クラスで必要に応じてオーバーライド
    /// </summary>
    protected virtual void OnCancelAttack(EnemyContext context)
    {
    }
}