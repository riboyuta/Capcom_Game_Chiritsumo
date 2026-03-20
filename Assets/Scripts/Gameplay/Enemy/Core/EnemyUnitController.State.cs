using UnityEngine;

// EnemyUnitController の状態遷移、攻撃開始条件判定、Gizmo 可視化を担当する partial。
// Idle → Windup → Attack → Recovery → Idle の循環で運用する。
public sealed partial class EnemyUnitController
{
    private enum AttackDecision
    {
        None,
        Grab,
        Smash
    }

    [System.Serializable]
    private sealed class AttackDecisionTuning
    {
        [Header("Common")]
        [Tooltip("攻撃開始条件を再判定する間隔。短いほど反応が速い。")]
        public float decisionInterval = 0.10f;

        [Tooltip("1回攻撃した後、次の攻撃を開始できるまでの待機時間。")]
        public float attackCooldown = 0.75f;

        [Header("Grab Trigger")]
        [Tooltip("Grab を出せる最大X距離。")]
        public float grabMaxXDistance = 1.50f;

        [Tooltip("Grab を出せる最大Y距離。")]
        public float grabMaxYDistance = 1.00f;

        [Header("Smash Trigger")]
        [Tooltip("Smash を出し始める最小X距離。近すぎる場合は Grab を優先する。")]
        public float smashMinXDistance = 1.00f;

        [Tooltip("Smash を出せる最大X距離。")]
        public float smashMaxXDistance = 5.00f;

        [Tooltip("Smash を出せる最大Y距離。")]
        public float smashMaxYDistance = 2.50f;
    }

    [Header("Attack Decision")]
    [Tooltip("距離ベースの攻撃開始条件。")]
    [SerializeField] private AttackDecisionTuning attackDecisionTuning = new();

    [Tooltip("距離条件を満たした時に自動で攻撃を開始する。")]
    [SerializeField] private bool enableAutoAttackDecision = true;

    [Tooltip("攻撃判定対象。通常はプレイヤーの Transform。")]
    [SerializeField] private Transform attackTarget;

    [Header("Front Check")]
    [Tooltip("前方にいる相手にだけ攻撃する。")]
    [SerializeField] private bool useFrontCheck = false;

    [Tooltip("前方判定。true = 右向き / false = 左向き。")]
    [SerializeField] private bool facingRight = true;

    [Header("Gizmo")]
    [Tooltip("攻撃トリガー範囲を SceneView に表示する。")]
    [SerializeField] private bool showAttackTriggerGizmos = true;

    [Tooltip("true の場合は常時表示、false の場合は選択時のみ表示。")]
    [SerializeField] private bool showAttackTriggerGizmosAlways = false;

    private float attackDecisionTimer = 0.0f;
    private float attackCooldownTimer = 0.0f;

    // Unity が毎フレーム呼び出す更新処理。
    private void Update()
    {
        float deltaTime = Time.deltaTime;

        // 攻撃後のクールダウンを更新
        TickAttackCooldown(deltaTime);

        // 状態ロジックを更新
        TickState(deltaTime);

        // Idle 中なら距離条件を見て自動攻撃予約
        TickAutoAttackDecision(deltaTime);

        // 見た目（位置、腕セグメント、アニメーター）を更新
        TickVisual(deltaTime);
    }

    // 状態タイマーを進め、現在の状態に応じた処理を実行する。
    private void TickState(float deltaTime)
    {
        if (config == null)
        {
            return;
        }

        stateTimer += deltaTime;

        switch (state)
        {
            case EnemyUnitState.Idle:
                // 待機中の攻撃判断は TickAutoAttackDecision 側で処理
                break;

            case EnemyUnitState.Windup:
                // 溜め終了時に、まだ攻撃条件を満たしているか再確認する
                if (stateTimer >= config.WindupDuration)
                {
                    if (CanExecuteReservedAttackNow())
                    {
                        StartReservedAttack();
                    }
                    else
                    {
                        // すでに範囲外なら攻撃キャンセル
                        ChangeState(EnemyUnitState.Idle);
                    }
                }
                break;

            case EnemyUnitState.Attack:
                // 攻撃実行は EnemyAttackController に一本化
                if (attackController == null || !attackController.IsRunning)
                {
                    ChangeState(EnemyUnitState.Recovery);
                }
                break;

            case EnemyUnitState.Recovery:
                if (stateTimer >= config.RecoveryDuration)
                {
                    ChangeState(EnemyUnitState.Idle);
                }
                break;
        }
    }

    // Windup 終了後に予約されていた攻撃を開始する。
    private void StartReservedAttack()
    {
        if (attackController == null)
        {
            ChangeState(EnemyUnitState.Recovery);
            return;
        }

        switch (reservedAttackType)
        {
            case EnemyAttackController.EnemyAttackType.Grab:
                attackController.BeginGrabAttack(reservedTargetWorld);
                break;

            case EnemyAttackController.EnemyAttackType.Smash:
                attackController.BeginSmashAttack(reservedTargetWorld);
                break;

            default:
                ChangeState(EnemyUnitState.Recovery);
                return;
        }

        BeginAttackCooldown();
        ChangeState(EnemyUnitState.Attack);
    }

    // Idle 中のみ、自動で攻撃開始条件を評価する。
    private void TickAutoAttackDecision(float deltaTime)
    {
        if (!enableAutoAttackDecision)
        {
            return;
        }

        if (state != EnemyUnitState.Idle)
        {
            return;
        }

        if (attackController != null && attackController.IsRunning)
        {
            return;
        }

        if (attackCooldownTimer > 0.0f)
        {
            return;
        }

        attackDecisionTimer -= deltaTime;
        if (attackDecisionTimer > 0.0f)
        {
            return;
        }

        attackDecisionTimer = Mathf.Max(0.01f, attackDecisionTuning.decisionInterval);

        AttackDecision decision = EvaluateAttackDecision();
        switch (decision)
        {
            case AttackDecision.Grab:
                ReserveGrabAttackInternal();
                break;

            case AttackDecision.Smash:
                ReserveSmashAttackInternal();
                break;
        }
    }

    // 現在の距離条件から、どの攻撃を出すべきかを決める。
    private AttackDecision EvaluateAttackDecision()
    {
        if (palm == null || attackTarget == null || attackDecisionTuning == null)
        {
            return AttackDecision.None;
        }

        Vector3 selfPos = palm.position;
        Vector3 targetPos = attackTarget.position;

        float dxSigned = targetPos.x - selfPos.x;
        float dx = Mathf.Abs(dxSigned);
        float dy = Mathf.Abs(targetPos.y - selfPos.y);

        if (useFrontCheck)
        {
            bool inFront = facingRight ? dxSigned >= 0.0f : dxSigned <= 0.0f;
            if (!inFront)
            {
                return AttackDecision.None;
            }
        }

        // まず近距離の Grab を優先
        bool canGrab =
            dx <= attackDecisionTuning.grabMaxXDistance &&
            dy <= attackDecisionTuning.grabMaxYDistance;

        if (canGrab)
        {
            return AttackDecision.Grab;
        }

        // 次に中距離の Smash
        bool canSmash =
            dx >= attackDecisionTuning.smashMinXDistance &&
            dx <= attackDecisionTuning.smashMaxXDistance &&
            dy <= attackDecisionTuning.smashMaxYDistance;

        if (canSmash)
        {
            return AttackDecision.Smash;
        }

        return AttackDecision.None;
    }

    // Windup 終了時に、予約された攻撃をまだ実行してよいか再確認する。
    private bool CanExecuteReservedAttackNow()
    {
        if (palm == null || attackDecisionTuning == null)
        {
            return false;
        }

        // attackTarget があるなら現在位置を使う。
        // なければ予約時の座標を使う。
        Vector3 currentTargetPosition = attackTarget != null
            ? attackTarget.position
            : reservedTargetWorld;

        float dxSigned = currentTargetPosition.x - palm.position.x;
        float dx = Mathf.Abs(dxSigned);
        float dy = Mathf.Abs(currentTargetPosition.y - palm.position.y);

        if (useFrontCheck)
        {
            bool inFront = facingRight ? dxSigned >= 0.0f : dxSigned <= 0.0f;
            if (!inFront)
            {
                return false;
            }
        }

        switch (reservedAttackType)
        {
            case EnemyAttackController.EnemyAttackType.Grab:
                return
                    dx <= attackDecisionTuning.grabMaxXDistance &&
                    dy <= attackDecisionTuning.grabMaxYDistance;

            case EnemyAttackController.EnemyAttackType.Smash:
                return
                    dx >= attackDecisionTuning.smashMinXDistance &&
                    dx <= attackDecisionTuning.smashMaxXDistance &&
                    dy <= attackDecisionTuning.smashMaxYDistance;
        }

        return false;
    }

    // Grab を内部予約する。
    private void ReserveGrabAttackInternal()
    {
        if (state != EnemyUnitState.Idle || attackTarget == null)
        {
            return;
        }

        reservedTargetWorld = attackTarget.position;
        reservedAttackType = EnemyAttackController.EnemyAttackType.Grab;
        ChangeState(EnemyUnitState.Windup);
    }

    // Smash を内部予約する。
    private void ReserveSmashAttackInternal()
    {
        if (state != EnemyUnitState.Idle || attackTarget == null)
        {
            return;
        }

        reservedTargetWorld = attackTarget.position;
        reservedAttackType = EnemyAttackController.EnemyAttackType.Smash;
        ChangeState(EnemyUnitState.Windup);
    }

    // 攻撃後クールダウンを更新する。
    private void TickAttackCooldown(float deltaTime)
    {
        if (attackCooldownTimer > 0.0f)
        {
            attackCooldownTimer -= deltaTime;
        }
    }

    // 攻撃開始時にクールダウンをセットする。
    private void BeginAttackCooldown()
    {
        if (attackDecisionTuning == null)
        {
            attackCooldownTimer = 0.0f;
            return;
        }

        attackCooldownTimer = Mathf.Max(0.0f, attackDecisionTuning.attackCooldown);
    }

    // 指定された状態へ遷移し、状態タイマーをリセットする。
    private void ChangeState(EnemyUnitState next)
    {
        state = next;
        stateTimer = 0.0f;
    }

    private void OnDrawGizmosSelected()
    {
        if (!showAttackTriggerGizmos || showAttackTriggerGizmosAlways)
        {
            return;
        }

        DrawAttackTriggerGizmos();
    }

    private void OnDrawGizmos()
    {
        if (!showAttackTriggerGizmos || !showAttackTriggerGizmosAlways)
        {
            return;
        }

        DrawAttackTriggerGizmos();
    }

    // Grab / Smash の発生距離を SceneView に可視化する。
    private void DrawAttackTriggerGizmos()
    {
        Transform origin = palm != null ? palm : transform;
        if (origin == null || attackDecisionTuning == null)
        {
            return;
        }

        Vector3 center = origin.position;

        // Grab 範囲
        Gizmos.color = new Color(0.2f, 1.0f, 0.2f, 0.35f);
        Vector3 grabSize = new Vector3(
            attackDecisionTuning.grabMaxXDistance * 2.0f,
            attackDecisionTuning.grabMaxYDistance * 2.0f,
            0.05f
        );
        Gizmos.DrawWireCube(center, grabSize);

        // Smash 最大範囲
        Gizmos.color = new Color(1.0f, 0.75f, 0.2f, 0.35f);
        Vector3 smashOuterSize = new Vector3(
            attackDecisionTuning.smashMaxXDistance * 2.0f,
            attackDecisionTuning.smashMaxYDistance * 2.0f,
            0.05f
        );
        Gizmos.DrawWireCube(center, smashOuterSize);

        // Smash 最小距離
        if (attackDecisionTuning.smashMinXDistance > 0.0f)
        {
            Gizmos.color = new Color(1.0f, 0.35f, 0.2f, 0.35f);
            Vector3 smashInnerSize = new Vector3(
                attackDecisionTuning.smashMinXDistance * 2.0f,
                attackDecisionTuning.smashMaxYDistance * 2.0f,
                0.05f
            );
            Gizmos.DrawWireCube(center, smashInnerSize);
        }

        // 前方線
        if (useFrontCheck)
        {
            Gizmos.color = Color.cyan;
            float dir = facingRight ? 1.0f : -1.0f;
            Vector3 lineEnd = center + Vector3.right * dir * attackDecisionTuning.smashMaxXDistance;
            Gizmos.DrawLine(center, lineEnd);
        }

        // 現在のターゲット位置
        if (attackTarget != null)
        {
            Gizmos.color = Color.white;
            Gizmos.DrawLine(center, attackTarget.position);
            Gizmos.DrawWireSphere(attackTarget.position, 0.15f);
        }
    }
}