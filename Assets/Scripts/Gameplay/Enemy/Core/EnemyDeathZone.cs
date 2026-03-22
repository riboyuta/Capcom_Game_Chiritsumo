using UnityEngine;

// 左から迫る圧の即死ラインを表すクラス。
// WorldX 同期と接触時の仮通知のみを担当し、死亡処理の本実装には依存しない。
public sealed class EnemyDeathZone : MonoBehaviour
{
    [Header("判定設定")]
    [Tooltip("DeathZone が接触判定の対象とみなすタグです。通常は Player を指定します。")]
    [SerializeField] private string targetTag = "Player";

    [Header("デバッグ")]
    [Tooltip("有効にすると接触時に詳細ログを表示します。実装段階の確認用途です。")]
    [SerializeField] private bool showDebugLog;

    // 指定された WorldX に即死ラインを移動する。
    // Y/Z は既存配置を維持して、横方向だけ同期する。
    // EnemyChaseManager から呼び出され、全体圧の位置に連動させる。
    public void SetWorldX(float worldX)
    {
        Vector3 p = transform.position;
        p.x = worldX;
        transform.position = p;
    }

    // プレイヤー接触時に環境死（Hazard Death）を要求する。
    // PlayerController.RequestHazardDeath() を呼び出して即死処理を実行する。
    // Unity の 3D Trigger 接触時に自動で呼ばれるコールバック。
    private void OnTriggerEnter(Collider other)
    {
        HandlePlayerContact(other.gameObject);
    }

    // プレイヤーとの物理衝突時に環境死（Hazard Death）を要求する。
    // Player の Collider が Trigger でない場合はこちらが呼ばれる。
    private void OnCollisionEnter(Collision collision)
    {
        HandlePlayerContact(collision.gameObject);
    }

    // プレイヤー接触時の共通処理。
    // Trigger/Collision 両方から呼び出される。
    private void HandlePlayerContact(GameObject contactObject)
    {
        // 指定されたタグ以外は無視
        if (!contactObject.CompareTag(targetTag))
        {
            return;
        }

        // PlayerController を取得
        PlayerController player = contactObject.GetComponent<PlayerController>();
        if (player == null)
        {
            Debug.LogWarning($"[EnemyDeathZone] {contactObject.name} has target tag but no PlayerController", this);
            return;
        }

        // 環境死を要求
        bool deathAccepted = player.RequestHazardDeath();

        // デバッグログが有効なら詳細情報を出力
        if (showDebugLog)
        {
            Debug.Log($"[EnemyDeathZone] {contactObject.name} requested hazard death (accepted={deathAccepted})", this);
        }
    }
}
