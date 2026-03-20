using System.Collections.Generic;
using UnityEngine;

// Grab / Smash の実行を担当するコンポーネント。
// 攻撃中は Palm を目標位置へ移動させ、攻撃ごとの有効時間だけ判定を有効化する。
// ヒット時は IDamageable を持つ相手へ TakeDamage を送る。
public sealed class EnemyAttackController : MonoBehaviour
{
    public enum EnemyAttackType
    {
        Grab,
        Smash
    }

    [Header("攻撃判定")]
    [Tooltip("攻撃中に有効化する 3D Collider です。Trigger を有効にして使用します。")]
    [SerializeField] private Collider m_attackHitbox;

    [Tooltip("攻撃判定用 Collider の Trigger 接触を通知する補助コンポーネントです。")]
    [SerializeField] private EnemyAttackHitboxNotifier m_hitboxNotifier;

    [Header("ダメージ設定")]
    [Tooltip("Grab 攻撃が命中した時に与えるダメージ量です。最大HPを超える値にすると即死級ダメージとして扱えます。")]
    [SerializeField] private int m_grabDamage = 999;

    [Tooltip("Smash 攻撃が命中した時に与えるダメージ量です。最大HPを超える値にすると即死級ダメージとして扱えます。")]
    [SerializeField] private int m_smashDamage = 999;

    [Tooltip("Grab 攻撃が命中した時に与えるノックバック強さです。掴み系なら 0 にしても構いません。")]
    [SerializeField] private float m_grabKnockbackForce = 0.0f;

    [Tooltip("Smash 攻撃が命中した時に与えるノックバック強さです。叩きつけ感を出したい場合は大きめに設定します。")]
    [SerializeField] private float m_smashKnockbackForce = 8.0f;

    [Tooltip("同じ攻撃中に同じ対象へ複数回ダメージを入れないようにするかを設定します。通常は ON を推奨します。")]
    [SerializeField] private bool m_preventMultipleHitsPerAttack = true;

    [Tooltip("攻撃ヒット時にデバッグログを出すかどうかを設定します。")]
    [SerializeField] private bool m_showAttackDebugLog = false;

    private EnemyConfig m_config;
    private Transform m_palm;

    // 起動時点の待機用ローカル座標を保持する。
    // 攻撃終了後はこの位置へ戻る前提で使用する。
    private Vector3 m_idleLocalPalmPosition;
    private bool m_hasIdleLocalPalmPosition;

    private Vector3 m_attackTargetLocalPosition;
    private float m_attackTimer;
    private bool m_isRunning;
    private bool m_hasLoggedReferenceWarnings;
    private EnemyAttackType m_currentAttackType;

    // 同一攻撃中の多段ヒットを防ぐために記録する。
    private readonly HashSet<IDamageable> m_hitTargetsInCurrentAttack = new();

    public bool IsRunning => m_isRunning;
    public EnemyAttackType CurrentAttackType => m_currentAttackType;

    public void SetConfig(EnemyConfig config)
    {
        m_config = config;
    }

    public void SetPalm(Transform palm)
    {
        m_palm = palm;

        if (m_palm != null)
        {
            m_idleLocalPalmPosition = m_palm.localPosition;
            m_hasIdleLocalPalmPosition = true;
        }
    }

    private void Awake()
    {
        if (m_hitboxNotifier == null && m_attackHitbox != null)
        {
            m_hitboxNotifier = m_attackHitbox.GetComponent<EnemyAttackHitboxNotifier>();
        }

        if (m_palm != null)
        {
            m_idleLocalPalmPosition = m_palm.localPosition;
            m_hasIdleLocalPalmPosition = true;
        }

        SetAttackHitboxEnabled(false);
    }

    private void OnEnable()
    {
        if (m_hitboxNotifier != null)
        {
            m_hitboxNotifier.Triggered -= OnAttackHitboxTriggered;
            m_hitboxNotifier.Triggered += OnAttackHitboxTriggered;
        }
    }

    private void Start()
    {
        WarnIfMissingCriticalReferences();
    }

    private void OnDisable()
    {
        if (m_hitboxNotifier != null)
        {
            m_hitboxNotifier.Triggered -= OnAttackHitboxTriggered;
        }

        SetAttackHitboxEnabled(false);
        m_isRunning = false;
        m_hitTargetsInCurrentAttack.Clear();
    }

    private void Update()
    {
        if (!m_isRunning)
        {
            return;
        }

        switch (m_currentAttackType)
        {
            case EnemyAttackType.Grab:
                TickGrabAttack(Time.deltaTime);
                break;

            case EnemyAttackType.Smash:
                TickSmashAttack(Time.deltaTime);
                break;
        }
    }

    // Grab 攻撃を開始する。
    // targetWorldPosition は 3D 空間上のワールド座標として扱う。
    public void BeginGrabAttack(Vector3 targetWorldPosition)
    {
        WarnIfMissingCriticalReferences();

        if (m_config == null || m_palm == null || !m_hasIdleLocalPalmPosition)
        {
            m_isRunning = false;
            SetAttackHitboxEnabled(false);
            return;
        }

        Transform parent = m_palm.parent;
        m_attackTargetLocalPosition = parent != null
            ? parent.InverseTransformPoint(targetWorldPosition)
            : targetWorldPosition;

        m_currentAttackType = EnemyAttackType.Grab;
        m_attackTimer = 0.0f;
        m_isRunning = true;
        m_hitTargetsInCurrentAttack.Clear();
        SetAttackHitboxEnabled(false);
    }

    // Smash 攻撃を開始する。
    // targetWorldPosition は 3D 空間上のワールド座標として扱う。
    public void BeginSmashAttack(Vector3 targetWorldPosition)
    {
        WarnIfMissingCriticalReferences();

        if (m_config == null || m_palm == null || !m_hasIdleLocalPalmPosition)
        {
            m_isRunning = false;
            SetAttackHitboxEnabled(false);
            return;
        }

        Transform parent = m_palm.parent;
        m_attackTargetLocalPosition = parent != null
            ? parent.InverseTransformPoint(targetWorldPosition)
            : targetWorldPosition;

        m_currentAttackType = EnemyAttackType.Smash;
        m_attackTimer = 0.0f;
        m_isRunning = true;
        m_hitTargetsInCurrentAttack.Clear();
        SetAttackHitboxEnabled(false);
    }

    // Grab 攻撃を時間経過で進行させる。
    // 前半で目標へ伸び、後半で待機位置へ戻る。
    private void TickGrabAttack(float deltaTime)
    {
        if (m_config == null || m_palm == null || !m_hasIdleLocalPalmPosition)
        {
            m_isRunning = false;
            SetAttackHitboxEnabled(false);
            return;
        }

        float duration = Mathf.Max(0.01f, m_config.GrabDuration);
        m_attackTimer += deltaTime;

        float normalizedTime = Mathf.Clamp01(m_attackTimer / duration);

        if (normalizedTime < 0.5f)
        {
            float t = normalizedTime / 0.5f;
            m_palm.localPosition = Vector3.Lerp(m_idleLocalPalmPosition, m_attackTargetLocalPosition, t);
        }
        else
        {
            float t = (normalizedTime - 0.5f) / 0.5f;
            m_palm.localPosition = Vector3.Lerp(m_attackTargetLocalPosition, m_idleLocalPalmPosition, t);
        }

        bool isActiveWindow =
            m_attackTimer >= m_config.GrabActiveStartTime &&
            m_attackTimer <= m_config.GrabActiveEndTime;

        SetAttackHitboxEnabled(isActiveWindow);

        if (m_attackTimer >= duration)
        {
            m_palm.localPosition = m_idleLocalPalmPosition;
            SetAttackHitboxEnabled(false);
            m_isRunning = false;
        }
    }

    // Smash 攻撃を時間経過で進行させる。
    // 前半で手を上に掲げ、中盤でターゲットへ叩きつけ、終盤で待機位置へ戻る。
    private void TickSmashAttack(float deltaTime)
    {
        if (m_config == null || m_palm == null || !m_hasIdleLocalPalmPosition)
        {
            m_isRunning = false;
            SetAttackHitboxEnabled(false);
            return;
        }

        float duration = Mathf.Max(0.01f, m_config.SmashDuration);
        m_attackTimer += deltaTime;

        float normalizedTime = Mathf.Clamp01(m_attackTimer / duration);

        // 手を上に掲げるための溜め位置。
        Vector3 raiseLocalPosition = m_idleLocalPalmPosition + m_config.SmashWindupOffset;

        if (normalizedTime < 0.30f)
        {
            // 前半: 待機位置から上方向へ掲げる
            float t = normalizedTime / 0.30f;
            m_palm.localPosition = Vector3.Lerp(m_idleLocalPalmPosition, raiseLocalPosition, t);
        }
        else if (normalizedTime < 0.60f)
        {
            // 中盤: 上からターゲットへ一気に叩きつける
            float t = (normalizedTime - 0.30f) / 0.30f;
            m_palm.localPosition = Vector3.Lerp(raiseLocalPosition, m_attackTargetLocalPosition, t);
        }
        else
        {
            // 後半: 攻撃後に待機位置へ戻る
            float t = (normalizedTime - 0.60f) / 0.40f;
            m_palm.localPosition = Vector3.Lerp(m_attackTargetLocalPosition, m_idleLocalPalmPosition, t);
        }

        bool isActiveWindow =
            m_attackTimer >= m_config.SmashActiveStartTime &&
            m_attackTimer <= m_config.SmashActiveEndTime;

        SetAttackHitboxEnabled(isActiveWindow);

        if (m_attackTimer >= duration)
        {
            m_palm.localPosition = m_idleLocalPalmPosition;
            SetAttackHitboxEnabled(false);
            m_isRunning = false;
        }
    }

    private void SetAttackHitboxEnabled(bool enabled)
    {
        if (m_attackHitbox != null)
        {
            m_attackHitbox.enabled = enabled;
        }
    }

    // 攻撃判定の Trigger 接触を受け取る。
    // 状態変化を受け取れる相手には EnemyAttackContext を渡し、
    // それ以外には通常の TakeDamage のみを送る。
    private void OnAttackHitboxTriggered(Collider other)
    {
        if (!m_isRunning)
        {
            return;
        }

        if (m_attackHitbox == null || !m_attackHitbox.enabled)
        {
            return;
        }

        IDamageable damageable = other.GetComponentInParent<IDamageable>();
        IEnemyAttackReceiver receiver = other.GetComponentInParent<IEnemyAttackReceiver>();

        if (damageable == null && receiver == null)
        {
            return;
        }

        // 多段ヒット防止は IDamageable を基準に判定する
        if (m_preventMultipleHitsPerAttack && damageable != null && m_hitTargetsInCurrentAttack.Contains(damageable))
        {
            return;
        }

        Vector3 hitDirection = CalculateHitDirection(other);
        int damage = GetDamageByCurrentAttack();
        float knockbackForce = GetKnockbackForceByCurrentAttack();

        EnemyAttackContext context = new EnemyAttackContext
        {
            AttackType = m_currentAttackType,
            Damage = damage,
            KnockbackForce = knockbackForce,
            HitDirection = hitDirection,
            HitPoint = other.ClosestPoint(m_palm != null ? m_palm.position : transform.position),
            GrabAnchor = m_currentAttackType == EnemyAttackType.Grab ? m_palm : null,
            Attacker = transform
        };

        // まず状態変化側へ通知する
        if (receiver != null)
        {
            receiver.ReceiveEnemyAttack(context);
        }
        // 状態変化を受け取れない相手には通常ダメージだけ送る
        else if (damageable != null)
        {
            damageable.TakeDamage(damage, hitDirection, knockbackForce);
        }

        if (m_preventMultipleHitsPerAttack && damageable != null)
        {
            m_hitTargetsInCurrentAttack.Add(damageable);
        }

        if (m_showAttackDebugLog)
        {
            Debug.Log(
                $"[EnemyAttackController] {m_currentAttackType} hit " +
                $"target={other.name}, damage={damage}, knockback={knockbackForce}, direction={hitDirection}, receiver={(receiver != null)}");
        }
    }

    // 現在の攻撃種類に応じたダメージ量を返す。
    private int GetDamageByCurrentAttack()
    {
        switch (m_currentAttackType)
        {
            case EnemyAttackType.Grab:
                return m_grabDamage;

            case EnemyAttackType.Smash:
                return m_smashDamage;

            default:
                return 1;
        }
    }

    // 現在の攻撃種類に応じたノックバック量を返す。
    private float GetKnockbackForceByCurrentAttack()
    {
        switch (m_currentAttackType)
        {
            case EnemyAttackType.Grab:
                return m_grabKnockbackForce;

            case EnemyAttackType.Smash:
                return m_smashKnockbackForce;

            default:
                return 0.0f;
        }
    }

    // ヒット方向を計算する。
    // 疑似3D横スクを想定し、Z成分は固定する。
    private Vector3 CalculateHitDirection(Collider other)
    {
        Vector3 attackerPos = m_palm != null ? m_palm.position : transform.position;
        Vector3 targetPos = other.bounds.center;

        Vector3 direction = (targetPos - attackerPos).normalized;
        direction.z = 0.0f;

        // 完全にゼロ方向になるのを避ける
        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = Vector3.right;
        }

        return direction.normalized;
    }

    // 重要参照の不足を一度だけ警告する。
    private void WarnIfMissingCriticalReferences()
    {
        if (m_hasLoggedReferenceWarnings)
        {
            return;
        }

        bool hasMissing = false;

        if (m_palm == null)
        {
            Debug.LogWarning("EnemyAttackController: m_palm が未設定です。攻撃先端の移動ができません。");
            hasMissing = true;
        }

        if (m_attackHitbox == null)
        {
            Debug.LogWarning("EnemyAttackController: m_attackHitbox が未設定です。3D攻撃判定を有効化できません。");
            hasMissing = true;
        }

        if (m_hitboxNotifier == null)
        {
            Debug.LogWarning("EnemyAttackController: m_hitboxNotifier が未設定です。3D Trigger 接触通知を受け取れません。");
            hasMissing = true;
        }

        if (m_config == null)
        {
            Debug.LogWarning("EnemyAttackController: EnemyConfig が未設定です。攻撃時間や判定時間を取得できません。");
            hasMissing = true;
        }

        m_hasLoggedReferenceWarnings = hasMissing;
    }
}