using UnityEngine;

// 左から迫る圧の即死ラインを表すクラス。
// WorldX 同期と接触時の仮通知のみを担当し、死亡処理の本実装には依存しない。
public sealed class EnemyDeathZone : MonoBehaviour
{
    [Header("判定設定")]
    [Tooltip("DeathZone が接触判定の対象とみなすタグです。通常は Player を指定します。")]
    [SerializeField] private string m_targetTag = "Player";

    [Header("デバッグ")]
    [Tooltip("有効にすると接触時に詳細ログを表示します。実装段階の確認用途です。")]
    [SerializeField] private bool m_showDebugLog;

    // 指定された WorldX に即死ラインを移動する。
    // Y/Z は既存配置を維持して、横方向だけ同期する。
    // EnemyChaseManager から呼び出され、全体圧の位置に連動させる。
    public void SetWorldX(float worldX)
    {
        Vector3 p = transform.position;
        p.x = worldX;
        transform.position = p;
    }

    // プレイヤー接触時は仮通知のみ行う。
    // 外部の PlayerHealth や GameManager はここでは直接呼ばない。
    // Unity の 3D Trigger 接触時に自動で呼ばれるコールバック。
    private void OnTriggerEnter(Collider other)
    {
        // 指定されたタグ以外は無視
        if (!other.CompareTag(m_targetTag))
        {
            return;
        }

        // 接触を警告ログで通知（実際の死亡処理は別途実装予定）
        Debug.LogWarning($"[EnemyDeathZone] {other.name} が即死ラインへ接触しました。死亡処理は未接続です。", this);

        // デバッグログが有効なら詳細情報を出力
        if (m_showDebugLog)
        {
            Debug.Log($"[EnemyDeathZone] 接触タグ={other.tag}", this);
        }
    }
}
