using UnityEngine;

// PlayerController の攻撃リアクション状態を担当する partial。
// 通常被弾、掴まれ、叩きつけ、死亡の状態遷移を管理する。
public sealed partial class PlayerController : IEnemyAttackReceiver
{
    public enum PlayerReactionState
    {
        Normal,
        Damaged,
        Grabbed,
        Smashed,
        Dead
    }

    [Header("Reaction")]
    [SerializeField] private float damagedStateDuration = 0.15f;
    [SerializeField] private float grabbedStateDuration = 0.5f;
    [SerializeField] private float smashedStateDuration = 0.35f;
    [SerializeField] private bool grabIsInstantDeath = true;
    [SerializeField] private bool smashIsInstantDeath = true;
    [SerializeField] private bool showReactionDebugLog = false;

    [Header("Grab演出")]
    [SerializeField] private bool killAfterGrabbedDuration = true;
    [SerializeField] private bool snapToGrabAnchorImmediately = true;

    private PlayerReactionState reactionState = PlayerReactionState.Normal;
    private float reactionStateTimer = 0.0f;

    // 掴まれ中に追従する先
    private Transform currentGrabAnchor = null;

    public PlayerReactionState ReactionState => reactionState;
    public bool IsGrabbed => reactionState == PlayerReactionState.Grabbed;
    public bool IsSmashed => reactionState == PlayerReactionState.Smashed;
    public bool IsDeadState => reactionState == PlayerReactionState.Dead;

    // 入力禁止に使える
    public bool IsActionLocked =>
        reactionState == PlayerReactionState.Grabbed ||
        reactionState == PlayerReactionState.Smashed ||
        reactionState == PlayerReactionState.Dead;

    private void InitializeReactionState()
    {
        reactionState = PlayerReactionState.Normal;
        reactionStateTimer = 0.0f;
        currentGrabAnchor = null;
    }

    private void UpdateReactionState(float deltaTime)
    {
        reactionStateTimer += deltaTime;

        switch (reactionState)
        {
            case PlayerReactionState.Normal:
                break;

            case PlayerReactionState.Damaged:
                if (reactionStateTimer >= damagedStateDuration)
                {
                    ChangeReactionState(PlayerReactionState.Normal);
                }
                break;

            case PlayerReactionState.Grabbed:
                // 掴まれ中は手の位置に追従させる
                if (currentGrabAnchor != null)
                {
                    Vector3 target = currentGrabAnchor.position;
                    target.z = transform.position.z;

                    transform.position = Vector3.Lerp(
                        transform.position,
                        target,
                        deltaTime * 15.0f
                    );
                }

                // 一定時間拘束したあとに即死級ダメージを入れる
                if (reactionStateTimer >= grabbedStateDuration)
                {
                    if (killAfterGrabbedDuration)
                    {
                        // 拘束演出後に即死級ダメージを与える
                        TakeDamage(999, Vector3.zero, 0.0f);
                    }
                    else
                    {
                        ForceReleaseGrab();
                        ChangeReactionState(PlayerReactionState.Normal);
                    }
                }
                break;

            case PlayerReactionState.Smashed:
                if (!smashIsInstantDeath && reactionStateTimer >= smashedStateDuration)
                {
                    ChangeReactionState(PlayerReactionState.Normal);
                }
                break;

            case PlayerReactionState.Dead:
                break;
        }
    }

    public void ReceiveEnemyAttack(EnemyAttackContext context)
    {
        if (reactionState == PlayerReactionState.Dead)
        {
            return;
        }

        switch (context.AttackType)
        {
            case EnemyAttackController.EnemyAttackType.Grab:
                HandleGrabAttack(context);
                break;

            case EnemyAttackController.EnemyAttackType.Smash:
                HandleSmashAttack(context);
                break;
        }
    }

    private void HandleGrabAttack(EnemyAttackContext context)
    {
        LogReaction("Grab attack received.");

        currentGrabAnchor = context.GrabAnchor;
        ChangeReactionState(PlayerReactionState.Grabbed);

        // 掴まれた瞬間に手の位置へ寄せる
        if (snapToGrabAnchorImmediately && currentGrabAnchor != null)
        {
            Vector3 pos = transform.position;
            pos.x = currentGrabAnchor.position.x;
            pos.y = currentGrabAnchor.position.y;
            transform.position = pos;
        }

        // 即時にダメージを入れたい場合だけここで入れる
        // 拘束演出を見せたいなら通常は入れない
        if (!killAfterGrabbedDuration && context.Damage > 0)
        {
            TakeDamage(context.Damage, context.HitDirection, context.KnockbackForce);
        }
    }

    private void HandleSmashAttack(EnemyAttackContext context)
    {
        LogReaction("Smash attack received.");

        if (smashIsInstantDeath)
        {
            TakeDamage(context.Damage, context.HitDirection, context.KnockbackForce);
            return;
        }

        ChangeReactionState(PlayerReactionState.Smashed);
        TakeDamage(context.Damage, context.HitDirection, context.KnockbackForce);
    }

    private void ChangeReactionState(PlayerReactionState nextState)
    {
        reactionState = nextState;
        reactionStateTimer = 0.0f;

        if (reactionState != PlayerReactionState.Grabbed)
        {
            currentGrabAnchor = null;
        }

        LogReaction($"Reaction state changed: {reactionState}");
    }

    public void ForceReleaseGrab()
    {
        currentGrabAnchor = null;

        if (reactionState == PlayerReactionState.Grabbed)
        {
            ChangeReactionState(PlayerReactionState.Normal);
        }
    }

    private void LogReaction(string message)
    {
        if (!showReactionDebugLog)
        {
            return;
        }

        Debug.Log($"[PlayerReaction] {message}");
    }
}