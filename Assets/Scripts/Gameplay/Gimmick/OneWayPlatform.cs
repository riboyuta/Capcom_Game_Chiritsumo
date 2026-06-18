using UnityEngine;

// 一方通行床ギミック。
// 下からの通過、上からの落下入力によるすり抜けを Inspector で選択可能。
//
// 設計方針:
// プレイヤーの衝突を操作するのではなく、床自身のコライダーの enabled を
// 切り替えることで一方通行を実現する。
// これにより、プレイヤーの物理状態（接地判定・摩擦・ステップ等）に
// 一方通行床が干渉する問題を根本的に回避する。
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public sealed class OneWayPlatform : MonoBehaviour, IRespawnResettable
{
    private enum PassThroughFromBelowMode
    {
        Allow = 0,
        MatchDropThroughSetting = 1,
        Block = 2,
    }

    [Header("落下入力で降りられるか")]
    [Tooltip("true の場合、下入力を入れると床をすり抜けて降りられます。false の場合は降りられません。")]
    [SerializeField] private bool allowDropThrough = true;

    [Header("下から上へ通過できるか")]
    [Tooltip("MatchDropThroughSetting は落下入力で降りられるかの設定に合わせます。Allow は下から通過可能、Block は下からも衝突します。")]
    [SerializeField] private PassThroughFromBelowMode passThroughFromBelow = PassThroughFromBelowMode.Allow;

    [Header("すり抜け後の復帰時間（秒）")]
    [Tooltip("すり抜け開始後、衝突判定を再び有効にするまでの時間（秒）。短すぎるとすり抜け中に再び着地してしまいます。")]
    [SerializeField, Min(0.05f)] private float dropThroughDuration = 0.4f;

    [Header("判定の許容誤差（メートル）")]
    [Tooltip("プレイヤーの足元が床上面からこの値以内に収まっていれば「上にいる」と判定します。")]
    [SerializeField, Min(0f)] private float tolerance = 0.05f;

    [Header("下から侵入後の着地復帰")]
    [Tooltip("下から通過中に下降へ転じた時、プレイヤー中心が床上面からこの高さ以上なら床上側に入ったとみなして Collider を戻します。")]
    [SerializeField, Min(0f)] private float fromBelowCatchCenterMargin = 0.05f;

    private Collider platformCollider;
    private Collider playerCollider; // bounds 読み取り専用。衝突操作には使用しない。
    private PlayerFacade playerFacade;

    // プレイヤーはシーンに1体しかいないため、全インスタンスで検索結果を共有する。
    // FindObjectsByType の呼び出しを最小限に抑える。
    private static PlayerFacade s_cachedFacade;
    private static Collider s_cachedCollider;
    // 同一フレーム内での重複検索を防ぐためのフレーム番号。
    private static int s_lastSearchFrame = -1;
    private float dropThroughTimer;
    private bool hasCapturedInitialState;
    private bool wasBelowPlatform;

    // リスポーン・部屋遷移直後の猶予タイマー。
    // ResetToRespawnState はプレイヤーのテレポートより先に呼ばれるため、
    // テレポート完了後の数フレームは wasBelowPlatform を強制的に false にして
    // コライダーを有効に保つ。これにより、テレポート先で「下にいる」と
    // 誤判定されてコライダーが切れ、プレイヤーが落下する問題を防ぐ。
    // また、外部制御（部屋遷移）終了時にも同様に発動する。
    private float resetGraceTimer;
    private const float ResetGraceDuration = 0.15f;

    // 前フレームで外部制御中だったかを追跡する。
    // 外部制御が終了した瞬間（true → false）を検出し、猶予タイマーを起動する。
    private bool wasExternallyControlled;

    private void Awake()
    {
        platformCollider = GetComponent<Collider>();
    }

    private void Start()
    {
        // プレイヤーを検索してキャッシュする。
        TryFindPlayer();

        RecalculateWasBelowPlatform();
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
        // すり抜けタイマーをリセットし、コライダーを有効に戻す。
        dropThroughTimer = 0f;

        if (playerCollider == null || playerFacade == null)
        {
            TryFindPlayer();
        }

        SetPlatformEnabled(true);

        // プレイヤーのテレポートはこの呼び出しの後に行われるため、
        // 今の位置で wasBelowPlatform を再計算しても意味がない。
        // 強制的に false にし、猶予タイマーでテレポート完了まで保護する。
        wasBelowPlatform = false;
        resetGraceTimer = ResetGraceDuration;
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

        // ──────────────────────────────────────────────
        // 外部制御（部屋遷移）中の処理
        // ──────────────────────────────────────────────
        // 外部制御中はプレイヤーの移動が遷移システムに管理されているため、
        // 一方通行判定を停止し、コライダーを OFF にして自由に通過させる。
        // 外部制御が終了した瞬間に猶予タイマーを起動し、
        // プレイヤーが正しく床の上に押し出されるようにする。
        bool isExternallyControlled = playerFacade != null && playerFacade.IsExternallyControlled;

        if (isExternallyControlled)
        {
            // 外部制御中: コライダー OFF、状態追跡停止。
            SetPlatformEnabled(false);
            wasExternallyControlled = true;
            return;
        }

        // 外部制御が終了した瞬間を検出し、猶予タイマーを起動する。
        if (wasExternallyControlled)
        {
            wasExternallyControlled = false;
            wasBelowPlatform = false;
            resetGraceTimer = ResetGraceDuration;
        }

        // リスポーン・遷移直後の猶予期間中は、wasBelowPlatform を強制リセットして
        // コライダーを有効に保つ。テレポート先でのめり込みは物理エンジンが押し出す。
        if (resetGraceTimer > 0f)
        {
            resetGraceTimer -= Time.fixedDeltaTime;
            wasBelowPlatform = false;
            SetPlatformEnabled(true);
            return;
        }

        // すり抜けタイマーが残っている間はコライダーを無効のまま維持する。
        if (dropThroughTimer > 0f)
        {
            dropThroughTimer -= Time.fixedDeltaTime;
            SetPlatformEnabled(false);
            return;
        }

        // プレイヤーの足元位置と床上面の位置関係を調べる。
        // コライダーが無効のときは bounds が使えないため、保存済みの位置を使う。
        float platformTop = GetPlatformTop();
        float playerBottom = playerCollider.bounds.min.y;

        // 急降下など高速落下時のすっぽ抜け（トンネリング）対策
        // 落下速度が速い場合、1物理フレームで移動する距離が tolerance を超えてしまい
        // 「床の下にいる」と誤認されて衝突判定が外れるのを防ぎます。
        float verticalVelocity = playerFacade != null ? playerFacade.CurrentVelocity.y : 0f;
        bool playerFallingOrStill = verticalVelocity <= 0.01f;
        float currentTolerance = tolerance;
        if (playerFallingOrStill && verticalVelocity < -0.1f)
        {
            // 現在の落下速度に基づき、次フレームの移動距離を予測して許容範囲を広げる（最大 1.0m 拡張）
            float fallDistancePerFrame = Mathf.Abs(verticalVelocity) * Time.fixedDeltaTime;
            currentTolerance += Mathf.Min(1.0f, fallDistancePerFrame * 1.5f);
        }

        // 下からの通過を許可しない場合は、床を通常の固体として扱う。
        // ただし allowDropThrough=true なら、上にいる時の下入力によるすり抜けは許可する。
        if (!AllowsPassThroughFromBelow())
        {
            bool playerAboveForDropThrough = playerBottom >= platformTop - currentTolerance;
            if (allowDropThrough && playerAboveForDropThrough && playerFacade != null && playerFacade.IsDownInputHeld)
            {
                dropThroughTimer = dropThroughDuration;
                SetPlatformEnabled(false);
                return;
            }

            wasBelowPlatform = false;
            SetPlatformEnabled(true);
            return;
        }

        // 下から通過中にステップ等で床内部へ入り、足元が床上面を超えないまま下降へ転じるケースを拾う。
        // ステップ中は水平移動を優先し、終了後に床上側へ入っていれば Collider を戻す。
        bool isDashActive = playerFacade != null && playerFacade.IsDashActive;
        float playerCenterY = playerCollider.bounds.center.y;
        bool shouldCatchFromInside = wasBelowPlatform
            && !isDashActive
            && playerFallingOrStill
            && playerCenterY >= platformTop + fromBelowCatchCenterMargin;

        if (shouldCatchFromInside)
        {
            wasBelowPlatform = false;
            SetPlatformEnabled(true);
            return;
        }

        // 下からすり抜け中かどうかの状態を更新する。
        if (playerBottom < platformTop - currentTolerance)
        {
            wasBelowPlatform = true;
        }
        else if (playerBottom >= platformTop)
        {
            wasBelowPlatform = false;
        }

        bool playerAbove = playerBottom >= platformTop - currentTolerance;

        // まだ下から抜け切っていない間は「上にいる」と判定しない。
        // （ジャンプの頂点や、部屋遷移時の強制速度ゼロ化、ダッシュ時の速度変化などによる誤着地を防ぐため）
        if (wasBelowPlatform)
        {
            playerAbove = false;
        }

        // 落下入力によるすり抜け判定。
        // プレイヤーが床の上にいて、下入力を入れている場合にすり抜けを開始する。
        if (allowDropThrough && playerAbove && playerFacade != null && playerFacade.IsDownInputHeld)
        {
            dropThroughTimer = dropThroughDuration;
            SetPlatformEnabled(false);
            return;
        }

        // すでに接地中で床上面（許容範囲内）にいる場合は、強制的に上にいるとみなす。
        // （リスポーン等により wasBelowPlatform が不正に残っていても上書きで解除する）
        if (playerFacade != null && playerFacade.IsGrounded && (playerBottom >= platformTop - currentTolerance))
        {
            wasBelowPlatform = false;
            SetPlatformEnabled(true);
            return;
        }

        // 最終判定: プレイヤーが上にいればコライダー ON、それ以外は OFF。
        SetPlatformEnabled(playerAbove);
    }

    private bool AllowsPassThroughFromBelow()
    {
        switch (passThroughFromBelow)
        {
            case PassThroughFromBelowMode.Allow:
                return true;
            case PassThroughFromBelowMode.Block:
                return false;
            default:
                return allowDropThrough;
        }
    }

    // ──────────────────────────────────────────────
    // プレイヤー検索
    // ──────────────────────────────────────────────

    private void TryFindPlayer()
    {
        // 静的キャッシュが有効なら再検索をスキップする。
        // Destroy されたオブジェクトは Unity の null 比較で検出される。
        if (s_cachedFacade != null && s_cachedCollider != null)
        {
            playerFacade = s_cachedFacade;
            playerCollider = s_cachedCollider;
            return;
        }

        // プレイヤーが存在しないシーン（MapEditor 等）で全インスタンスが
        // 毎 FixedUpdate に FindObjectsByType を呼ぶのを防ぐため、
        // 1フレームにつき最大1回だけ検索する。
        int currentFrame = Time.frameCount;
        if (currentFrame == s_lastSearchFrame) return;
        s_lastSearchFrame = currentFrame;

        PlayerFacade[] facades = FindObjectsByType<PlayerFacade>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        if (facades.Length == 0) return;

        s_cachedFacade = facades[0];
        s_cachedCollider = s_cachedFacade.GetComponent<Collider>();

        playerFacade = s_cachedFacade;
        playerCollider = s_cachedCollider;
    }

    // 現在の位置関係から、プレイヤーが床の下側にいるかを再計算する。
    private void RecalculateWasBelowPlatform()
    {
        if (playerCollider == null || platformCollider == null)
        {
            wasBelowPlatform = false;
            return;
        }

        float platformTop = GetPlatformTop();
        float playerBottom = playerCollider.bounds.min.y;
        wasBelowPlatform = playerBottom < platformTop - tolerance;
    }

    // ──────────────────────────────────────────────
    // コライダー有効/無効の切り替え
    // ──────────────────────────────────────────────

    // 床のコライダーの有効/無効のみを切り替える。
    // プレイヤーの物理状態には一切干渉しない。
    private void SetPlatformEnabled(bool shouldEnable)
    {
        if (platformCollider == null) return;
        if (platformCollider.enabled == shouldEnable) return;

        platformCollider.enabled = shouldEnable;
    }

    // ──────────────────────────────────────────────
    // 床上面の位置取得
    // ──────────────────────────────────────────────

    // コライダーが無効になっていると bounds が信頼できないため、
    // Transform ベースで床上面のワールド Y 座標を計算する。
    private float GetPlatformTop()
    {
        if (platformCollider is BoxCollider box)
        {
            // BoxCollider: center + half-size の Y をワールド変換する。
            Vector3 localTop = box.center + new Vector3(0f, box.size.y * 0.5f, 0f);
            return transform.TransformPoint(localTop).y;
        }

        // フォールバック: コライダーが有効なら bounds を使う。
        if (platformCollider.enabled)
        {
            return platformCollider.bounds.max.y;
        }

        // コライダー無効かつ BoxCollider 以外の場合は Transform 基準で概算する。
        return transform.position.y + transform.lossyScale.y * 0.5f;
    }
}
