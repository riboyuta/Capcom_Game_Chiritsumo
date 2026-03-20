using System;
using UnityEngine;

// EnemyAttackController へ 3D Trigger 接触を通知する補助コンポーネント。
// 攻撃判定用の Collider に付与して使用する。
public sealed class EnemyAttackHitboxNotifier : MonoBehaviour
{
    [Header("デバッグ")]
    [Tooltip("有効にすると、攻撃判定が何に接触したかを日本語ログで確認できます。")]
    [SerializeField] private bool m_showDebugLog;

    // Trigger 接触時に相手の Collider を通知するイベント。
    // EnemyAttackController がこのイベントを購読して、接触判定を処理する。
    public event Action<Collider> Triggered;

    // Unity の 3D Trigger 接触時に自動的に呼ばれるコールバック。
    // 接触した相手の Collider を Triggered イベントで通知する。
    private void OnTriggerEnter(Collider other)
    {
        // デバッグログが有効なら接触情報を出力
        if (m_showDebugLog)
        {
            Debug.Log($"[EnemyAttackHitboxNotifier] {other.name} と接触しました。", this);
        }

        // イベント購読者に接触した Collider を通知
        Triggered?.Invoke(other);
    }
}
