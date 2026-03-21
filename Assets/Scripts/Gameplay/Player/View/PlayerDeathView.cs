using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerDeathView : MonoBehaviour
{
    [Header("参照: PlayerView")]
    [Tooltip("倒れ演出の回転対象を取得するための PlayerView 参照です。未設定時は同一階層から探索します。")]
    [SerializeField] private PlayerView playerView;

    [Header("参照: DeathTransitionView")]
    [Tooltip("死亡時の黒トランジション表示を担当する View 参照です。未設定時は同一階層から探索します。")]
    [SerializeField] private DeathTransitionView deathTransitionView;

    [Header("敵攻撃死: 入口演出")]
    [Tooltip("DeathCause.Damage 時に、少ズーム+倒れ演出を再生する入口時間です。")]
    [Min(0f)]
    [SerializeField] private float damageDeathIntroDuration = 0.12f;

    [Tooltip("DeathCause.Damage 時に viewRoot を Z 回転でどこまで倒すかの目標角度です。")]
    [Range(0f, 120f)]
    [SerializeField] private float damageDeathTiltAngle = 80f;

    [Space(8f)]
    [Header("敵攻撃死: 少ズーム")]
    [Tooltip("DeathCause.Damage 時に現在の orthographicSize へ加算する値です。負値で少しズームインします。")]
    [SerializeField] private float damageDeathZoomSizeOffset = -0.35f;

    [Tooltip("DeathCause.Damage 時にズーム上書きへ切り替える際の補間時間です。0 で即時反映します。")]
    [Min(0f)]
    [SerializeField] private float damageDeathZoomSmoothTime = 0.08f;

    [Space(8f)]
    [Header("黒トランジション")]
    [Tooltip("透明から黒へ入る時間です。")]
    [Min(0f)]
    [SerializeField] private float blackInDuration = 0.2f;

    [Tooltip("黒から透明へ戻る時間です。")]
    [Min(0f)]
    [SerializeField] private float blackOutDuration = 0.25f;

    [Tooltip("現在の黒さがこの値以上なら復帰処理を隠せるとみなすしきい値です。")]
    [Range(0f, 1f)]
    [SerializeField] private float blackRespawnThreshold = 0.85f;

    private bool isIntroPlaying;
    private bool isIntroComplete;
    private float introElapsed;
    private float introDuration;
    private float introTiltAngle;

    private void Awake()
    {
        if (playerView == null)
        {
            playerView = GetComponent<PlayerView>();
        }

        if (playerView == null)
        {
            playerView = GetComponentInChildren<PlayerView>();
        }

        if (deathTransitionView == null)
        {
            deathTransitionView = GetComponent<DeathTransitionView>();
        }

        if (deathTransitionView == null)
        {
            deathTransitionView = GetComponentInChildren<DeathTransitionView>();
        }

        introDuration = Mathf.Max(0f, damageDeathIntroDuration);
        introTiltAngle = Mathf.Clamp(damageDeathTiltAngle, 0f, 120f);
    }

    private void Update()
    {
        if (!isIntroPlaying)
        {
            return;
        }

        Transform target = GetRotationTarget();
        if (target == null)
        {
            isIntroPlaying = false;
            isIntroComplete = true;
            return;
        }

        introElapsed += Time.deltaTime;
        float duration = Mathf.Max(0.0001f, introDuration);
        float progress = Mathf.Clamp01(introElapsed / duration);

        float currentAngle = Mathf.Lerp(0f, introTiltAngle, progress);
        target.localRotation = Quaternion.Euler(0f, 0f, currentAngle);

        if (progress >= 1f)
        {
            isIntroPlaying = false;
            isIntroComplete = true;
            Debug.Log("[PlayerDeathView] Damage death intro complete", this);
        }
    }

    public void ConfigureDamageDeathIntro(float duration, float tiltAngle)
    {
        introDuration = Mathf.Max(0f, duration);
        introTiltAngle = Mathf.Clamp(tiltAngle, 0f, 120f);
    }

    public float DamageDeathIntroDuration => Mathf.Max(0f, damageDeathIntroDuration);
    public float DamageDeathTiltAngle => Mathf.Clamp(damageDeathTiltAngle, 0f, 120f);
    public float DamageDeathZoomSizeOffset => damageDeathZoomSizeOffset;
    public float DamageDeathZoomSmoothTime => Mathf.Max(0f, damageDeathZoomSmoothTime);
    public float BlackInDuration => Mathf.Max(0f, blackInDuration);
    public float BlackOutDuration => Mathf.Max(0f, blackOutDuration);
    public float BlackRespawnThreshold => Mathf.Clamp01(blackRespawnThreshold);

    public void PlayDamageDeathIntro()
    {
        Transform target = GetRotationTarget();
        if (target == null)
        {
            isIntroPlaying = false;
            isIntroComplete = true;
            Debug.LogWarning("[PlayerDeathView] Damage death rotation target missing", this);
            return;
        }

        introElapsed = 0f;
        isIntroPlaying = true;
        isIntroComplete = introDuration <= 0f;

        target.localRotation = Quaternion.identity;

        if (isIntroComplete)
        {
            target.localRotation = Quaternion.Euler(0f, 0f, introTiltAngle);
            isIntroPlaying = false;
            Debug.Log("[PlayerDeathView] Damage death intro complete", this);
        }

        Debug.Log("[PlayerDeathView] Damage death intro started", this);
    }

    public void ResetDeathPresentation()
    {
        Transform target = GetRotationTarget();
        if (target != null)
        {
            target.localRotation = Quaternion.identity;
        }

        introElapsed = 0f;
        isIntroPlaying = false;
        isIntroComplete = true;

        Debug.Log("[PlayerDeathView] Damage death presentation reset", this);
    }

    public bool IsIntroComplete()
    {
        return isIntroComplete;
    }

    public void PlayTransitionIn()
    {
        if (deathTransitionView == null)
        {
            deathTransitionView = GetComponentInChildren<DeathTransitionView>();
        }

        deathTransitionView?.PlayTransitionIn(BlackInDuration);
    }

    public void PlayTransitionOut()
    {
        if (deathTransitionView == null)
        {
            deathTransitionView = GetComponentInChildren<DeathTransitionView>();
        }

        deathTransitionView?.PlayTransitionOut(BlackOutDuration);
    }

    public float GetBlackAmount()
    {
        if (deathTransitionView == null)
        {
            deathTransitionView = GetComponentInChildren<DeathTransitionView>();
        }

        return deathTransitionView != null ? deathTransitionView.GetBlackAmount() : 0f;
    }

    public void ResetTransitionImmediate()
    {
        if (deathTransitionView == null)
        {
            deathTransitionView = GetComponentInChildren<DeathTransitionView>();
        }

        deathTransitionView?.ResetTransitionImmediate();
    }

    private Transform GetRotationTarget()
    {
        if (playerView == null)
        {
            playerView = GetComponent<PlayerView>();
            if (playerView == null)
            {
                playerView = GetComponentInChildren<PlayerView>();
            }
        }

        return playerView != null ? playerView.ViewRoot : null;
    }
}