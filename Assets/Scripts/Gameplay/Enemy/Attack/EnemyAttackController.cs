using UnityEngine;

/// <summary>
/// 敵の攻撃を管理するコントローラー
/// 複数の攻撃パターンから優先度に基づいて最適な攻撃を選択し、実行する
/// </summary>
public sealed class EnemyAttackController : MonoBehaviour
{
    [SerializeField] private EnemyAttackBase[] m_attacks;          // この敵が持つすべての攻撃パターン
    [SerializeField] private bool m_show_debug_log = false;        // デバッグログの表示フラグ

    private EnemyAttackBase m_current_attack;                      // 現在実行中の攻撃

    // プロパティ：外部からアクセス可能な読み取り専用情報
    public bool IsAttacking => m_current_attack != null;                                                      // 攻撃実行中かどうか
    public string CurrentAttackName => m_current_attack != null ? m_current_attack.AttackName : "None";      // 現在の攻撃名

    /// <summary>
    /// Unity エディタでコンポーネント追加時に自動で呼ばれる
    /// 同じGameObjectにアタッチされている攻撃コンポーネントを自動収集
    /// </summary>
    private void Reset()
    {
        m_attacks = GetComponents<EnemyAttackBase>();
    }

    /// <summary>
    /// 攻撃の開始を試みる
    /// </summary>
    /// <returns>攻撃を開始できた場合はtrue</returns>
    public bool TryStartAttack(EnemyContext context)
    {
        // 既に攻撃実行中の場合は開始できない
        if (m_current_attack != null)
        {
            return false;
        }

        // 実行可能な攻撃の中から最適なものを選択
        EnemyAttackBase next_attack = FindBestAttack(context);
        if (next_attack == null)
        {
            return false;
        }

        // 攻撃を開始
        m_current_attack = next_attack;
        m_current_attack.StartAttack(context);

        LogDebug($"Start {m_current_attack.AttackName}");
        return true;
    }

    /// <summary>
    /// 現在実行中の攻撃を更新する（毎フレーム呼ばれる）
    /// </summary>
    public void TickCurrentAttack(EnemyContext context)
    {
        if (m_current_attack == null)
        {
            return;
        }

        // 攻撃の更新処理を実行
        m_current_attack.TickAttack(context);

        // 攻撃が終了したかチェック
        if (m_current_attack.IsFinished())
        {
            LogDebug($"Finish {m_current_attack.AttackName}");
            m_current_attack.FinishAttack(context);
            m_current_attack = null;  // 攻撃終了後はnullに戻す
        }
    }

    /// <summary>
    /// 現在実行中の攻撃をキャンセルする（強制中断）
    /// </summary>
    public void CancelCurrentAttack(EnemyContext context)
    {
        if (m_current_attack == null)
        {
            return;
        }

        LogDebug($"Cancel {m_current_attack.AttackName}");
        m_current_attack.CancelAttack(context);
        m_current_attack = null;
    }

    /// <summary>
    /// 現在の状況で実行可能な攻撃の中から、最も優先度の高いものを選択
    /// </summary>
    private EnemyAttackBase FindBestAttack(EnemyContext context)
    {
        if (m_attacks == null || m_attacks.Length == 0)
        {
            return null;
        }

        EnemyAttackBase best_attack = null;
        int best_priority = int.MinValue;

        // すべての攻撃パターンをチェック
        foreach (EnemyAttackBase attack in m_attacks)
        {
            if (attack == null)
            {
                continue;
            }

            // 開始条件を満たしているかチェック
            if (!attack.CanStart(context))
            {
                continue;
            }

            // より優先度の高い攻撃を選択
            if (attack.Priority > best_priority)
            {
                best_priority = attack.Priority;
                best_attack = attack;
            }
        }

        return best_attack;
    }

    /// <summary>
    /// デバッグログを出力（m_show_debug_logがtrueの場合のみ）
    /// </summary>
    private void LogDebug(string message)
    {
        if (!m_show_debug_log)
        {
            return;
        }

        Debug.Log($"[EnemyAttackController] {name} : {message}");
    }
}