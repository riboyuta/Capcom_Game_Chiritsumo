using UnityEngine;

// 敵攻撃がプレイヤーへ渡す情報をまとめた構造体。
public struct EnemyAttackContext
{
    public EnemyAttackController.EnemyAttackType AttackType;
    public int Damage;
    public float KnockbackForce;
    public Vector3 HitDirection;
    public Vector3 HitPoint;
    public Transform GrabAnchor;
    public Transform Attacker;
}

// 敵攻撃を受けた時の状態変化を受け取るインターフェース。
public interface IEnemyAttackReceiver
{
    void ReceiveEnemyAttack(EnemyAttackContext context);
}

// ダメージを受け取れるオブジェクト用の共通インターフェース。
public interface IDamageable
{
    void TakeDamage(int damage, Vector3 hit_direction, float knockback_force);
}