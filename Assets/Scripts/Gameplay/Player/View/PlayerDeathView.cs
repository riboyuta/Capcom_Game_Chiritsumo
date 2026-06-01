using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerDeathView : MonoBehaviour
{
    [Header("参照: PlayerView")]
    [Tooltip("倒れ演出の回転対象を取得するための PlayerView 参照です。")]
    [SerializeField] private PlayerView playerView;

    [Header("参照: BlinkRespawnTransitionView")]
    [Tooltip("リスポーン時の瞬き幕トランジションを担当する View です。")]
    [SerializeField] private BlinkRespawnTransitionView blinkTransitionView;

    [Header("Damage演出")]
    [Tooltip("Damage 死亡時に倒れ演出を使うかどうかです。")]
    [SerializeField] private bool useDamageDeathIntro = true;

    [Tooltip("Damage 死亡時に倒れ演出を進める時間です。")]
    [Min(0f)]
    [SerializeField] private float damageDeathIntroDuration = 0.12f;

    [Tooltip("Damage 死亡時に ViewRoot を Z 回転でどこまで倒すかの角度です。")]
    [Range(0f, 120f)]
    [SerializeField] private float damageDeathTiltAngle = 80f;

    private bool isIntroPlaying;
    private bool isIntroComplete = true;
    private float introElapsed;
    private float introDuration;
    private float introTiltAngle;

    public float DamageDeathIntroDuration
    {
        get
        {
            if (!useDamageDeathIntro)
            {
                return 0f;
            }

            return Mathf.Max(0f, damageDeathIntroDuration);
        }
    }

    public float DamageDeathTiltAngle => Mathf.Clamp(damageDeathTiltAngle, 0f, 120f);

    private void Awake()
    {
        ResolveReferencesIfNeeded();

        introDuration = DamageDeathIntroDuration;
        introTiltAngle = DamageDeathTiltAngle;
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
        progress = SmoothStep(progress);

        float currentAngle = Mathf.Lerp(0f, introTiltAngle, progress);
        target.localRotation = Quaternion.Euler(0f, 0f, currentAngle);

        if (progress >= 1f)
        {
            isIntroPlaying = false;
            isIntroComplete = true;
        }
    }

    public void ConfigureDamageDeathIntro(float duration, float tiltAngle)
    {
        if (!useDamageDeathIntro)
        {
            introDuration = 0f;
            introTiltAngle = 0f;
            return;
        }

        introDuration = Mathf.Max(0f, duration);
        introTiltAngle = Mathf.Clamp(tiltAngle, 0f, 120f);
    }

    public void PlayDamageDeathIntro()
    {
        if (!useDamageDeathIntro)
        {
            isIntroPlaying = false;
            isIntroComplete = true;
            return;
        }

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
        }
    }

    public bool IsIntroComplete()
    {
        return isIntroComplete;
    }

    public IEnumerator PlayRespawnTransitionIn()
    {
        ResolveReferencesIfNeeded();

        if (blinkTransitionView == null)
        {
            yield break;
        }

        yield return blinkTransitionView.PlayClose();
    }

    public IEnumerator PlayRespawnTransitionOut()
    {
        ResolveReferencesIfNeeded();

        if (blinkTransitionView == null)
        {
            yield break;
        }

        yield return blinkTransitionView.PlayOpen();
    }

    public void ResetRespawnTransitionImmediate()
    {
        ResolveReferencesIfNeeded();

        if (blinkTransitionView == null)
        {
            return;
        }

        blinkTransitionView.ResetImmediate();
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
    }

    private void ResolveReferencesIfNeeded()
    {
        if (playerView == null)
        {
            playerView = GetComponent<PlayerView>();
        }

        if (playerView == null)
        {
            playerView = GetComponentInChildren<PlayerView>(true);
        }

        if (blinkTransitionView == null)
        {
            blinkTransitionView = GetComponent<BlinkRespawnTransitionView>();
        }

        if (blinkTransitionView == null)
        {
            blinkTransitionView = GetComponentInChildren<BlinkRespawnTransitionView>(true);
        }
    }

    private Transform GetRotationTarget()
    {
        if (playerView == null)
        {
            playerView = GetComponent<PlayerView>();
        }

        if (playerView == null)
        {
            playerView = GetComponentInChildren<PlayerView>(true);
        }

        return playerView != null ? playerView.ViewRoot : null;
    }

    private float SmoothStep(float t)
    {
        return t * t * (3f - 2f * t);
    }
}