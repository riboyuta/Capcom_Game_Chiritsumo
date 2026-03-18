using UnityEngine;

// スマッシュ攻撃の実装
// 予備動作→攻撃実行→硬直の3段階で構成される近接攻撃
// ビジュアル表現付き：伸縮と回転でハンマーのようなモーションを再現
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

    [Header("Visibility")]
    [SerializeField] private GameObject m_smash_visual_root;        // スマッシュのビジュアルのルートオブジェクト（表示/非表示切り替え用）

    [Header("Smash Motion")]
    [SerializeField] private Transform m_smash_pivot;               // スマッシュの回転軸（この点を中心に回転）
    [SerializeField] private Transform m_smash_visual;              // スマッシュのビジュアル部分（伸縮させるScale変更対象）
    [SerializeField] private Transform m_hit_box_root;              // ヒットボックスのルート（スマッシュの長さに合わせて移動）

    [SerializeField] private float m_retracted_length = 0.25f;      // 収納状態の長さ（短い）
    [SerializeField] private float m_extended_length = 1.40f;       // 展開状態の長さ（長い）

    [SerializeField] private float m_forward_angle = 0.0f;          // 前方向きの角度（通常状態）
    [SerializeField] private float m_raised_angle = 75.0f;          // 振りかぶり状態の角度（上に上げた状態）
    [SerializeField] private float m_slam_angle = -95.0f;           // 叩きつけ状態の角度（下に振り下ろした状態）

    [SerializeField, Range(0.0f, 1.0f)] private float m_extend_ratio_in_windup = 0.35f;  // 予備動作中の伸長フェーズの比率（0~1）

    [Header("Debug")]
    [SerializeField] private bool m_draw_range_gizmo = true;        // 攻撃範囲のギズモを描画するか

    private float m_timer = 0.0f;                                   // 攻撃の各フェーズを計測するタイマー
    private int m_attack_direction_sign = 1;                        // 攻撃方向の符号（1=右、-1=左）

    // 攻撃ビジュアルの初期化処理
    // 初期ポーズ（収納・前方向き）を設定
    protected override void OnInitializeAttackVisual()
    {
        ApplyPose(m_retracted_length, m_forward_angle);

        if (m_hit_box != null)
        {
            m_hit_box.DeactivateHitBox();
        }
    }

    // 攻撃ビジュアルの表示/非表示を切り替える
    protected override void SetAttackVisualVisible(bool visible)
    {
        if (m_smash_visual_root != null)
        {
            m_smash_visual_root.SetActive(visible);
        }
    }

    // 攻撃開始条件をチェック
    // プレイヤーが攻撃範囲内にいるかどうかを判定
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

    // 攻撃開始時の処理
    // タイマーをリセットし、予備動作状態に遷移、攻撃方向を計算
    protected override void OnStartAttack(EnemyContext context)
    {
        m_timer = 0.0f;
        SetAttackState(AttackState.WindUp);  // 予備動作状態へ

        // ヒットボックスを無効化（予備動作中は当たり判定なし）
        if (m_hit_box != null)
        {
            m_hit_box.DeactivateHitBox();
        }

        // プレイヤーの方向に応じて攻撃方向を決定（右=1、左=-1）
        if (context != null && context.player_transform != null)
        {
            float delta_x = context.player_transform.position.x - transform.position.x;
            m_attack_direction_sign = (delta_x >= 0.0f) ? 1 : -1;
        }
        else
        {
            m_attack_direction_sign = 1;
        }

        // 初期ポーズを適用（収納・前方向き）
        ApplyPose(m_retracted_length, m_forward_angle);

        // アニメーショントリガーを設定
        if (context.enemy_animator != null && !string.IsNullOrEmpty(m_trigger_name))
        {
            context.enemy_animator.SetTrigger(m_trigger_name);
        }
    }

    // 攻撃の更新処理（毎フレーム呼ばれる）
    // 状態に応じてタイマーを進め、WindUp→Active→Recoverと遷移
    // 各状態でビジュアルポーズも更新
    protected override void OnTickAttack(EnemyContext context)
    {
        m_timer += Time.deltaTime;

        switch (State)
        {
            case AttackState.WindUp:  // 予備動作フェーズ
                UpdateWindUpPose();   // 予備動作のポーズを更新
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

            case AttackState.Active:  // 攻撃実行フェーズ
                UpdateActivePose();   // 攻撃実行のポーズを更新
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

            case AttackState.Recover:  // 硬直フェーズ
                UpdateRecoverPose();   // 硬直のポーズを更新
                break;
        }
    }

    // 攻撃が終了したかどうかを判定
    // 硬直時間が経過したら終了とみなす
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

    // 攻撃終了時の処理（正常終了）
    // ヒットボックスを確実に無効化し、初期ポーズに戻す
    protected override void OnFinishAttack(EnemyContext context)
    {
        if (m_hit_box != null)
        {
            m_hit_box.DeactivateHitBox();
        }

        ApplyPose(m_retracted_length, m_forward_angle);
    }

    // 攻撃キャンセル時の処理（強制中断）
    // ヒットボックスを確実に無効化し、初期ポーズに戻す
    protected override void OnCancelAttack(EnemyContext context)
    {
        if (m_hit_box != null)
        {
            m_hit_box.DeactivateHitBox();
        }

        ApplyPose(m_retracted_length, m_forward_angle);
    }

    // 予備動作フェーズのポーズを更新
    // 2段階構成：1)伸ばすフェーズ 2)上に振りかぶるフェーズ
    private void UpdateWindUpPose()
    {
        if (m_windup_time <= 0.0f)
        {
            ApplyPose(m_extended_length, m_raised_angle);
            return;
        }

        float normalized = Mathf.Clamp01(m_timer / m_windup_time);
        float split = Mathf.Clamp01(m_extend_ratio_in_windup);

        // フェーズ1：伸ばす動き（splitの割合まで）
        if (normalized < split)
        {
            float t = (split <= 0.0001f) ? 1.0f : normalized / split;
            t = EaseOutCubic(t);  // イージング適用

            float length = Mathf.Lerp(m_retracted_length, m_extended_length, t);
            ApplyPose(length, m_forward_angle);
        }
        // フェーズ2：振りかぶる動き（splitから終わりまで）
        else
        {
            float denom = Mathf.Max(0.0001f, 1.0f - split);
            float t = (normalized - split) / denom;
            t = EaseInOutCubic(t);  // イージング適用

            float angle = Mathf.Lerp(m_forward_angle, m_raised_angle, t);
            ApplyPose(m_extended_length, angle);
        }
    }

    // 攻撃実行フェーズのポーズを更新
    // 振りかぶり状態から叩きつけ状態へ高速で振り下ろす
    private void UpdateActivePose()
    {
        if (m_active_time <= 0.0f)
        {
            ApplyPose(m_extended_length, m_slam_angle);
            return;
        }

        float normalized = Mathf.Clamp01(m_timer / m_active_time);
        float t = EaseInCubic(normalized);  // イージング適用（加速）

        float angle = Mathf.Lerp(m_raised_angle, m_slam_angle, t);
        ApplyPose(m_extended_length, angle);
    }

    // 硬直フェーズのポーズを更新
    // 叩きつけ状態から初期ポーズ（収納・前方向き）に戻る
    private void UpdateRecoverPose()
    {
        if (m_recover_time <= 0.0f)
        {
            ApplyPose(m_retracted_length, m_forward_angle);
            return;
        }

        float normalized = Mathf.Clamp01(m_timer / m_recover_time);
        float t = EaseInOutCubic(normalized);  // イージング適用

        float length = Mathf.Lerp(m_extended_length, m_retracted_length, t);
        float angle = Mathf.Lerp(m_slam_angle, m_forward_angle, t);
        ApplyPose(length, angle);
    }

    // ポーズを適用する（長さと角度を同時に設定）
    private void ApplyPose(float length, float angle)
    {
        SetSmashLength(length);
        SetSmashAngle(angle);
        UpdateHitBoxRoot(length);
    }

    // スマッシュの長さを設定（ScaleのX値を変更）
    private void SetSmashLength(float length)
    {
        if (m_smash_visual == null)
        {
            return;
        }

        Vector3 scale = m_smash_visual.localScale;
        scale.x = Mathf.Max(0.01f, length);  // 最小値でクランプ（ゼロ除算防止）
        m_smash_visual.localScale = scale;
    }

    // スマッシュの角度を設定（ピボットを回転）
    private void SetSmashAngle(float angle)
    {
        if (m_smash_pivot == null)
        {
            return;
        }

        // 攻撃方向の符号を適用（左右反転）
        float signed_angle = angle * m_attack_direction_sign;
        m_smash_pivot.localRotation = Quaternion.Euler(0.0f, 0.0f, signed_angle);
    }

    // ヒットボックスの位置を更新（スマッシュの長さに合わせて移動）
    private void UpdateHitBoxRoot(float length)
    {
        if (m_hit_box_root == null)
        {
            return;
        }

        Vector3 local_pos = m_hit_box_root.localPosition;
        local_pos.x = length;  // X座標を長さに合わせる
        m_hit_box_root.localPosition = local_pos;
    }

    // イージング関数：Ease In Cubic（加速）
    // アニメーションの開始が遅く、後半で加速
    private static float EaseInCubic(float x)
    {
        return x * x * x;
    }

    // イージング関数：Ease Out Cubic（減速）
    // アニメーションの開始が速く、後半で減速
    private static float EaseOutCubic(float x)
    {
        float inv = 1.0f - x;
        return 1.0f - inv * inv * inv;
    }

    // イージング関数：Ease In-Out Cubic（加減速）
    // アニメーションの開始と終わりが滞らか、中間が速い
    private static float EaseInOutCubic(float x)
    {
        return (x < 0.5f)
            ? 4.0f * x * x * x
            : 1.0f - Mathf.Pow(-2.0f * x + 2.0f, 3.0f) * 0.5f;
    }

    // Unityエディタでオブジェクト選択時にギズモを描画（デバッグ用）
    // 攻撃範囲を赤いワイヤーボックスで視覚的に表示
    private void OnDrawGizmosSelected()
    {
        if (!m_draw_range_gizmo)
        {
            return;
        }

        Gizmos.color = Color.red;

        // 攻撃方向の符号を決定（実行時はm_attack_direction_sign、編集時はScaleから判定）
        float sign = 1.0f;
        if (Application.isPlaying)
        {
            sign = m_attack_direction_sign;
        }
        else
        {
            sign = transform.lossyScale.x >= 0.0f ? 1.0f : -1.0f;
        }

        // 攻攣範囲を赤いワイヤーボックスで描画
        Vector3 center = transform.position + Vector3.right * (m_range_x * 0.5f * sign);  // 範囲の中心位置
        Vector3 size = new Vector3(m_range_x, m_range_y * 2.0f, 0.0f);                  // ボックスのサイズ
        Gizmos.DrawWireCube(center, size);
    }
}