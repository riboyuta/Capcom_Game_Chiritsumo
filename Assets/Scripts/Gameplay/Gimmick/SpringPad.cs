using UnityEngine;

// 任意方向へプレイヤーを固定射出するバネギミック。
// 射出方向は transform.up で決まり、床・壁・下向き・斜めバネに対応する。
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public sealed class SpringPad : MonoBehaviour
{
    private const string InteractedTriggerName = "Interacted";
    private const string SpringPadSfxName = "SFX_gimmick_springpad";

    [Header("射出: 距離")]
    [Tooltip("バネ方向へ進ませたい距離です。上向きバネなら高さ、横向きバネなら押し出し距離として扱います。")]
    [SerializeField, Min(0.1f)] private float launchDistance = 4.5f;

    [Header("射出: 到達時間")]
    [Tooltip("指定距離へ到達する目安時間です。小さいほど鋭く速いバネになります。")]
    [SerializeField, Min(0.05f)] private float timeToTarget = 0.35f;

    [Header("射出: 入力ブロック時間")]
    [Tooltip("射出直後に移動入力の干渉を抑える時間です。0.08〜0.12秒程度が目安です。")]
    [SerializeField, Min(0f)] private float inputBlockDuration = 0.1f;

    [Header("射出: 接線速度維持率")]
    [Tooltip("射出方向と直交する既存速度をどれだけ残すかです。1なら完全維持、0なら消します。")]
    [SerializeField, Range(0f, 1f)] private float tangentVelocityKeepRate = 0.85f;

    [Header("射出: 保護時間")]
    [Tooltip("外部射出として扱い、可変ジャンプカット等に潰されないようにする時間です。")]
    [SerializeField, Min(0f)] private float launchProtectionDuration = 0.5f;

    [Header("跳ね返し: クールダウン（秒）")]
    [Tooltip("同一オブジェクトが短時間で連続して跳ねられるのを防ぐクールダウン時間（秒）。値を大きくすると連続バウンスを減らせます。")]
    [SerializeField, Min(0f)] private float bounceCooldown = 0.2f;

    [Header("発動判定: 法線しきい値")]
    [Tooltip("発動面から触れたかを判定するしきい値です。0.5なら約60度以内、0.707なら約45度以内です。")]
    [SerializeField, Range(0f, 1f)] private float activationNormalThreshold = 0.5f;

    [Header("アニメーション")]
    [Tooltip("使用するアニメーターを入れる")]
    [SerializeField] private Animator anim;

    // バネ自身の Collider（中心位置基準の発動面判定用）。
    private Collider springCollider;

    private float lastBounceTime = float.NegativeInfinity;

    private void Awake()
    {
        springCollider = GetComponent<Collider>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!TryGetPlayerFacade(other, out PlayerFacade facade))
        {
            return;
        }

        if (!IsOnActivationSide(other))
        {
            return;
        }

        TryBounce(facade);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!TryGetPlayerFacade(collision.collider, out PlayerFacade facade))
        {
            return;
        }

        if (!HasValidActivationContact(collision))
        {
            return;
        }

        TryBounce(facade);
    }

    private bool TryGetPlayerFacade(Collider hitCollider, out PlayerFacade facade)
    {
        facade = null;

        Rigidbody attachedRigidbody = hitCollider.attachedRigidbody;
        if (attachedRigidbody == null)
        {
            return false;
        }

        facade = attachedRigidbody.GetComponent<PlayerFacade>();
        return facade != null;
    }

    private bool IsOnActivationSide(Collider other)
    {
        if (springCollider == null)
        {
            return true;
        }

        Vector3 toOther = other.bounds.center - springCollider.bounds.center;
        if (toOther.sqrMagnitude <= Mathf.Epsilon)
        {
            return true;
        }

        Vector3 launchDirection = transform.up.normalized;
        float sideDot = Vector3.Dot(toOther.normalized, launchDirection);
        return sideDot >= 0f;
    }

    private bool HasValidActivationContact(Collision collision)
    {
        Vector3 activationDirection = transform.up.normalized;

        for (int i = 0; i < collision.contactCount; i++)
        {
            ContactPoint contact = collision.GetContact(i);

            // 既存実装と同様に「上から踏んだとき normal が -transform.up 側を向く」前提で判定する。
            // ContactPoint.normal の向きは実機で Debug.DrawRay などを使って再確認すること。
            float dot = Vector3.Dot(contact.normal, -activationDirection);
            if (dot >= activationNormalThreshold)
            {
                return true;
            }
        }

        return false;
    }

    private void TryBounce(PlayerFacade facade)
    {
        if (facade == null)
        {
            return;
        }

        if (Time.time - lastBounceTime < bounceCooldown)
        {
            return;
        }

        PlayFeedback();
        lastBounceTime = Time.time;

        facade.TryRefillDash(DashRefillReason.Gimmick);

        Vector3 launchDirection = transform.up.normalized;
        PlayerFixedLaunchRequest request = new PlayerFixedLaunchRequest
        {
            Owner = this,
            Direction = launchDirection,
            Speed = ComputeLaunchSpeed(),
            TangentVelocityKeepRate = tangentVelocityKeepRate,
            InputBlockFlags = PlayerController.InputBlockFlags.Move,
            InputBlockDuration = inputBlockDuration,
            LaunchProtectionDuration = launchProtectionDuration,
            CancelDash = true,
            NotifyExternalLaunch = true,
            ForceUnground = true
        };

        facade.ApplyFixedLaunch(in request);
    }

    private float ComputeLaunchSpeed()
    {
        float safeTime = Mathf.Max(0.05f, timeToTarget);
        return launchDistance / safeTime;
    }

    private void PlayFeedback()
    {
        if (anim != null)
        {
            anim.SetTrigger(InteractedTriggerName);
        }

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayOverlap(SpringPadSfxName);
        }
    }
}