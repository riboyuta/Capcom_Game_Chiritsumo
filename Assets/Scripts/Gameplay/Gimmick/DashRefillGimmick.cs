using UnityEngine;

// ダッシュ（ステップ）回復ギミック。
// プレイヤーが触れるとダッシュ残数を全回復する。
// 使用後は一定時間クールダウンし、再度使用可能になる。
// Celeste のダッシュ回復クリスタルと同様の挙動。
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class DashRefillGimmick : MonoBehaviour, IRespawnResettable
{
    [Header("クールダウン設定")]
    [Tooltip("回復後、再び使えるようになるまでの時間（秒）。0 にすると一度きり使用。")]
    [SerializeField] private float cooldownDuration = 2.5f;

    [Header("見た目")]
    [Tooltip("ギミック本体の見た目 Transform。OFF 時に非表示にします。未指定時は子オブジェクトの Renderer を使用します。")]
    [SerializeField] private Transform visualTransform;

    // 内部状態
    private Collider myCollider;
    private Renderer[] visualRenderers;
    private bool isAvailable = true;
    private float cooldownTimer;
    private bool initialIsAvailable;
    private float initialCooldownTimer;
    private bool initialColliderEnabled;
    private bool[] initialRendererEnabledStates;

    // IRespawnResettable 用
    private bool hasCapturedInitialState;

    private void Awake()
    {
        EnsureRuntimeReferences();

        // トリガー化を保証する
        if (myCollider != null)
        {
            myCollider.isTrigger = true;
        }

        // MeshRenderer のソーティングを設定する
        foreach (var r in GetComponentsInChildren<Renderer>())
        {
            r.sortingLayerName = "UseGimmick";
            r.sortingOrder = 0;
        }
    }

    private void EnsureRuntimeReferences()
    {
        if (myCollider == null)
        {
            myCollider = GetComponent<Collider>();
        }

        if (visualTransform == null)
        {
            visualTransform = transform.childCount > 0 ? transform.GetChild(0) : transform;
        }

        if (visualRenderers == null)
        {
            visualRenderers = visualTransform != null
                ? visualTransform.GetComponentsInChildren<Renderer>(true)
                : new Renderer[0];
        }
    }

    private void Update()
    {
        if (isAvailable) return;

        // クールダウンが 0 以下なら一度きりの使い切り扱い（復活しない）
        if (cooldownDuration <= 0f) return;

        cooldownTimer -= Time.deltaTime;
        if (cooldownTimer <= 0f)
        {
            Reactivate();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        TryRefillFromCollider(other);
    }

    // アイテムと重なった状態でダッシュを使った場合も回収できるよう Stay でも判定する。
    private void OnTriggerStay(Collider other)
    {
        TryRefillFromCollider(other);
    }

    // コライダーに触れているプレイヤーへのダッシュ回復を試みる共通処理。
    private void TryRefillFromCollider(Collider other)
    {
        if (!isAvailable) return;
        if (!other.CompareTag("Player")) return;

        // PlayerFacade を取得してダッシュ回復を試みる
        var facade = other.GetComponent<PlayerFacade>();
        if (facade == null)
        {
            facade = other.GetComponentInParent<PlayerFacade>();
        }

        if (facade == null) return;

        // 既にダッシュが満タンなら消費しない
        if (!facade.TryRefillDash(DashRefillReason.Gimmick)) return;

        Consume();
    }

    // ギミックを消費状態にする。
    private void Consume()
    {
        isAvailable = false;
        cooldownTimer = cooldownDuration;

        // 判定を無効化する
        if (myCollider != null) myCollider.enabled = false;

        // 見た目を消す
        SetVisualActive(false);

        // SE: 回復音
        AudioEvent.Emit(this, "Consumed");
    }

    // ギミックを再びアクティブにする。
    private void Reactivate()
    {
        isAvailable = true;
        cooldownTimer = 0f;

        if (myCollider != null) myCollider.enabled = true;
        SetVisualActive(true);
    }

    private void SetVisualActive(bool active)
    {
        if (visualRenderers == null) return;

        foreach (var r in visualRenderers)
        {
            if (r != null) r.enabled = active;
        }
    }

    private void CaptureRendererInitialStates()
    {
        if (visualRenderers == null)
        {
            initialRendererEnabledStates = null;
            return;
        }

        initialRendererEnabledStates = new bool[visualRenderers.Length];
        for (int i = 0; i < visualRenderers.Length; i++)
        {
            initialRendererEnabledStates[i] = visualRenderers[i] != null && visualRenderers[i].enabled;
        }
    }

    private void RestoreRendererInitialStates()
    {
        if (visualRenderers == null || initialRendererEnabledStates == null) return;

        int count = Mathf.Min(visualRenderers.Length, initialRendererEnabledStates.Length);
        for (int i = 0; i < count; i++)
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
        if (hasCapturedInitialState) return;

        EnsureRuntimeReferences();

        initialIsAvailable = isAvailable;
        initialCooldownTimer = cooldownTimer;
        initialColliderEnabled = myCollider != null && myCollider.enabled;
        CaptureRendererInitialStates();

        hasCapturedInitialState = true;
    }

    public void ResetToRespawnState()
    {
        // リスポーン時は使用可否も初期キャプチャ状態へ戻す。
        if (!hasCapturedInitialState)
        {
            CaptureInitialState();
        }

        // 死亡復帰では常に再有効化せず、キャプチャした初期状態へ戻す。
        isAvailable = initialIsAvailable;
        cooldownTimer = initialCooldownTimer;

        if (myCollider != null)
        {
            myCollider.enabled = initialColliderEnabled;
        }

        RestoreRendererInitialStates();
    }
}
