using UnityEngine;

// 敵の攻撃を管理するコントローラー
// 複数の攻撃パターンから優先度に基づいて最適な攻撃を選択し、実行する
public sealed class EnemyAttackController : MonoBehaviour
{
    [Header("この敵が持つすべての攻撃パターン")]
    [SerializeField] private EnemyAttackBase[] attacks;          // この敵が持つすべての攻撃パターン
    [Header("デバッグログの表示")]
    [SerializeField] private bool showDebugLog = false;        // デバッグログの表示フラグ

    private EnemyAttackBase currentAttack;                      // 現在実行中の攻撃

    // プロパティ：外部からアクセス可能な読み取り専用情報
    public bool IsAttacking => currentAttack != null;                                                      // 攻撃実行中かどうか
    public string CurrentAttackName => currentAttack != null ? currentAttack.AttackName : "None";      // 現在の攻撃名

    // Unity エディタでコンポーネント追加時に自動で呼ばれる
    // 同じGameObjectにアタッチされている攻撃コンポーネントを自動収集
    private void Reset()
    {
        attacks = GetComponents<EnemyAttackBase>();
    }

    // 攻撃の開始を試みる
    // Returns: 攻撃を開始できた場合はtrue
    public bool TryStartAttack(EnemyContext context)
    {
        // 既に攻撃実行中の場合は開始できない
        if (currentAttack != null)
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
        currentAttack = next_attack;
        currentAttack.StartAttack(context);

        LogDebug($"Start {currentAttack.AttackName}");
        return true;
    }

    // 現在実行中の攻撃を更新する（毎フレーム呼ばれる）
    public void TickCurrentAttack(EnemyContext context)
    {
        if (currentAttack == null)
        {
            return;
        }

        // 攻撃の更新処理を実行
        currentAttack.TickAttack(context);

        // 攻撃が終了したかチェック
        if (currentAttack.IsFinished())
        {
            LogDebug($"Finish {currentAttack.AttackName}");
            currentAttack.FinishAttack(context);
            currentAttack = null;  // 攻撃終了後はnullに戻す
        }
    }

    // 現在実行中の攻撃をキャンセルする（強制中断）
    public void CancelCurrentAttack(EnemyContext context)
    {
        if (currentAttack == null)
        {
            return;
        }

        LogDebug($"Cancel {currentAttack.AttackName}");
        currentAttack.CancelAttack(context);
        currentAttack = null;
    }

    // 現在の状況で実行可能な攻撃の中から、最も優先度の高いものを選択
    private EnemyAttackBase FindBestAttack(EnemyContext context)
    {
        if (attacks == null || attacks.Length == 0)
        {
            return null;
        }

        EnemyAttackBase best_attack = null;
        int best_priority = int.MinValue;

        // すべての攻撃パターンをチェック
        foreach (EnemyAttackBase attack in attacks)
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

    // デバッグログを出力（m_show_debug_logがtrueの場合のみ）
    private void LogDebug(string message)
    {
        if (!showDebugLog)
        {
            return;
        }

        Debug.Log($"[EnemyAttackController] {name} : {message}");
    }
}