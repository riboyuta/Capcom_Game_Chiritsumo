using UnityEngine;

// 責務:
// - プレイヤー死亡時の見た目演出を管理する
// - Damage 死亡時の倒れイントロ演出を再生する
// - DeathCause に応じた黒トランジション開始 / 終了を DeathTransitionView へ中継する
// - 回転対象となる PlayerView.ViewRoot を解決し、演出後に見た目を初期状態へ戻す
//
// 非責務:
// - 死亡判定や死亡開始条件の判断は担当しない
// - リスポーン位置決定やチェックポイント管理は担当しない
// - カメラ本体の制御やズーム適用本体は担当しない
//
// 依存先:
// - PlayerView: 倒れ演出の回転対象取得に使用
// - DeathTransitionView: 黒フェードの表示制御に使用
// - PlayerController.DeathCause: Damage / Hazard の演出分岐キーとして使用
//
// 前提条件:
// - PlayerView.ViewRoot が倒れ演出の回転対象として使える
// - DeathTransitionView 側に PlayTransitionIn(duration) / PlayTransitionOut(duration) が存在する
// - Update が毎フレーム呼ばれ、イントロ進行を継続できる
[DisallowMultipleComponent]
public sealed class PlayerDeathView : MonoBehaviour
{
    // =====================================================================
    // Inspector 設定値
    // =====================================================================

    [Header("参照: PlayerView")]
    [Tooltip("倒れ演出の回転対象を取得するための PlayerView 参照です。GetRotationTarget で ViewRoot 解決に使います。未設定時は同一 GameObject と子階層から探索を試み、見つからない場合は倒れ演出を再生できません。")]
    [SerializeField] private PlayerView playerView;

    [Header("参照: DeathTransitionView")]
    [Tooltip("死亡時の黒トランジション表示を担当する View 参照です。PlayTransitionIn / PlayTransitionOut / GetBlackAmount / ResetTransitionImmediate で使います。未設定時は同一 GameObject と子階層から探索を試みます。")]
    [SerializeField] private DeathTransitionView deathTransitionView;

    [Header("Damage演出: イントロ時間(秒)")]
    [Tooltip("DeathCause.Damage 時に倒れ演出を進める時間です。PlayDamageDeathIntro 後の Update で進行に使います。長くするとゆっくり倒れ、小さくすると素早く倒れます。0 の場合は即時で完了します。")]
    [Min(0f)]
    [SerializeField] private float damageDeathIntroDuration = 0.12f;

    [Header("Damage演出: 倒れ角度")]
    [Tooltip("DeathCause.Damage 時に viewRoot を Z 回転でどこまで倒すかの目標角度です。Update 中の補間終点として使います。大きくすると大きく倒れ、小さくすると控えめな倒れ方になります。")]
    [Range(0f, 120f)]
    [SerializeField] private float damageDeathTiltAngle = 80f;

    [Header("Damage演出: ズーム量オフセット")]
    [Tooltip("DeathCause.Damage 時に現在の orthographicSize へ加算するズーム量です。主にカメラ側のズーム上書き値として参照されます。負値にするとズームイン寄りになり、正値にするとズームアウト寄りになります。")]
    [SerializeField] private float damageDeathZoomSizeOffset = -0.35f;

    [Header("Damage演出: ズーム補間時間(秒)")]
    [Tooltip("DeathCause.Damage 時にズーム上書きへ切り替える際の補間時間です。主にカメラ側で参照される設定値です。大きくするとゆっくり切り替わり、小さくすると素早く切り替わります。0 で即時反映です。")]
    [Min(0f)]
    [SerializeField] private float damageDeathZoomSmoothTime = 0.08f;

    [Header("Damage遷移: 黒入り時間(秒)")]
    [Tooltip("DeathCause.Damage 時の透明から黒へ入る時間です。PlayTransitionIn(Damage) から DeathTransitionView へ渡します。長くするとゆっくり暗転し、小さくすると素早く暗転します。")]
    [Min(0f)]
    [SerializeField] private float damageBlackInDuration = 0.2f;

    [Header("Damage遷移: 黒戻り時間(秒)")]
    [Tooltip("DeathCause.Damage 時の黒から透明へ戻る時間です。PlayTransitionOut(Damage) から DeathTransitionView へ渡します。長くするとゆっくり復帰し、小さくすると素早く復帰します。")]
    [Min(0f)]
    [SerializeField] private float damageBlackOutDuration = 0.25f;

    [Header("Hazard遷移: 黒入り時間(秒)")]
    [Tooltip("DeathCause.Hazard 時の透明から黒へ入る時間です。PlayTransitionIn(Hazard) から DeathTransitionView へ渡します。Damage 用より短くすると、奈落や即死ギミック向けの素早い暗転にできます。")]
    [Min(0f)]
    [SerializeField] private float hazardBlackInDuration = 0.12f;

    [Header("Hazard遷移: 黒戻り時間(秒)")]
    [Tooltip("DeathCause.Hazard 時の黒から透明へ戻る時間です。PlayTransitionOut(Hazard) から DeathTransitionView へ渡します。Damage 用より短くすると、素早い復帰演出にできます。")]
    [Min(0f)]
    [SerializeField] private float hazardBlackOutDuration = 0.16f;

    [Header("判定しきい値: 復帰隠蔽用の黒さ")]
    [Tooltip("現在の黒さがこの値以上なら復帰処理を隠せるとみなすしきい値です。外部が GetBlackAmount と組み合わせて使う前提です。値を下げると早い段階で復帰に入れ、値を上げると十分に暗くなるまで待つようになります。")]
    [Range(0f, 1f)]
    [SerializeField] private float blackRespawnThreshold = 0.85f;

    // =====================================================================
    // 実行時状態
    // =====================================================================

    // イントロ再生中かどうか。
    // true の間だけ Update で倒れ演出を進める。
    private bool isIntroPlaying;

    // イントロ完了済みかどうか。
    // 外部が IsIntroComplete で進行確認するための観測状態。
    private bool isIntroComplete;

    // 現在のイントロ経過時間。
    // introDuration に対する進行率計算に使う。
    private float introElapsed;

    // 現在有効なイントロ時間設定。
    // Awake と ConfigureDamageDeathIntro で正規化した値を保持する。
    private float introDuration;

    // 現在有効な倒れ目標角度。
    // Awake と ConfigureDamageDeathIntro で clamp 後の値を保持する。
    private float introTiltAngle;

    // =====================================================================
    // 初期化
    // =====================================================================

    // 参照の自動補完と、イントロ設定の正規化を行う。
    // Inspector 未設定でも最低限の自己補完を試み、演出進行で使う内部値を初期化する。
    private void Awake()
    {
        ResolveReferencesIfNeeded();

        introDuration = Mathf.Max(0f, damageDeathIntroDuration);
        introTiltAngle = Mathf.Clamp(damageDeathTiltAngle, 0f, 120f);
    }

    // =====================================================================
    // 毎フレーム更新
    // =====================================================================

    // Damage 死亡時の倒れイントロを進行する。
    // イントロ再生中だけ回転対象へ Z 回転を補間適用し、終点到達で完了状態へ移る。
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

    // =====================================================================
    // 公開設定反映
    // =====================================================================

    // Damage 死亡イントロの時間と倒れ角度を外部から上書きする。
    // 正規化済みの内部値へ反映し、次回再生に使う。
    public void ConfigureDamageDeathIntro(float duration, float tiltAngle)
    {
        introDuration = Mathf.Max(0f, duration);
        introTiltAngle = Mathf.Clamp(tiltAngle, 0f, 120f);
    }

    // =====================================================================
    // 公開参照口
    // 外部の死亡シーケンスやカメラ制御が参照する設定値。
    // =====================================================================

    public float DamageDeathIntroDuration => Mathf.Max(0f, damageDeathIntroDuration);
    public float DamageDeathTiltAngle => Mathf.Clamp(damageDeathTiltAngle, 0f, 120f);
    public float DamageDeathZoomSizeOffset => damageDeathZoomSizeOffset;
    public float DamageDeathZoomSmoothTime => Mathf.Max(0f, damageDeathZoomSmoothTime);
    public float DamageBlackInDuration => Mathf.Max(0f, damageBlackInDuration);
    public float DamageBlackOutDuration => Mathf.Max(0f, damageBlackOutDuration);
    public float HazardBlackInDuration => Mathf.Max(0f, hazardBlackInDuration);
    public float HazardBlackOutDuration => Mathf.Max(0f, hazardBlackOutDuration);
    public float BlackRespawnThreshold => Mathf.Clamp01(blackRespawnThreshold);

    // =====================================================================
    // 死亡イントロ制御
    // =====================================================================

    // Damage 死亡時の倒れイントロを開始する。
    // 回転対象があれば初期姿勢から再生し、無ければ即完了扱いにして停止する。
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

    // 死亡見た目を通常状態へ戻す。
    // 倒れ回転とイントロ進行状態をクリアし、次回死亡演出の開始条件を整える。
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

    // Damage 死亡イントロが完了したかどうかを返す。
    // 死亡シーケンス側が次工程へ進む条件判定に使う想定。
    public bool IsIntroComplete()
    {
        return isIntroComplete;
    }

    // =====================================================================
    // 黒トランジション中継
    // =====================================================================

    // DeathCause に応じた黒入り時間を選び、DeathTransitionView へ開始要求を中継する。
    public void PlayTransitionIn(PlayerController.DeathCause cause)
    {
        if (deathTransitionView == null)
        {
            deathTransitionView = GetComponentInChildren<DeathTransitionView>();
        }

        float duration = cause == PlayerController.DeathCause.Hazard
            ? HazardBlackInDuration
            : DamageBlackInDuration;

        deathTransitionView?.PlayTransitionIn(duration);
    }

    // DeathCause に応じた黒戻り時間を選び、DeathTransitionView へ終了要求を中継する。
    public void PlayTransitionOut(PlayerController.DeathCause cause)
    {
        if (deathTransitionView == null)
        {
            deathTransitionView = GetComponentInChildren<DeathTransitionView>();
        }

        float duration = cause == PlayerController.DeathCause.Hazard
            ? HazardBlackOutDuration
            : DamageBlackOutDuration;

        deathTransitionView?.PlayTransitionOut(duration);
    }

    // 現在の黒さを返す。
    // DeathTransitionView が未解決なら探索を試み、無ければ 0 を返す。
    public float GetBlackAmount()
    {
        if (deathTransitionView == null)
        {
            deathTransitionView = GetComponentInChildren<DeathTransitionView>();
        }

        return deathTransitionView != null ? deathTransitionView.GetBlackAmount() : 0f;
    }

    // 黒トランジションを即時で待機状態へ戻す。
    public void ResetTransitionImmediate()
    {
        if (deathTransitionView == null)
        {
            deathTransitionView = GetComponentInChildren<DeathTransitionView>();
        }

        deathTransitionView?.ResetTransitionImmediate();
    }

    // =====================================================================
    // 参照補完 / 補助処理
    // =====================================================================

    // 未設定参照の自動補完を行う。
    // 既存設定を壊さないため、未設定時だけ同一 GameObject と子階層から取得を試みる。
    private void ResolveReferencesIfNeeded()
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
    }

    // 倒れ演出の回転対象を解決する。
    // PlayerView が未解決なら探索を試み、最終的に ViewRoot を返す。
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