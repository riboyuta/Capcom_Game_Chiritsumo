using UnityEngine;

// 特定エリア侵入で ShadowChaserEnemy を有効化するトリガー。
// トリガーごとに別のスポーン位置を持てる。
// StageResetSystem からは IRespawnResettable 経由で未使用状態へ戻される。
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public sealed class ShadowChaserActivator : MonoBehaviour, IRespawnResettable
{
    [Header("参照")]
    [Tooltip("起動対象の ShadowChaserEnemy です。")]
    [SerializeField] private ShadowChaserEnemy targetEnemy;

    [Tooltip("このトリガーから起動した時のスポーン位置です。未設定時はこのトリガー自身の Transform を使います。")]
    [SerializeField] private Transform spawnPoint;

    [Header("挙動")]
    [Tooltip("一度起動したらこのトリガーを無効化するかです。")]
    [SerializeField] private bool oneShot = true;

    [Tooltip("Player タグで判定するかです。")]
    [SerializeField] private bool usePlayerTag = true;

    [Tooltip("Player タグ判定に使うタグ名です。")]
    [SerializeField] private string playerTag = "Player";

    private Collider triggerCollider;
    private bool hasTriggered = false;

    // Respawn 用に保存する初期状態
    private bool hasCapturedInitialState;
    private bool initialEnabled;
    private bool initialColliderEnabled;
    private bool initialHasTriggered;

    // 初期化処理。
    // Collider を Trigger として設定し、スポーン位置の初期化を行う。
    private void Awake()
    {
        triggerCollider = GetComponent<Collider>();
        triggerCollider.isTrigger = true;

        // スポーン位置が未設定の場合は自身の Transform を使用
        if (spawnPoint == null)
        {
            spawnPoint = transform;
        }
    }

    // トリガーに何かが侵入した時の処理。
    // プレイヤーが侵入したら ShadowChaserEnemy を起動する。
    private void OnTriggerEnter(Collider other)
    {
        // ターゲットの敵が未設定なら何もしない
        if (targetEnemy == null)
        {
            return;
        }

        // プレイヤーでなければ無視
        if (!IsPlayer(other))
        {
            return;
        }

        // oneShot モードで既に発動済みなら無視
        if (hasTriggered && oneShot)
        {
            return;
        }

        // トリガー発動フラグを立てる
        hasTriggered = true;

        // スポーン要求を作成し、敵を起動
        ShadowChaserSpawnRequest request = new ShadowChaserSpawnRequest(
            spawnPoint.position,
            spawnPoint.rotation);

        targetEnemy.Activate(request);

        // oneShot モードなら、このトリガーを無効化
        if (oneShot)
        {
            enabled = false;

            if (triggerCollider != null)
            {
                triggerCollider.enabled = false;
            }
        }
    }

    // Respawn システム用：初期状態をキャプチャする。
    // リスポーン時にこの状態に戻すことができる。
    public void CaptureInitialState()
    {
        // 既にキャプチャ済みなら何もしない
        if (hasCapturedInitialState)
        {
            return;
        }

        if (triggerCollider == null)
        {
            triggerCollider = GetComponent<Collider>();
        }

        // 初期状態を保存
        initialEnabled = enabled;
        initialColliderEnabled = triggerCollider != null && triggerCollider.enabled;
        initialHasTriggered = hasTriggered;

        hasCapturedInitialState = true;
    }

    // Respawn システム用：キャプチャした初期状態にリセットする。
    // キャプチャしていない場合はデフォルトの状態にリセットする。
    public void ResetToRespawnState()
    {
        if (triggerCollider == null)
        {
            triggerCollider = GetComponent<Collider>();
        }

        // 初期状態がキャプチャされている場合はそれを復元
        if (hasCapturedInitialState)
        {
            enabled = initialEnabled;
            hasTriggered = initialHasTriggered;

            if (triggerCollider != null)
            {
                triggerCollider.enabled = initialColliderEnabled;
                triggerCollider.isTrigger = true;
            }
        }
        else
        {
            // キャプチャされていない場合はデフォルトの状態に
            hasTriggered = false;
            enabled = true;

            if (triggerCollider != null)
            {
                triggerCollider.enabled = true;
                triggerCollider.isTrigger = true;
            }
        }
    }

    // プレイヤーかどうかを判定する。
    // タグ判定と PlayerController コンポーネントの有無で判定する。
    private bool IsPlayer(Collider other)
    {
        if (other == null)
        {
            return false;
        }

        // タグ判定が有効な場合はタグで判定
        if (usePlayerTag && other.CompareTag(playerTag))
        {
            return true;
        }

        // タグが無い場合は PlayerController コンポーネントの有無で判定
        PlayerController player = other.GetComponentInParent<PlayerController>();
        return player != null;
    }
}