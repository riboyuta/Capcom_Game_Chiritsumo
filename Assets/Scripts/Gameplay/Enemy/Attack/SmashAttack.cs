using UnityEngine;

/// <summary>
/// スマッシュ攻撃の実装
/// 予備動作→攻撃実行→硬直の3段階で構成される近接攻撃
/// </summary>
public sealed class SmashAttack : EnemyAttackBase
{
    [Header("Range")]
    [SerializeField] private float m_range_x = 2.0f;                // 攻撃可能範囲（X軸）
    [SerializeField] private float m_range_y = 2.0f;                // 攻撃可能範囲（Y軸）

    [Header("Timing")]
    [SerializeField] private float m_windup_time = 0.35f;           // 予備動作の時間（秒）
    [SerializeField] private float m_active_time = 0.15f;           // 攻撃判定が有効な時間（秒）
    [SerializeField] private float m_recover_time = 0.4f;           // 硬直時間（秒）

    [Header("HitBox")]
    [SerializeField] private EnemyHitBox m_hit_box;                 // 攻撃判定用のヒットボックス

    [Header("Animation")]
    [SerializeField] private string m_trigger_name = "smash";       // アニメーション再生用のトリガー名

    [Header("Debug")]
    [SerializeField] private bool m_draw_range_gizmo = true;        // 攻撃範囲のギズモを描画するか

    private float m_timer = 0.0f;                                   // 攻撃の各フェーズを計測するタイマー

    /// <summary>
    /// 攻撃開始条件をチェック
    /// プレイヤーが攻撃範囲内にいるかどうかを判定
    /// </summary>
    protected override bool CheckCanStart(EnemyContext context)
    {
        // コンテキストと必要な参照が有効かチェック
        if (context == null || context.player_transform == null || context.enemy_controller == null)
        {
            return false;
        }

        float distance_x = context.enemy_controller.GetPlayerDistanceX();
        float distance_y = context.enemy_controller.GetPlayerDistanceY();

        // プレイヤーが敵の後方にいる場合は攻撃不可
        if (distance_x < 0.0f)
        {
            return false;
        }

        // プレイヤーが攻撃範囲内にいるかチェック
        return distance_x <= m_range_x && distance_y <= m_range_y;
    }

    /// <summary>
    /// 攻撃開始時の処理
    /// タイマーをリセットし、予備動作状態に遷移、アニメーションを再生
    /// </summary>
    protected override void OnStartAttack(EnemyContext context)
    {
        m_timer = 0.0f;
        SetAttackState(AttackState.WindUp);  // 予備動作状態へ

        // ヒットボックスを無効化（予備動作中は当たり判定なし）
        if (m_hit_box != null)
        {
            m_hit_box.DeactivateHitBox();
        }

        // アニメーショントリガーを設定
        if (context.enemy_animator != null && !string.IsNullOrEmpty(m_trigger_name))
        {
            context.enemy_animator.SetTrigger(m_trigger_name);
        }
    }

    /// <summary>
    /// 攻撃の更新処理（毎フレーム呼ばれる）
    /// 状態に応じてタイマーを進め、WindUp→Active→Recoverと遷移
    /// </summary>
    protected override void OnTickAttack(EnemyContext context)
    {
        m_timer += Time.deltaTime;

        switch (State)
        {
            case AttackState.WindUp:  // 予備動作フェーズ
                {
                    // 予備動作時間が経過したら攻撃実行フェーズへ
                    if (m_timer >= m_windup_time)
                    {
                        m_timer = 0.0f;
                        SetAttackState(AttackState.Active);

                        // ヒットボックスを有効化（攻撃判定開始）
                        if (m_hit_box != null)
                        {
                            m_hit_box.ActivateHitBox();
                        }
                    }
                    break;
                }

            case AttackState.Active:  // 攻撃実行フェーズ
                {
                    // 攻撃実行時間が経過したら硬直フェーズへ
                    if (m_timer >= m_active_time)
                    {
                        m_timer = 0.0f;
                        SetAttackState(AttackState.Recover);

                        // ヒットボックスを無効化（攻撃判定終了）
                        if (m_hit_box != null)
                        {
                            m_hit_box.DeactivateHitBox();
                        }
                    }
                    break;
                }

            case AttackState.Recover:  // 硬直フェーズ
                {
                    // タイマーが進むだけで特別な処理なし
                    break;
                }
        }
    }

    /// <summary>
    /// 攻撃が終了したかどうかを判定
    /// 硬直時間が経過したら終了とみなす
    /// </summary>
    protected override bool CheckIsFinished()
    {
        // 硬直状態でない場合はまだ終了していない
        if (State != AttackState.Recover)
        {
            return false;
        }

        // 硬直時間が経過したら終了
        return m_timer >= m_recover_time;
    }

    /// <summary>
    /// 攻撃終了時の処理（正常終了）
    /// ヒットボックスを確実に無効化してクリーンアップ
    /// </summary>
    protected override void OnFinishAttack(EnemyContext context)
    {
        if (m_hit_box != null)
        {
            m_hit_box.DeactivateHitBox();
        }
    }

    /// <summary>
    /// 攻撃キャンセル時の処理（強制中断）
    /// ヒットボックスを確実に無効化してクリーンアップ
    /// </summary>
    protected override void OnCancelAttack(EnemyContext context)
    {
        if (m_hit_box != null)
        {
            m_hit_box.DeactivateHitBox();
        }
    }

    /// <summary>
    /// Unityエディタでオブジェクト選択時にギズモを描画（デバッグ用）
    /// 攻撃範囲を赤いワイヤーボックスで視覚的に表示
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        if (!m_draw_range_gizmo)
        {
            return;
        }

        // 攻撃範囲を赤いワイヤーボックスで描画
        Gizmos.color = Color.red;
        Vector3 center = transform.position + Vector3.right * (m_range_x * 0.5f);  // 範囲の中心位置
        Vector3 size = new Vector3(m_range_x, m_range_y * 2.0f, 0.0f);             // ボックスのサイズ
        Gizmos.DrawWireCube(center, size);
    }
}