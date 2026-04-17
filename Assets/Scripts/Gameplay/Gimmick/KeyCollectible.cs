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

    private bool initialIsCollected;
    private bool initialColliderEnabled;
    private bool[] initialRendererEnabledStates;

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

        visualRenderers = visualTransform != null
            ? visualTransform.GetComponentsInChildren<Renderer>(true)
            : new Renderer[0];

        if (visualTransform == null)
        {
            Debug.LogWarning($"[{nameof(KeyCollectible)}] visualTransform が解決できませんでした: {name}", this);
        }
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
        for (int i = 0; i < visualRenderers.Length; i++)
        {
            if (visualRenderers[i] != null)
            {
                visualRenderers[i].enabled = false;
            }
        }

        // マネージャーへ通知
        if (manager != null)
        {
            manager.NotifyKeyCollected();
        }
        else
        {
            Debug.LogWarning($"[{nameof(KeyCollectible)}] KeyManager が未設定です: {name}", this);
        }

        // SE: 取得したときの音
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayOverlap("SFX_gimmick_push_switch"); // ※仮の音を設定
        }
    }

    // Collider と Renderer の初期状態を含めて保存する
    private void CaptureVisualAndColliderInitialState()
    {
        initialIsCollected = isCollected;
        initialColliderEnabled = myCollider != null && myCollider.enabled;

        initialRendererEnabledStates = new bool[visualRenderers.Length];
        for (int i = 0; i < visualRenderers.Length; i++)
        {
            initialRendererEnabledStates[i] = visualRenderers[i] != null && visualRenderers[i].enabled;
        }
    }

    // 保存した初期状態へ復元する
    private void RestoreVisualAndColliderInitialState()
    {
        isCollected = initialIsCollected;

        if (myCollider != null)
        {
            myCollider.enabled = initialColliderEnabled;
        }

        int restoreCount = Mathf.Min(visualRenderers.Length, initialRendererEnabledStates != null ? initialRendererEnabledStates.Length : 0);
        for (int i = 0; i < restoreCount; i++)
        {
            if (visualRenderers[i] != null)
            {
                visualRenderers[i].enabled = initialRendererEnabledStates[i];
            }
        }
    }

    // ──────────────────────────────────────────────
    // IRespawnResettable
    // ──────────────────────────────────────────────

    public void CaptureInitialState()
    {
        if (hasCapturedInitialState)
        {
            return;
        }

        CaptureVisualAndColliderInitialState();
        hasCapturedInitialState = true;
    }

    public void ResetToRespawnState()
    {
        if (!hasCapturedInitialState)
        {
            CaptureInitialState();
        }

        RestoreVisualAndColliderInitialState();
    }
}
