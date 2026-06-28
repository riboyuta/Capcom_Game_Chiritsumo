using UnityEngine;

[DefaultExecutionOrder(1000)]
[DisallowMultipleComponent]
[RequireComponent(typeof(BreakActionHitbox))]
public sealed class BreakActionHitboxStateBinder : MonoBehaviour
{
    private enum HitboxStateSource
    {
        PlayerDash,
        SonarChargerCharge,
        ShadowChaserDash,
    }

    [Header("破壊判定")]
    [Tooltip("ON/OFFを切り替える破壊判定です。未設定なら同じGameObjectから取得します。")]
    [SerializeField] private BreakActionHitbox hitbox;

    [Header("有効化条件")]
    [Tooltip("どの状態を見て破壊判定を有効化するかです。")]
    [SerializeField] private HitboxStateSource stateSource = HitboxStateSource.PlayerDash;

    [Tooltip("PlayerDash を使う場合の参照です。未設定なら親から取得します。")]
    [SerializeField] private PlayerController playerController;

    [Tooltip("SonarChargerCharge を使う場合の参照です。未設定なら親から取得します。")]
    [SerializeField] private SonarChargerEnemy sonarChargerEnemy;

    [Tooltip("ShadowChaserDash を使う場合の参照です。未設定なら親から取得します。")]
    [SerializeField] private ShadowChaserEnemy shadowChaserEnemy;

    [Header("デバッグ")]
    [Tooltip("有効にすると破壊判定のON/OFFログを出します。")]
    [SerializeField] private bool enableDebugLog = false;

    private bool lastEnabled;

    private void Awake()
    {
        ResolveReferences();
        SetHitboxEnabled(false, true);
    }

    private void OnValidate()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        SyncHitboxState();
    }

    private void OnDisable()
    {
        SetHitboxEnabled(false, true);
    }

    private void Update()
    {
        SyncHitboxState();
    }

    private void FixedUpdate()
    {
        SyncHitboxState();
    }

    private void LateUpdate()
    {
        SyncHitboxState();
    }

    private void SyncHitboxState()
    {
        ResolveReferences();

        bool shouldEnable = ShouldEnableHitbox();
        SetHitboxEnabled(shouldEnable, false);
    }

    private bool ShouldEnableHitbox()
    {
        switch (stateSource)
        {
            case HitboxStateSource.PlayerDash:
                return ShouldEnableByPlayerDash();

            case HitboxStateSource.SonarChargerCharge:
                return ShouldEnableBySonarChargerCharge();

            case HitboxStateSource.ShadowChaserDash:
                return ShouldEnableByShadowChaserDash();

            default:
                return false;
        }
    }

    private bool ShouldEnableByPlayerDash()
    {
        if (playerController == null)
        {
            return false;
        }

        if (playerController.IsActionLocked || playerController.IsDeathSequencePlaying)
        {
            return false;
        }

        return playerController.IsDashActive;
    }

    private bool ShouldEnableBySonarChargerCharge()
    {
        if (sonarChargerEnemy == null)
        {
            return false;
        }

        return sonarChargerEnemy.IsActivated && sonarChargerEnemy.IsCharging;
    }

    private bool ShouldEnableByShadowChaserDash()
    {
        if (shadowChaserEnemy == null)
        {
            return false;
        }

        if (!shadowChaserEnemy.IsFollowing)
        {
            return false;
        }

        if (!shadowChaserEnemy.HasSnapshot)
        {
            return false;
        }

        return shadowChaserEnemy.CurrentSnapshot.isDashing;
    }

    private void SetHitboxEnabled(bool enabled, bool force)
    {
        if (hitbox == null)
        {
            return;
        }

        if (!force && lastEnabled == enabled)
        {
            return;
        }

        lastEnabled = enabled;
        hitbox.SetHitboxEnabled(enabled);

        if (enableDebugLog)
        {
            Debug.Log($"BreakActionHitboxStateBinder: Hitbox {(enabled ? "ON" : "OFF")} Source={stateSource}", this);
        }
    }

    private void ResolveReferences()
    {
        if (hitbox == null)
        {
            hitbox = GetComponent<BreakActionHitbox>();
        }

        switch (stateSource)
        {
            case HitboxStateSource.PlayerDash:
                if (playerController == null)
                {
                    playerController = GetComponentInParent<PlayerController>();
                }
                break;

            case HitboxStateSource.SonarChargerCharge:
                if (sonarChargerEnemy == null)
                {
                    sonarChargerEnemy = GetComponentInParent<SonarChargerEnemy>();
                }
                break;

            case HitboxStateSource.ShadowChaserDash:
                if (shadowChaserEnemy == null)
                {
                    shadowChaserEnemy = GetComponentInParent<ShadowChaserEnemy>();
                }
                break;
        }
    }
}