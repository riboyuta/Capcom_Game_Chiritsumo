using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerDeathView : MonoBehaviour
{
    [Header("参照: PlayerView")]
    [Tooltip("倒れ演出の回転対象を取得するための PlayerView 参照です。未設定時は同一階層から探索します。")]
    [SerializeField] private PlayerView playerView;

    [Header("敵攻撃死: デフォルト入口時間(秒)")]
    [Tooltip("ConfigureDamageDeathIntro が未呼び出し時に使う倒れ演出時間です。")]
    [Min(0f)]
    [SerializeField] private float defaultIntroDuration = 0.12f;

    [Header("敵攻撃死: デフォルト倒れ角度(度)")]
    [Tooltip("ConfigureDamageDeathIntro が未呼び出し時に使う Z 回転目標角度です。")]
    [Range(0f, 120f)]
    [SerializeField] private float defaultTiltAngle = 80f;

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

        introDuration = Mathf.Max(0f, defaultIntroDuration);
        introTiltAngle = Mathf.Clamp(defaultTiltAngle, 0f, 120f);
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