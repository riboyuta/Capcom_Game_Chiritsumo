using UnityEngine;

// 敵攻撃がプレイヤーへ渡す情報をまとめた構造体。
// EnemyAttackController から IEnemyAttackReceiver へ渡される。
public struct EnemyAttackContext
{
    // 攻撃の種類（Grab / Smash）
    public EnemyAttackController.EnemyAttackType AttackType;
    // 与えるダメージ量
    public int Damage;
    // ノックバックの強さ
    public float KnockbackForce;
    // ヒット方向（正規化されたベクトル）
    public Vector3 HitDirection;
    // ヒット位置（ワールド座標）
    public Vector3 HitPoint;
    // Grab 攻撃の場合、掴んでいる手の Transform（Grab 以外は null）
    public Transform GrabAnchor;
    // 攻撃元の Transform（敵の手のルート Transform など）
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