using UnityEngine;

// スマッシュ攻撃の実装
// 予備動作→攻撃実行→硬直の3段階で構成される近接攻撃
// ビジュアル表現付き：伸縮と回転でハンマーのようなモーションを再現
public sealed class SmashAttack : EnemyAttackBase
{
    [Header("Range")]
    [Header("攻撃可能範囲（X軸）")]
    [SerializeField] private float rangeX = 2.0f;                // 攻撃可能範囲（X軸）
    [Header("攻撃可能範囲（Y軸）")]
    [SerializeField] private float rangeY = 2.0f;                // 攻撃可能範囲（Y軸）

    [Header("Timing")]
    [Header("予備動作時間")]
    [SerializeField] private float windupTime = 0.35f;           // 予備動作の時間（秒）
    [Header("攻撃判定の有効時間")]
    [SerializeField] private float activeTime = 0.15f;           // 攻撃判定が有効な時間（秒）
    [Header("硬直時間")]
    [SerializeField] private float recoverTime = 0.4f;           // 硬直時間（秒）
    [Header("HitBox")]
    [Header("攻撃判定用のヒットボックス")]
    [SerializeField] private EnemyHitBox hitBox;                 // 攻撃判定用のヒットボックス

    [Header("Animation")]
    [Header("アニメーション再生用のトリガー名")]
    [SerializeField] private string triggerName = "smash";       // アニメーション再生用のトリガー名
    [Header("Visibility")]
    [Header("スマッシュのビジュアルのルートオブジェクト")]
    [SerializeField] private GameObject smashVisualRoot;        // スマッシュのビジュアルのルートオブジェクト（表示/非表示切り替え用）

    [Header("Smash Motion")]
    [Header("スマッシュの回転軸")]
    [SerializeField] private Transform smashPivot;               // スマッシュの回転軸（この点を中心に回転）
    [Header("スマッシュのビジュアル部分")]
    [SerializeField] private Transform smashVisual;              // スマッシュのビジュアル部分（伸縮させるScale変更対象）
    [Header("ヒットボックスのルート")]
    [SerializeField] private Transform hitBoxRoot;              // ヒットボックスのルート（スマッシュの長さに合わせて移動）
    [Header("収納状態の長さ")]
    [SerializeField] private float retractedLength = 0.25f;      // 収納状態の長さ（短い）
    [Header("展開状態の長さ")]
    [SerializeField] private float extendedLength = 1.40f;       // 展開状態の長さ（長い）

    [Header("前方向きの角度")]
    [SerializeField] private float forwardAngle = 0.0f;          // 前方向きの角度（通常状態）
    [Header("振りかぶり状態の角度")]
    [SerializeField] private float raisedAngle = 75.0f;          // 振りかぶり状態の角度（上に上げた状態）
    [Header("叩きつけ状態の角度")]
    [SerializeField] private float slamAngle = -95.0f;           // 叩きつけ状態の角度（下に振り下ろした状態）
    [Header("予備動作中の伸長フェーズの比率")]
    [SerializeField, Range(0.0f, 1.0f)] private float extendRatioInWindup = 0.35f;  // 予備動作中の伸長フェーズの比率（0~1）

    [Header("Debug")]
    [Header("攻撃範囲のギズモ描画")]
    [SerializeField] private bool drawRangeGizmo = true;        // 攻撃範囲のギズモを描画するか
    private float timer = 0.0f;                                   // 攻撃の各フェーズを計測するタイマー
    private int attackDirectionSign = 1;                        // 攻撃方向の符号（1=右、-1=左）

    // 攻撃ビジュアルの初期化処理
    // 初期ポーズ（収納・前方向き）を設定
    protected override void OnInitializeAttackVisual()
    {
        ApplyPose(retractedLength, forwardAngle);

        if (hitBox != null)
        {
            hitBox.DeactivateHitBox();
        }
    }

    // 攻撃ビジュアルの表示/非表示を切り替える
    protected override void SetAttackVisualVisible(bool visible)
    {
        if (smashVisualRoot != null)
        {
            smashVisualRoot.SetActive(visible);
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
        return distance_x <= rangeX && distance_y <= rangeY;
    }

    // 攻撃開始時の処理
    // タイマーをリセットし、予備動作状態に遷移、攻撃方向を計算
    protected override void OnStartAttack(EnemyContext context)
    {
        timer = 0.0f;
        SetAttackState(AttackState.WindUp);  // 予備動作状態へ

        // ヒットボックスを無効化（予備動作中は当たり判定なし）
        if (hitBox != null)
        {
            hitBox.DeactivateHitBox();
        }

        // プレイヤーの方向に応じて攻撃方向を決定（右=1、左=-1）
        if (context != null && context.player_transform != null)
        {
            float delta_x = context.player_transform.position.x - transform.position.x;
            attackDirectionSign = (delta_x >= 0.0f) ? 1 : -1;
        }
        else
        {
            attackDirectionSign = 1;
        }

        // 初期ポーズを適用（収納・前方向き）
        ApplyPose(retractedLength, forwardAngle);
        // アニメーショントリガーを設定
        if (context.enemy_animator != null && !string.IsNullOrEmpty(triggerName))
        {
            context.enemy_animator.SetTrigger(triggerName);
        }
    }

    // 攻撃の更新処理（毎フレーム呼ばれる）
    // 状態に応じてタイマーを進め、WindUp→Active→Recoverと遷移
    // 各状態でビジュアルポーズも更新
    protected override void OnTickAttack(EnemyContext context)
    {
        timer += Time.deltaTime;

        switch (State)
        {
            case AttackState.WindUp:  // 予備動作フェーズ
                UpdateWindUpPose();   // 予備動作のポーズを更新
                if (timer >= windupTime)
                {
                    timer = 0.0f;
                    SetAttackState(AttackState.Active);

                    // ヒットボックスを有効化（攻撃判定開始）
                    if (hitBox != null)
                    {
                        hitBox.ActivateHitBox();
                    }
                }
                break;

            case AttackState.Active:  // 攻撃実行フェーズ
                UpdateActivePose();   // 攻撃実行のポーズを更新
                if (timer >= activeTime)
                {
                    timer = 0.0f;
                    SetAttackState(AttackState.Recover);

                    // ヒットボックスを無効化（攻撃判定終了）
                    if (hitBox != null)
                    {
                        hitBox.DeactivateHitBox();
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
        return timer >= recoverTime;
    }

    // 攻撃終了時の処理（正常終了）
    // ヒットボックスを確実に無効化し、初期ポーズに戻す
    protected override void OnFinishAttack(EnemyContext context)
    {
        if (hitBox != null)
        {
            hitBox.DeactivateHitBox();
        }

        ApplyPose(retractedLength, forwardAngle);
    }

    // 攻撃キャンセル時の処理（強制中断）
    // ヒットボックスを確実に無効化し、初期ポーズに戻す
    protected override void OnCancelAttack(EnemyContext context)
    {
        if (hitBox != null)
        {
            hitBox.DeactivateHitBox();
        }

        ApplyPose(retractedLength, forwardAngle);
    }

    // 予備動作フェーズのポーズを更新
    // 2段階構成：1)伸ばすフェーズ 2)上に振りかぶるフェーズ
    private void UpdateWindUpPose()
    {
        if (windupTime <= 0.0f)
        {
            ApplyPose(extendedLength, raisedAngle);
            return;
        }

        float normalized = Mathf.Clamp01(timer / windupTime);
        float split = Mathf.Clamp01(extendRatioInWindup);

        // フェーズ1：伸ばす動き（splitの割合まで）
        if (normalized < split)
        {
            float t = (split <= 0.0001f) ? 1.0f : normalized / split;
            t = EaseOutCubic(t);  // イージング適用

            float length = Mathf.Lerp(retractedLength, extendedLength, t);
            ApplyPose(length, forwardAngle);
        }
        // フェーズ2：振りかぶる動き（splitから終わりまで）
        else
        {
            float denom = Mathf.Max(0.0001f, 1.0f - split);
            float t = (normalized - split) / denom;
            t = EaseInOutCubic(t);  // イージング適用

            float angle = Mathf.Lerp(forwardAngle, raisedAngle, t);
            ApplyPose(extendedLength, angle);
        }
    }

    // 攻撃実行フェーズのポーズを更新
    // 振りかぶり状態から叩きつけ状態へ高速で振り下ろす
    private void UpdateActivePose()
    {
        if (activeTime <= 0.0f)
        {
            ApplyPose(extendedLength, slamAngle);
            return;
        }

        float normalized = Mathf.Clamp01(timer / activeTime);
        float t = EaseInCubic(normalized);  // イージング適用（加速）

        float angle = Mathf.Lerp(raisedAngle, slamAngle, t);
        ApplyPose(extendedLength, angle);
    }

    // 硬直フェーズのポーズを更新
    // 叩きつけ状態から初期ポーズ（収納・前方向き）に戻る
    private void UpdateRecoverPose()
    {
        if (recoverTime <= 0.0f)
        {
            ApplyPose(retractedLength, forwardAngle);
            return;
        }

        float normalized = Mathf.Clamp01(timer / recoverTime);
        float t = EaseInOutCubic(normalized);  // イージング適用

        float length = Mathf.Lerp(extendedLength, retractedLength, t);
        float angle = Mathf.Lerp(slamAngle, forwardAngle, t);
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
        if (smashVisual == null)
        {
            return;
        }

        Vector3 scale = smashVisual.localScale;
        scale.x = Mathf.Max(0.01f, length);  // 最小値でクランプ（ゼロ除算防止）
        smashVisual.localScale = scale;
    }

    // スマッシュの角度を設定（ピボットを回転）
    private void SetSmashAngle(float angle)
    {
        if (smashPivot == null)
        {
            return;
        }

        // 攻撃方向の符号を適用（左右反転）
        float signed_angle = angle * attackDirectionSign;
        smashPivot.localRotation = Quaternion.Euler(0.0f, 0.0f, signed_angle);
    }

    // ヒットボックスの位置を更新（スマッシュの長さに合わせて移動）
    private void UpdateHitBoxRoot(float length)
    {
        if (hitBoxRoot == null)
        {
            return;
        }

        Vector3 localPos = hitBoxRoot.localPosition;
        localPos.x = length;  // X座標を長さに合わせる
        hitBoxRoot.localPosition = localPos;
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
        if (!drawRangeGizmo)
        {
            return;
        }

        Gizmos.color = Color.red;

        // 攻撃方向の符号を決定（実行時はm_attack_direction_sign、編集時はScaleから判定）
        float sign = 1.0f;
        if (Application.isPlaying)
        {
            sign = attackDirectionSign;
        }
        else
        {
            sign = transform.lossyScale.x >= 0.0f ? 1.0f : -1.0f;
        }

        // 攻攣範囲を赤いワイヤーボックスで描画
        Vector3 center = transform.position + Vector3.right * (rangeX * 0.5f * sign);  // 範囲の中心位置
        Vector3 size = new Vector3(rangeX, rangeY * 2.0f, 0.0f);                  // ボックスのサイズ
        Gizmos.DrawWireCube(center, size);
    }
}