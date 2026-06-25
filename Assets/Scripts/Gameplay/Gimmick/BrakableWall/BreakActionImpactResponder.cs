using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BreakActionHitbox))]
public sealed class BreakActionImpactResponder : MonoBehaviour
{
    [Header("参照")]
    [Tooltip("壁破壊通知を受け取る破壊判定です。未設定なら同じGameObjectから取得します。")]
    [SerializeField] private BreakActionHitbox hitbox;

    [Tooltip("PlayerDash の跳ね返り対象です。未設定なら親から取得します。")]
    [SerializeField] private PlayerController playerController;

    [Tooltip("SonarChargerCharge の跳ね返り対象です。未設定なら親から取得します。")]
    [SerializeField] private SonarChargerEnemy sonarChargerEnemy;

    [Tooltip("ShadowChaserDash の跳ね返り対象です。未設定なら親から取得します。")]
    [SerializeField] private ShadowChaserEnemy shadowChaserEnemy;

    [Header("プレイヤー跳ね返り")]
    [Tooltip("プレイヤーが壁を壊した時に、壁から離れる方向へ入れる速度です。")]
    [SerializeField] private float playerReboundSpeed = 8.0f;

    [Tooltip("プレイヤーが壁を壊した時に最低限入れる上向き速度です。")]
    [SerializeField] private float playerReboundUpSpeed = 3.0f;

    [Header("影敵跳ね返り")]
    [Tooltip("影敵が壁を壊した時に、壁から離れる方向へ戻る距離です。")]
    [SerializeField] private float shadowReboundDistance = 0.45f;

    [Tooltip("影敵の跳ね返り演出時間です。")]
    [SerializeField] private float shadowReboundDuration = 0.12f;

    [Header("デバッグ")]
    [Tooltip("有効にすると壁破壊後の跳ね返りログを出します。")]
    [SerializeField] private bool enableDebugLog = false;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();

        if (hitbox != null)
        {
            hitbox.WallBroken += HandleWallBroken;
        }
    }

    private void OnDisable()
    {
        if (hitbox != null)
        {
            hitbox.WallBroken -= HandleWallBroken;
        }
    }

    private void OnValidate()
    {
        playerReboundSpeed = Mathf.Max(0.0f, playerReboundSpeed);
        playerReboundUpSpeed = Mathf.Max(0.0f, playerReboundUpSpeed);
        shadowReboundDistance = Mathf.Max(0.0f, shadowReboundDistance);
        shadowReboundDuration = Mathf.Max(0.01f, shadowReboundDuration);

        ResolveReferences();
    }

    private void HandleWallBroken(BreakableWall wall, Vector3 hitPoint, Vector3 reboundDirection)
    {
        if (hitbox == null)
        {
            return;
        }

        switch (hitbox.ActionType)
        {
            case BreakActionType.PlayerDash:
                ApplyPlayerRebound(reboundDirection);
                break;

            case BreakActionType.SonarChargerCharge:
                ApplySonarChargerRebound();
                break;

            case BreakActionType.ShadowChaserDash:
                ApplyShadowChaserRebound(reboundDirection);
                break;
        }

        if (enableDebugLog)
        {
            Debug.Log(
                $"BreakActionImpactResponder: Wall broken. Action={hitbox.ActionType}, ReboundDirection={reboundDirection}",
                this);
        }
    }

    private void ApplyPlayerRebound(Vector3 reboundDirection)
    {
        if (playerController == null)
        {
            return;
        }

        playerController.RequestBreakWallDashRebound(
            reboundDirection,
            playerReboundSpeed,
            playerReboundUpSpeed);
    }

    private void ApplySonarChargerRebound()
    {
        if (sonarChargerEnemy == null)
        {
            return;
        }

        sonarChargerEnemy.RequestBreakWallRebound();
    }

    private void ApplyShadowChaserRebound(Vector3 reboundDirection)
    {
        if (shadowChaserEnemy == null)
        {
            return;
        }

        shadowChaserEnemy.RequestBreakWallDashRebound(
            reboundDirection,
            shadowReboundDistance,
            shadowReboundDuration);
    }

    private void ResolveReferences()
    {
        if (hitbox == null)
        {
            hitbox = GetComponent<BreakActionHitbox>();
        }

        if (playerController == null)
        {
            playerController = GetComponentInParent<PlayerController>();
        }

        if (sonarChargerEnemy == null)
        {
            sonarChargerEnemy = GetComponentInParent<SonarChargerEnemy>();
        }

        if (shadowChaserEnemy == null)
        {
            shadowChaserEnemy = GetComponentInParent<ShadowChaserEnemy>();
        }
    }
}