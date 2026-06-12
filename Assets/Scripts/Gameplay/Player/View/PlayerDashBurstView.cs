using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Gameplay/Player/Player Dash Burst View")]
public sealed class PlayerDashBurstView : MonoBehaviour
{
    [Header("参照: PlayerFacade")]
    [Tooltip("ダッシュ開始状態とダッシュ方向を読み取るPlayerFacadeです。未設定の場合は親階層から自動取得します。")]
    [SerializeField] private PlayerFacade playerFacade;

    [Header("参照: 開始バーストPrefab")]
    [Tooltip("ダッシュ開始時に生成する見た目用Prefabです。未設定の場合は1回だけWarningを出し、生成処理は行いません。")]
    [SerializeField] private GameObject burstPrefab;

    [Header("生成位置: 追加オフセット")]
    [Tooltip("バースト生成位置に加えるワールド座標オフセットです。プレイヤー中心から上下左右にずらしたい場合に調整します。")]
    [SerializeField] private Vector3 spawnOffset;

    [Header("生成位置: ダッシュ後方オフセット")]
    [Tooltip("ダッシュ方向の逆側へずらす距離です。値を大きくすると、プレイヤーの後ろで爆ぜて前へ飛ぶ印象になります。")]
    [SerializeField, Min(0f)] private float backOffsetFromDashDirection = 0.25f;

    [Header("見た目: サイズ倍率")]
    [Tooltip("生成したPrefab全体に掛けるサイズ倍率です。Prefab本来のスケールを基準に乗算します。")]
    [SerializeField, Min(0.01f)] private float scaleMultiplier = 1f;

    [Header("破棄: 自動Destroyまでの時間")]
    [Tooltip("生成したバーストPrefabをDestroyするまでの秒数です。ParticleSystemの再生時間より少し長めに設定してください。")]
    [SerializeField, Min(0.01f)] private float destroyDelay = 1f;

    [Header("向き: ダッシュ方向へ回転")]
    [Tooltip("有効にすると、Prefabのローカル+X方向がDashDirectionへ向くように回転して生成します。無効時はPrefab側の回転を使います。")]
    [SerializeField] private bool useDashDirectionRotation = true;

    private bool wasDashActive;
    private bool warnedMissingReferences;
    private bool warnedMissingPrefab;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        // PlayerFacade内部の初期化順に依存しないよう、OnEnableでは状態を読まない。
        wasDashActive = false;
    }

    private void OnValidate()
    {
        backOffsetFromDashDirection = Mathf.Max(0f, backOffsetFromDashDirection);
        scaleMultiplier = Mathf.Max(0.01f, scaleMultiplier);
        destroyDelay = Mathf.Max(0.01f, destroyDelay);
    }

    private void Update()
    {
        if (!ResolveReferences())
        {
            WarnMissingReferencesOnce();
            return;
        }

        bool isDashActive = playerFacade.IsDashActive;
        bool didDashStart = playerFacade.JustDashStartedThisFrame || (!wasDashActive && isDashActive);

        if (didDashStart)
        {
            SpawnBurst();
        }

        wasDashActive = isDashActive;
    }

    private bool ResolveReferences()
    {
        if (playerFacade == null)
        {
            playerFacade = GetComponentInParent<PlayerFacade>();
        }

        return playerFacade != null;
    }

    private void SpawnBurst()
    {
        if (burstPrefab == null)
        {
            WarnMissingPrefabOnce();
            return;
        }

        Vector3 dashDirection = ResolveDashDirection();
        Vector3 spawnPosition = transform.position + spawnOffset - dashDirection * backOffsetFromDashDirection;
        Quaternion spawnRotation = useDashDirectionRotation
            ? Quaternion.FromToRotation(Vector3.right, dashDirection)
            : burstPrefab.transform.rotation;

        GameObject instance = Instantiate(burstPrefab, spawnPosition, spawnRotation);
        instance.transform.localScale *= scaleMultiplier;

        // 試作用ScriptなのでPool化せず、連続ダッシュ後にHierarchyへ残らないことを優先する。
        Destroy(instance, destroyDelay);
    }

    private Vector3 ResolveDashDirection()
    {
        Vector2 dashDirection = playerFacade.DashDirection;
        if (dashDirection.sqrMagnitude > 0.0001f)
        {
            return new Vector3(dashDirection.x, dashDirection.y, 0f).normalized;
        }

        Vector3 fallbackDirection = transform.right;
        fallbackDirection.z = 0f;
        if (fallbackDirection.sqrMagnitude <= 0.0001f)
        {
            fallbackDirection = Vector3.right;
        }

        return fallbackDirection.normalized;
    }

    private void WarnMissingReferencesOnce()
    {
        if (warnedMissingReferences)
        {
            return;
        }

        warnedMissingReferences = true;
        Debug.LogWarning(
            $"{nameof(PlayerDashBurstView)} requires a {nameof(PlayerFacade)} reference.",
            this);
    }

    private void WarnMissingPrefabOnce()
    {
        if (warnedMissingPrefab)
        {
            return;
        }

        warnedMissingPrefab = true;
        Debug.LogWarning(
            $"{nameof(PlayerDashBurstView)} requires a burstPrefab to spawn dash start burst effects.",
            this);
    }
}
