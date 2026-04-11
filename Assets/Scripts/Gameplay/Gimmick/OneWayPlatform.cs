using UnityEngine;

// 一方通行床ギミック。
// 下からは通過し、上からは着地できる。横からはすり抜ける。
// 落下入力で降りられるかどうかを Inspector で選択可能。
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public sealed class OneWayPlatform : MonoBehaviour, IRespawnResettable
{
    [Header("落下入力で降りられるか")]
    [Tooltip("true の場合、下入力を入れると床をすり抜けて降りられます。false の場合は降りられません。")]
    [SerializeField] private bool allowDropThrough = true;

    [Header("すり抜け後の復帰時間（秒）")]
    [Tooltip("すり抜け開始後、衝突判定を再び有効にするまでの時間（秒）。短すぎるとすり抜け中に再び着地してしまいます。")]
    [SerializeField, Min(0.05f)] private float dropThroughDuration = 0.4f;

    [Header("判定の許容誤差（メートル）")]
    [Tooltip("プレイヤーの足元が床上面からこの値以内に収まっていれば「上にいる」と判定します。")]
    [SerializeField, Min(0f)] private float tolerance = 0.05f;

    private Collider platformCollider;
    private Collider playerCollider;
    private Rigidbody playerRb;
    private PlayerFacade playerFacade;
    private float dropThroughTimer;
    private bool hasCapturedInitialState;

    private void Awake()
    {
        platformCollider = GetComponent<Collider>();
    }

    private void Start()
    {
        // プレイヤーを検索してキャッシュする。
        TryFindPlayer();
    }

    // ──────────────────────────────────────────────
    // IRespawnResettable
    // ──────────────────────────────────────────────

    public void CaptureInitialState()
    {
        if (hasCapturedInitialState) return;
        hasCapturedInitialState = true;
    }

    public void ResetToRespawnState()
    {
        // すり抜けタイマーをリセットし、衝突判定を復帰させる。
        dropThroughTimer = 0f;

        if (playerCollider != null && platformCollider != null)
        {
            Physics.IgnoreCollision(playerCollider, platformCollider, false);
        }
    }

    // ──────────────────────────────────────────────
    // 毎物理フレームの衝突判定更新
    // ──────────────────────────────────────────────

    private void FixedUpdate()
    {
        // プレイヤーが見つかっていなければ再検索する。
        if (playerCollider == null)
        {
            TryFindPlayer();
            if (playerCollider == null) return;
        }

        if (platformCollider == null) return;

        // すり抜けタイマーが残っている間は衝突を無効のまま維持する。
        if (dropThroughTimer > 0f)
        {
            dropThroughTimer -= Time.fixedDeltaTime;
            Physics.IgnoreCollision(playerCollider, platformCollider, true);
            return;
        }

        // プレイヤーの足元位置と床上面の位置関係を調べる。
        float platformTop = platformCollider.bounds.max.y;
        float playerBottom = playerCollider.bounds.min.y;
        bool playerFallingOrStill = playerRb == null || playerRb.linearVelocity.y <= 0.01f;

        // 【修正点】急降下など高速落下時のすっぽ抜け（トンネリング）対策
        // 落下速度が速い場合、1物理フレームで移動する距離が tolerance を超えてしまい
        // 「床の下にいる」と誤認されて衝突判定が外れるのを防ぎます。
        float currentTolerance = tolerance;
        if (playerFallingOrStill && playerRb != null && playerRb.linearVelocity.y < -0.1f)
        {
            // 現在の落下速度に基づき、次フレームの移動距離を予測して許容範囲を広げる（最大 1.0m 拡張）
            float fallDistancePerFrame = Mathf.Abs(playerRb.linearVelocity.y) * Time.fixedDeltaTime;
            currentTolerance += Mathf.Min(1.0f, fallDistancePerFrame * 1.5f);
        }

        bool playerAbove = playerBottom >= platformTop - currentTolerance;

        // 落下入力によるすり抜け判定。
        // プレイヤーが床の上にいて、下入力を入れている場合にすり抜けを開始する。
        if (allowDropThrough && playerAbove && playerFacade != null && playerFacade.IsDownInputHeld)
        {
            dropThroughTimer = dropThroughDuration;
            Physics.IgnoreCollision(playerCollider, platformCollider, true);
            return;
        }

        // プレイヤーが床の上にいて、落下中または静止中なら衝突を有効にする。
        // それ以外（下にいる、上昇中）なら衝突を無効にして通過させる。
        bool shouldCollide = playerAbove && playerFallingOrStill;
        Physics.IgnoreCollision(playerCollider, platformCollider, !shouldCollide);
    }

    // ──────────────────────────────────────────────
    // プレイヤー検索
    // ──────────────────────────────────────────────

    private void TryFindPlayer()
    {
        // シーン内から PlayerFacade を検索する。
        PlayerFacade[] facades = FindObjectsByType<PlayerFacade>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        if (facades.Length == 0) return;

        playerFacade = facades[0];
        playerRb = playerFacade.GetComponent<Rigidbody>();
        playerCollider = playerFacade.GetComponent<Collider>();
    }
}
