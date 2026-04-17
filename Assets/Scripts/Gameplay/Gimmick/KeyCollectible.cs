using UnityEngine;

// 個別の鍵ギミック。
// プレイヤーが接触すると取得済みとなり、KeyManager に通知する。
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class KeyCollectible : MonoBehaviour, IRespawnResettable
{
    [Header("見た目（ビジュアル）")]
    [Tooltip("取得時に非表示にする見た目の Transform。未指定時は自身の階層の Renderer を使用します。")]
    [SerializeField] private Transform visualTransform;

    private Collider myCollider;
    private Renderer[] visualRenderers;
    private KeyManager manager;
    private bool isCollected = false;
    private bool hasCapturedInitialState;

    public void Initialize(KeyManager m)
    {
        manager = m;
    }

    private void Awake()
    {
        // 念のためトリガーにしておく
        myCollider = GetComponent<Collider>();
        if (myCollider != null)
        {
            myCollider.isTrigger = true;
        }

        if (visualTransform == null)
        {
            visualTransform = transform.childCount > 0 ? transform.GetChild(0) : transform;
        }
        
        visualRenderers = visualTransform.GetComponentsInChildren<Renderer>();
    }

    private void OnTriggerEnter(Collider other)
    {
        // 既に取得済みなら無視する
        if (isCollected) return;
        
        // Player 以外は無視する
        if (!other.CompareTag("Player")) return;

        GetCollected();
    }

    private void GetCollected()
    {
        isCollected = true;
        
        // 判定と見た目を無効化
        if (myCollider != null) myCollider.enabled = false;
        foreach (var r in visualRenderers)
        {
            if (r != null) r.enabled = false;
        }

        // マネージャーへ通知
        if (manager != null)
        {
            manager.NotifyKeyCollected();
        }

        // SE: 取得したときの音
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayOverlap("SFX_gimmick_push_switch"); // ※仮の音を設定
        }
    }

    // ──────────────────────────────────────────────
    // IRespawnResettable
    // ──────────────────────────────────────────────

    public void CaptureInitialState()
    {
        if (hasCapturedInitialState) return;
        hasCapturedInitialState = true;
    }

    public void ResetToRespawnState()
    {
        // 死亡リセット時は未取得状態に戻し、判定・見た目を有効化する
        isCollected = false;
        
        if (myCollider != null) myCollider.enabled = true;
        foreach (var r in visualRenderers)
        {
            if (r != null) r.enabled = true;
        }
    }
}
