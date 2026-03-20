using System;
using UnityEngine;

// EnemyAttackController へ 3D Trigger 接触を通知する補助コンポーネント。
// 攻撃判定用の Collider に付与して使用する。
public sealed class EnemyAttackHitboxNotifier : MonoBehaviour
{
    [Header("デバッグ")]
    [Tooltip("有効にすると、攻撃判定が何に接触したかを日本語ログで確認できます。")]
    [SerializeField] private bool m_showDebugLog;

    // Trigger 接触時に相手の Collider を通知する。
    public event Action<Collider> Triggered;

    private void OnTriggerEnter(Collider other)
    {
        if (m_showDebugLog)
        {
            Debug.Log($"[EnemyAttackHitboxNotifier] {other.name} と接触しました。", this);
        }

        Triggered?.Invoke(other);
    }
}
