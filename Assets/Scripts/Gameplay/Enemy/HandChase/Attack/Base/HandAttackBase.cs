using System;
using UnityEngine;

/// <summary>
/// Hand系攻撃の共通基底クラス。
/// Rigidbody初期化、完了処理、キャンセル処理などの共通ロジックを提供する。
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public abstract class HandAttackBase : MonoBehaviour, IHandAttack
{
    protected Rigidbody rb;
    protected Transform targetPlayer;
    protected Action onFinished;
    protected bool isFinished;

    public bool IsFinished => isFinished;

    protected void InitializeRigidbody()
    {
        rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.useGravity = false;
            rb.isKinematic = true;
        }
    }

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

    protected void RequestPlayerDeath(GameObject playerObject)
    {
        if (playerObject == null)
        {
            return;
        }

        PlayerController playerController = playerObject.GetComponent<PlayerController>();
        if (playerController != null)
        {
            playerController.RequestDamageDeath();
            return;
        }

        Debug.LogWarning($"{GetType().Name}: PlayerController が見つからないため死亡要求を送れませんでした。", playerObject);
    }
}
