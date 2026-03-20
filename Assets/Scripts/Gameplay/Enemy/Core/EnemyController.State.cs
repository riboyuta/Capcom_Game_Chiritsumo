using UnityEngine;

// EnemyUnitController の状態遷移と更新ループを担当する partial。
// Idle → Windup → Attack → Recovery → Idle の循環で運用する。
public sealed partial class EnemyUnitController
{
    // Unity が毎フレーム呼び出す更新処理。
    // 状態遷移と見た目の同期を順番に実行する。
    private void Update()
    {
        // 状態ロジックを更新
        TickState(Time.deltaTime);
        // 見た目（位置、腕セグメント、アニメーター）を更新
        TickVisual(Time.deltaTime);
    }

    // 状態タイマーを進め、現在の状態に応じた処理を実行する。
    // 各状態で時間経過をチェックし、次の状態へ遷移するか判断する。
    private void TickState(float deltaTime)
    {
        // Config がないと時間設定を取得できないので処理をスキップ
        if (config == null)
        {
            return;
        }

        // 状態タイマーを進める
        stateTimer += deltaTime;

        // 現在の状態に応じた処理
        switch (state)
        {
            case EnemyUnitState.Idle:
                // 待機状態では何もしない（外部からの攻撃命令待ち）
                break;

            case EnemyUnitState.Windup:
                // 溜め時間が終了したら攻撃開始
                if (stateTimer >= config.WindupDuration)
                {
                    StartReservedAttack();
                }
                break;

            case EnemyUnitState.Attack:
                // AttackController が攻撃を完了したら Recovery へ遷移
                if (attackController == null || !attackController.IsRunning)
                {
                    ChangeState(EnemyUnitState.Recovery);
                }
                break;

            case EnemyUnitState.Recovery:
                // リカバリー時間が終了したら Idle に戻る
                if (stateTimer >= config.RecoveryDuration)
                {
                    ChangeState(EnemyUnitState.Idle);
                }
                break;
        }
    }

    // Windup 終了後に予約されていた攻撃を開始する。
    // 予約された攻撃種類に応じて AttackController に攻撃実行を依頼する。
    private void StartReservedAttack()
    {
        // AttackController がない場合は攻撃をスキップして Recovery へ遷移
        if (attackController == null)
        {
            ChangeState(EnemyUnitState.Recovery);
            return;
        }

        // 予約された攻撃種類に応じて攻撃を開始
        switch (reservedAttackType)
        {
            case EnemyAttackController.EnemyAttackType.Grab:
                attackController.BeginGrabAttack(reservedTargetWorld);
                break;

            case EnemyAttackController.EnemyAttackType.Smash:
                attackController.BeginSmashAttack(reservedTargetWorld);
                break;
        }

        // Attack 状態へ遷移
        ChangeState(EnemyUnitState.Attack);
    }

    // 指定された状態へ遷移し、状態タイマーをリセットする。
    private void ChangeState(EnemyUnitState next)
    {
        state = next;
        stateTimer = 0.0f;
    }
}