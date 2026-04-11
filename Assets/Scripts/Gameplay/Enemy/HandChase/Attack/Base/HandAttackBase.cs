using System;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public abstract class HandAttackBase : MonoBehaviour, IHandAttack
{
    protected Rigidbody rb;
    protected Transform targetPlayer;
    protected Action onFinished;
    protected bool isFinished;

    public bool IsFinished => isFinished;

    // Rigidbodyの初期設定（重力無し、Kinematic）
    protected void InitializeRigidbody()
    {
        rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.useGravity = false;
            rb.isKinematic = true;
        }
    }

    // 攻撃を強制的にキャンセル
    public virtual void Cancel()
    {
        if (isFinished)
        {
            return;
        }

        isFinished = true;
        onFinished?.Invoke();
        Destroy(gameObject);
    }

    // 攻撃を正常終了
    protected void FinishAttack()
    {
        if (isFinished)
        {
            return;
        }

        isFinished = true;
        onFinished?.Invoke();
        Destroy(gameObject);
    }

    // プレイヤーに死亡ダメージを要求
    protected void RequestPlayerDeath(GameObject playerObject)
    {
        if (playerObject == null)
        {
            return;
        }

        // PlayerControllerを取得して死亡をリクエスト
        PlayerController playerController = playerObject.GetComponent<PlayerController>();
        if (playerController != null)
        {
            playerController.RequestDamageDeath();
            return;
        }

        Debug.LogWarning($"{GetType().Name}: PlayerController が見つからないため死亡要求を送れませんでした。", playerObject);
    }
}
