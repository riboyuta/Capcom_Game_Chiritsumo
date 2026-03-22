using UnityEngine;

// 責務:
// - 3D 空間で一定方向へ単純移動する
// - Player タグとの Trigger 接触を検知し、PlayerController の既存死亡入口を呼ぶ
// - 腕 / 手の平の 2 レイヤー構成で簡易 Sprite アニメを再生する
// - 全体 / 腕 / 手の平 の見た目位置とスケールを Inspector から調整できる
//
// 非責務:
// - 敵 AI や経路探索
// - ダメージ計算や死亡可否判定そのもの
// - Sprite 生成やアセット読込
//
// 前提:
// - 3D Trigger 判定が機能する構成である
// - Player は playerTag で識別できる
// - PlayerController は接触先自身または親階層から取得できる
// - 座標系は X=左右, Y=上下, Z=奥行き を想定
[DisallowMultipleComponent]
public sealed class EnemySimpleHand3D : MonoBehaviour
{
    // =====================================================================
    // Inspector 設定値
    // =====================================================================

    [Header("移動")]
    [SerializeField, Min(0f)] private float moveSpeed = 2.0f;
    [SerializeField] private Vector3 moveAxis = Vector3.right;

    [Header("判定")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool disableAfterHit = false;

    [Header("見た目参照")]
    [SerializeField] private Transform visualRoot;
    [SerializeField] private SpriteRenderer armRenderer;
    [SerializeField] private SpriteRenderer palmRenderer;

    [Header("見た目: 全体調整")]
    [SerializeField] private Vector3 visualRootLocalOffset = Vector3.zero;
    [SerializeField] private Vector3 visualRootLocalScale = Vector3.one;

    [Header("見た目: 腕調整")]
    [SerializeField] private Vector3 armLocalOffset = Vector3.zero;
    [SerializeField] private Vector3 armLocalScale = Vector3.one;

    [Header("見た目: 手の平調整")]
    [SerializeField] private Vector3 palmLocalOffset = Vector3.zero;
    [SerializeField] private Vector3 palmLocalScale = Vector3.one;

    [Header("アニメ")]
    [SerializeField] private Sprite[] armFrames;
    [SerializeField] private Sprite[] palmFrames;
    [SerializeField, Min(0f)] private float animationFps = 8.0f;

    [Header("デバッグ")]
    [SerializeField] private bool enableDebugLog;
    [SerializeField] private bool logMissingReferences = true;

    // =====================================================================
    // 実行時状態
    // =====================================================================

    private Rigidbody rb;
    private bool isDisabled;
    private float animationTimer;

    // =====================================================================
    // 初期化 / 検証
    // =====================================================================

    private void Reset()
    {
        visualRoot = transform;
        rb = GetComponent<Rigidbody>();
        TryAutoAssignRenderers();
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        if (visualRoot == null)
        {
            visualRoot = transform;
        }

        TryAutoAssignRenderers();

        if (rb == null && logMissingReferences)
        {
            Debug.LogWarning("[EnemySimpleHand3D] Rigidbody がありません。移動は行われません。", this);
        }
    }

    private void OnValidate()
    {
        moveSpeed = Mathf.Max(0f, moveSpeed);
        animationFps = Mathf.Max(0f, animationFps);

        if (visualRootLocalScale.x == 0f) visualRootLocalScale.x = 1f;
        if (visualRootLocalScale.y == 0f) visualRootLocalScale.y = 1f;
        if (visualRootLocalScale.z == 0f) visualRootLocalScale.z = 1f;

        if (armLocalScale.x == 0f) armLocalScale.x = 1f;
        if (armLocalScale.y == 0f) armLocalScale.y = 1f;
        if (armLocalScale.z == 0f) armLocalScale.z = 1f;

        if (palmLocalScale.x == 0f) palmLocalScale.x = 1f;
        if (palmLocalScale.y == 0f) palmLocalScale.y = 1f;
        if (palmLocalScale.z == 0f) palmLocalScale.z = 1f;

        if (visualRoot == null)
        {
            visualRoot = transform;
        }

        if (moveAxis.sqrMagnitude > 0f)
        {
            moveAxis = moveAxis.normalized;
        }

        TryAutoAssignRenderers();
    }

    // =====================================================================
    // 毎フレーム更新
    // =====================================================================

    private void FixedUpdate()
    {
        if (isDisabled || rb == null)
        {
            return;
        }

        Vector3 next = rb.position + moveAxis * (moveSpeed * Time.fixedDeltaTime);
        rb.MovePosition(next);
    }

    private void Update()
    {
        if (isDisabled)
        {
            return;
        }

        UpdateSimpleAnimation();
        ApplyVisualLayout();
    }

    // =====================================================================
    // Trigger 判定
    // =====================================================================

    private void OnTriggerEnter(Collider other)
    {
        if (isDisabled)
        {
            return;
        }

        if (!other.CompareTag(playerTag))
        {
            return;
        }

        PlayerController player = other.GetComponent<PlayerController>();
        if (player == null)
        {
            player = other.GetComponentInParent<PlayerController>();
        }

        if (player == null)
        {
            if (logMissingReferences)
            {
                Debug.LogWarning($"[EnemySimpleHand3D] {other.name} has tag '{playerTag}' but no PlayerController.", this);
            }
            return;
        }

        bool accepted = player.RequestDamageDeath();

        if (!accepted)
        {
            if (enableDebugLog)
            {
                Debug.Log($"[EnemySimpleHand3D] RequestDamageDeath rejected for '{other.name}'.", this);
            }
            return;
        }

        if (enableDebugLog)
        {
            Debug.Log($"[EnemySimpleHand3D] Hit player '{other.name}', RequestDamageDeath accepted=true", this);
        }

        if (disableAfterHit)
        {
            DisableSelf();
        }
    }

    // =====================================================================
    // 無効化処理
    // =====================================================================

    private void DisableSelf()
    {
        isDisabled = true;

        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.enabled = false;
        }

        gameObject.SetActive(false);
    }

    // =====================================================================
    // 簡易 Sprite アニメ
    // =====================================================================

    private void UpdateSimpleAnimation()
    {
        if (animationFps <= 0f)
        {
            ApplyFrame(armRenderer, armFrames, 0);
            ApplyFrame(palmRenderer, palmFrames, 0);
            return;
        }

        animationTimer += Time.deltaTime;

        int armIndex = GetLoopFrameIndex(armFrames, animationTimer, animationFps);
        int palmIndex = GetLoopFrameIndex(palmFrames, animationTimer, animationFps);

        ApplyFrame(armRenderer, armFrames, armIndex);
        ApplyFrame(palmRenderer, palmFrames, palmIndex);
    }

    private static int GetLoopFrameIndex(Sprite[] frames, float time, float fps)
    {
        if (frames == null || frames.Length == 0)
        {
            return -1;
        }

        int index = Mathf.FloorToInt(time * fps) % frames.Length;
        if (index < 0)
        {
            index += frames.Length;
        }

        return index;
    }

    private static void ApplyFrame(SpriteRenderer target, Sprite[] frames, int index)
    {
        if (target == null || frames == null || frames.Length == 0)
        {
            return;
        }

        if (index < 0 || index >= frames.Length)
        {
            return;
        }

        target.sprite = frames[index];
    }

    // =====================================================================
    // 見た目調整
    // =====================================================================

    private void ApplyVisualLayout()
    {
        if (visualRoot != null)
        {
            visualRoot.localPosition = visualRootLocalOffset;
            visualRoot.localScale = visualRootLocalScale;
        }

        if (armRenderer != null)
        {
            armRenderer.transform.localPosition = armLocalOffset;
            armRenderer.transform.localScale = armLocalScale;
        }

        if (palmRenderer != null)
        {
            palmRenderer.transform.localPosition = palmLocalOffset;
            palmRenderer.transform.localScale = palmLocalScale;
        }
    }

    // =====================================================================
    // 参照補完
    // =====================================================================

    private void TryAutoAssignRenderers()
    {
        if (visualRoot == null)
        {
            return;
        }

        if (armRenderer == null)
        {
            armRenderer = visualRoot.Find("ArmRenderer")?.GetComponent<SpriteRenderer>();
        }

        if (palmRenderer == null)
        {
            palmRenderer = visualRoot.Find("PalmRenderer")?.GetComponent<SpriteRenderer>();
        }
    }
}