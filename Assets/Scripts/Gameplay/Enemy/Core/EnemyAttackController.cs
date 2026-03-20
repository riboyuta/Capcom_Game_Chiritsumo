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

    // 敵の設定データへの参照
    private EnemyConfig m_config;
    // 攻撃先端として動かす Transform への参照
    private Transform m_palm;

    // 起動時点の待機用ローカル座標を保持する。
    // 攻撃終了後はこの位置へ戻る前提で使用する。
    private Vector3 m_idleLocalPalmPosition;
    // 待機位置が正常に取得できたかを記録するフラグ
    private bool m_hasIdleLocalPalmPosition;

    // 現在実行中の攻撃のターゲット位置（ローカル座標）
    private Vector3 m_attackTargetLocalPosition;
    // 攻撃開始からの経過時間
    private float m_attackTimer;
    // 攻撃実行中フラグ
    private bool m_isRunning;
    // 参照不足警告を出したかどうかのフラグ（重複警告を防ぐ）
    private bool m_hasLoggedReferenceWarnings;
    // 現在実行中の攻撃種類
    private EnemyAttackType m_currentAttackType;

    // 同一攻撃中の多段ヒットを防ぐために記録する。
    private readonly HashSet<IDamageable> m_hitTargetsInCurrentAttack = new();

    public bool IsRunning => m_isRunning;
    public EnemyAttackType CurrentAttackType => m_currentAttackType;

    // 敵設定データを外部から設定する
    public void SetConfig(EnemyConfig config)
    {
        m_config = config;
    }

    // Palm Transform を外部から設定し、待機位置を記録する
    public void SetPalm(Transform palm)
    {
        m_palm = palm;

        // Palm が有効なら現在のローカル座標を待機位置として記録
        if (m_palm != null)
        {
            m_idleLocalPalmPosition = m_palm.localPosition;
            m_hasIdleLocalPalmPosition = true;
        }
    }

    private void Awake()
    {
        // Notifier が未設定なら Hitbox から自動取得を試みる
        if (m_hitboxNotifier == null && m_attackHitbox != null)
        {
            m_hitboxNotifier = m_attackHitbox.GetComponent<EnemyAttackHitboxNotifier>();
        }

        // Palm が設定済みなら待機位置を記録
        if (m_palm != null)
        {
            m_idleLocalPalmPosition = m_palm.localPosition;
            m_hasIdleLocalPalmPosition = true;
        }

        // 初期状態では攻撃判定を無効化
        SetAttackHitboxEnabled(false);
    }

    private void OnEnable()
    {
        // ヒットボックス通知イベントを登録（二重登録を防ぐため先に解除）
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
        // イベント購読を解除
        if (m_hitboxNotifier != null)
        {
            m_hitboxNotifier.Triggered -= OnAttackHitboxTriggered;
        }

        // 攻撃状態をクリーンアップ
        SetAttackHitboxEnabled(false);
        m_isRunning = false;
        m_hitTargetsInCurrentAttack.Clear();
    }

    private void Update()
    {
        // 攻撃実行中でなければ何もしない
        if (!m_isRunning)
        {
            return;
        }

        // 現在の攻撃種類に応じた更新処理を実行
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
        // 必要な参照が不足していないか確認
        WarnIfMissingCriticalReferences();

        // 必須参照のいずれかが不足していれば攻撃を開始しない
        if (m_config == null || m_palm == null || !m_hasIdleLocalPalmPosition)
        {
            m_isRunning = false;
            SetAttackHitboxEnabled(false);
            return;
        }

        // ワールド座標をローカル座標に変換（親がいない場合はそのまま使用）
        Transform parent = m_palm.parent;
        m_attackTargetLocalPosition = parent != null
            ? parent.InverseTransformPoint(targetWorldPosition)
            : targetWorldPosition;

        // 攻撃パラメータを初期化
        m_currentAttackType = EnemyAttackType.Grab;
        m_attackTimer = 0.0f;
        m_isRunning = true;
        m_hitTargetsInCurrentAttack.Clear();
        // 攻撃開始時点では判定を無効化（有効時間に入ったら有効化）
        SetAttackHitboxEnabled(false);
    }

    // Smash 攻撃を開始する。
    // targetWorldPosition は 3D 空間上のワールド座標として扱う。
    public void BeginSmashAttack(Vector3 targetWorldPosition)
    {
        // 必要な参照が不足していないか確認
        WarnIfMissingCriticalReferences();

        // 必須参照のいずれかが不足していれば攻撃を開始しない
        if (m_config == null || m_palm == null || !m_hasIdleLocalPalmPosition)
        {
            m_isRunning = false;
            SetAttackHitboxEnabled(false);
            return;
        }

        // ワールド座標をローカル座標に変換（親がいない場合はそのまま使用）
        Transform parent = m_palm.parent;
        m_attackTargetLocalPosition = parent != null
            ? parent.InverseTransformPoint(targetWorldPosition)
            : targetWorldPosition;

        // 攻撃パラメータを初期化
        m_currentAttackType = EnemyAttackType.Smash;
        m_attackTimer = 0.0f;
        m_isRunning = true;
        m_hitTargetsInCurrentAttack.Clear();
        // 攻撃開始時点では判定を無効化（有効時間に入ったら有効化）
        SetAttackHitboxEnabled(false);
    }

    // Grab 攻撃を時間経過で進行させる。
    // 前半で目標へ伸び、後半で待機位置へ戻る。
    private void TickGrabAttack(float deltaTime)
    {
        // 必須参照が不足していれば攻撃を中断
        if (m_config == null || m_palm == null || !m_hasIdleLocalPalmPosition)
        {
            m_isRunning = false;
            SetAttackHitboxEnabled(false);
            return;
        }

        // 攻撃全体の継続時間を取得（最小値で安全性を確保）
        float duration = Mathf.Max(0.01f, m_config.GrabDuration);
        m_attackTimer += deltaTime;

        // 攻撃の進行度を 0.0 ～ 1.0 の範囲で計算
        float normalizedTime = Mathf.Clamp01(m_attackTimer / duration);

        // 前半 50%: 待機位置からターゲット位置へ移動
        if (normalizedTime < 0.5f)
        {
            float t = normalizedTime / 0.5f;
            m_palm.localPosition = Vector3.Lerp(m_idleLocalPalmPosition, m_attackTargetLocalPosition, t);
        }
        // 後半 50%: ターゲット位置から待機位置へ戻る
        else
        {
            float t = (normalizedTime - 0.5f) / 0.5f;
            m_palm.localPosition = Vector3.Lerp(m_attackTargetLocalPosition, m_idleLocalPalmPosition, t);
        }

        // 攻撃判定の有効時間内かをチェック
        bool isActiveWindow =
            m_attackTimer >= m_config.GrabActiveStartTime &&
            m_attackTimer <= m_config.GrabActiveEndTime;

        // 有効時間内のみ判定を有効化
        SetAttackHitboxEnabled(isActiveWindow);

        // 攻撃時間が終了したら待機位置に戻して攻撃終了
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
        // 必須参照が不足していれば攻撃を中断
        if (m_config == null || m_palm == null || !m_hasIdleLocalPalmPosition)
        {
            m_isRunning = false;
            SetAttackHitboxEnabled(false);
            return;
        }

        // 攻撃全体の継続時間を取得（最小値で安全性を確保）
        float duration = Mathf.Max(0.01f, m_config.SmashDuration);
        m_attackTimer += deltaTime;

        // 攻撃の進行度を 0.0 ～ 1.0 の範囲で計算
        float normalizedTime = Mathf.Clamp01(m_attackTimer / duration);

        // 手を上に掲げるための溜め位置。
        Vector3 raiseLocalPosition = m_idleLocalPalmPosition + m_config.SmashWindupOffset;

        // 0% ～ 30%: 待機位置から上方向へ掲げる（溜めモーション）
        if (normalizedTime < 0.30f)
        {
            float t = normalizedTime / 0.30f;
            m_palm.localPosition = Vector3.Lerp(m_idleLocalPalmPosition, raiseLocalPosition, t);
        }
        // 30% ～ 60%: 上からターゲットへ一気に叩きつける（攻撃モーション）
        else if (normalizedTime < 0.60f)
        {
            float t = (normalizedTime - 0.30f) / 0.30f;
            m_palm.localPosition = Vector3.Lerp(raiseLocalPosition, m_attackTargetLocalPosition, t);
        }
        // 60% ～ 100%: 攻撃後に待機位置へ戻る（戻りモーション）
        else
        {
            float t = (normalizedTime - 0.60f) / 0.40f;
            m_palm.localPosition = Vector3.Lerp(m_attackTargetLocalPosition, m_idleLocalPalmPosition, t);
        }

        // 攻撃判定の有効時間内かをチェック
        bool isActiveWindow =
            m_attackTimer >= m_config.SmashActiveStartTime &&
            m_attackTimer <= m_config.SmashActiveEndTime;

        // 有効時間内のみ判定を有効化
        SetAttackHitboxEnabled(isActiveWindow);

        // 攻撃時間が終了したら待機位置に戻して攻撃終了
        if (m_attackTimer >= duration)
        {
            m_palm.localPosition = m_idleLocalPalmPosition;
            SetAttackHitboxEnabled(false);
            m_isRunning = false;
        }
    }

    // 攻撃判定用 Collider の有効/無効を切り替える
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
        // 攻撃実行中でなければ無視
        if (!m_isRunning)
        {
            return;
        }

        // 攻撃判定が無効化されていれば無視
        if (m_attackHitbox == null || !m_attackHitbox.enabled)
        {
            return;
        }

        // ダメージ受付インターフェースと敵攻撃受付インターフェースを取得
        IDamageable damageable = other.GetComponentInParent<IDamageable>();
        IEnemyAttackReceiver receiver = other.GetComponentInParent<IEnemyAttackReceiver>();

        // どちらのインターフェースも持っていなければ無視
        if (damageable == null && receiver == null)
        {
            return;
        }

        // 多段ヒット防止が有効で、既にヒット済みのターゲットなら無視
        if (m_preventMultipleHitsPerAttack && damageable != null && m_hitTargetsInCurrentAttack.Contains(damageable))
        {
            return;
        }

        // ヒット情報を計算
        Vector3 hitDirection = CalculateHitDirection(other);
        int damage = GetDamageByCurrentAttack();
        float knockbackForce = GetKnockbackForceByCurrentAttack();

        // 攻撃コンテキストを作成
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

        // まず状態変化側へ通知する（特殊な挙動が必要な場合）
        if (receiver != null)
        {
            receiver.ReceiveEnemyAttack(context);
        }
        // 状態変化を受け取れない相手には通常ダメージだけ送る
        else if (damageable != null)
        {
            damageable.TakeDamage(damage, hitDirection, knockbackForce);
        }

        // ヒット済みリストに追加して多段ヒットを防止
        if (m_preventMultipleHitsPerAttack && damageable != null)
        {
            m_hitTargetsInCurrentAttack.Add(damageable);
        }

        // デバッグログを出力
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
        // 攻撃元の位置（Palm があれば Palm、なければ自身の位置）
        Vector3 attackerPos = m_palm != null ? m_palm.position : transform.position;
        // ヒット対象の中心位置
        Vector3 targetPos = other.bounds.center;

        // 攻撃元からターゲットへの方向を計算
        Vector3 direction = (targetPos - attackerPos).normalized;
        // 疑似3D横スクなので Z 成分は無視
        direction.z = 0.0f;

        // 完全にゼロ方向になるのを避ける（同じ位置での接触など）
        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = Vector3.right;
        }

        return direction.normalized;
    }

    // 重要参照の不足を一度だけ警告する。
    // 毎フレーム警告が出ないように、一度警告したらフラグを立てる。
    private void WarnIfMissingCriticalReferences()
    {
        // 既に警告済みなら何もしない
        if (m_hasLoggedReferenceWarnings)
        {
            return;
        }

        bool hasMissing = false;

        // 各必須参照をチェック
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

        // 不足があった場合のみフラグを立てる（重複警告を防止）
        m_hasLoggedReferenceWarnings = hasMissing;
    }
}