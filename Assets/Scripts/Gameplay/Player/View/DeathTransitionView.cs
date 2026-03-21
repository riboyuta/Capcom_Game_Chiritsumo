using UnityEngine;
using UnityEngine.UI;

// 責務:
// - 死亡遷移用の黒オーバーレイのフェードを管理する
// - フェードイン / フェードアウトの開始要求を受ける
// - 現在の黒さを返し、復帰処理側が利用できるようにする
//
// 非責務:
// - 死亡判定やリスポーン判定は担当しない
// - ゲーム停止やシーン遷移は担当しない
// - UI オブジェクトの生成は担当しない
//
// 依存先:
// - CanvasGroup: オーバーレイ全体の alpha 制御先
// - Image: CanvasGroup が無い場合の alpha 制御先
// - Time.deltaTime: フェード更新に使用
//
// 前提条件:
// - 黒オーバーレイとして使う CanvasGroup または Image が存在する
// - Update が毎フレーム呼ばれる
// - フェード開始要求は外部から PlayTransitionIn / PlayTransitionOut で行う
[DisallowMultipleComponent]
public sealed class DeathTransitionView : MonoBehaviour
{
    // =====================================================================
    // Inspector 設定値
    // =====================================================================

    [Header("参照: 黒オーバーレイ CanvasGroup")]
    [Tooltip("黒フェード制御に使う CanvasGroup です。alpha を直接変更して画面全体の黒さを制御します。未設定時は同一 GameObject から取得を試み、見つかれば CanvasGroup 側を優先して使います。")]
    [SerializeField] private CanvasGroup overlayCanvasGroup;

    [Header("参照: 黒オーバーレイ Image")]
    [Tooltip("CanvasGroup が無い場合に alpha 制御へ使う黒 Image です。CanvasGroup 未使用構成の fallback として使います。未設定時は同一 GameObject から取得を試みますが、CanvasGroup がある場合はこちらは使われません。")]
    [SerializeField] private Image overlayImage;

    [Header("フェード: 黒へ入る時間(秒)")]
    [Tooltip("透明から黒へ入る時間です。PlayTransitionIn 実行中の補間速度に使います。大きくするとゆっくり暗転し、小さくすると素早く暗転します。0 の場合は即時で黒になります。")]
    [SerializeField, Min(0f)] private float fadeInDuration = 0.2f;

    [Header("フェード: 黒から抜ける時間(秒)")]
    [Tooltip("黒から透明へ戻る時間です。PlayTransitionOut 実行中の補間速度に使います。大きくするとゆっくり復帰し、小さくすると素早く復帰します。0 の場合は即時で透明になります。")]
    [SerializeField, Min(0f)] private float fadeOutDuration = 0.25f;

    [Header("判定しきい値: 復帰隠蔽用の黒さ")]
    [Tooltip("現在の黒さがこの値以上なら『復帰処理を画面上で隠せる』とみなす判定しきい値です。外部が GetBlackAmount と組み合わせて使う想定で、値を下げると早い段階で復帰処理に入れ、値を上げるとより十分に暗くなるまで待つようになります。")]
    [SerializeField, Range(0f, 1f)] private float blackRespawnThreshold = 0.85f;

    // =====================================================================
    // 実行時状態
    // =====================================================================

    // フェード進行状態。
    // Update でどの方向へ補間するかを決めるための最小状態だけを持つ。
    private enum TransitionState
    {
        Idle,
        FadingIn,
        FadingOut
    }

    // 現在の遷移状態。
    // PlayTransitionIn / PlayTransitionOut で切り替わり、目標 alpha 到達で Idle に戻る。
    private TransitionState state = TransitionState.Idle;

    // =====================================================================
    // 公開参照口
    // =====================================================================

    // 復帰処理を隠せる黒さのしきい値。
    // 判定そのものは外部で行い、この View は値の保持だけを担当する。
    public float BlackRespawnThreshold => blackRespawnThreshold;

    // =====================================================================
    // 初期化
    // =====================================================================

    // 起動時に参照を補完し、オーバーレイを透明な待機状態へ戻す。
    // シーン開始直後に黒が残らないことを保証するための初期化。
    private void Awake()
    {
        ResolveReferencesIfNeeded();
        ResetTransitionImmediate();
    }

    // =====================================================================
    // 毎フレーム更新
    // =====================================================================

    // 現在の遷移状態に応じて alpha を更新する。
    // 補間方向の決定だけをここで行い、実際の計算は UpdateFadeToward に委譲する。
    private void Update()
    {
        switch (state)
        {
            case TransitionState.FadingIn:
                UpdateFadeToward(1f, fadeInDuration);
                break;

            case TransitionState.FadingOut:
                UpdateFadeToward(0f, fadeOutDuration);
                break;
        }
    }

    // =====================================================================
    // 公開操作
    // =====================================================================

    // 黒フェードインを開始する。
    // 参照未設定でも自己補完を試みてから開始する。
    public void PlayTransitionIn()
    {
        ResolveReferencesIfNeeded();
        state = TransitionState.FadingIn;
    }

    // 黒フェードアウトを開始する。
    // 参照未設定でも自己補完を試みてから開始する。
    public void PlayTransitionOut()
    {
        ResolveReferencesIfNeeded();
        state = TransitionState.FadingOut;
    }

    // 現在の黒さを 0〜1 の範囲で返す。
    // CanvasGroup を優先し、無い場合は Image の alpha を参照する。
    public float GetBlackAmount()
    {
        if (overlayCanvasGroup != null)
        {
            return Mathf.Clamp01(overlayCanvasGroup.alpha);
        }

        if (overlayImage != null)
        {
            return Mathf.Clamp01(overlayImage.color.a);
        }

        return 0f;
    }

    // フェード状態を即時リセットし、透明状態へ戻す。
    // シーン初期化や強制復帰時に、途中補間を残さないために使う。
    public void ResetTransitionImmediate()
    {
        state = TransitionState.Idle;
        SetAlphaImmediate(0f);
    }

    // =====================================================================
    // 補助処理
    // =====================================================================

    // 現在 alpha から目標 alpha へ向かって補間する。
    // duration が 0 以下なら即時反映し、到達したら Idle に戻す。
    private void UpdateFadeToward(float targetAlpha, float duration)
    {
        float current = GetBlackAmount();

        if (duration <= 0f)
        {
            SetAlphaImmediate(targetAlpha);
            state = TransitionState.Idle;
            return;
        }

        float next = Mathf.MoveTowards(current, targetAlpha, Time.deltaTime / duration);
        SetAlphaImmediate(next);

        if (Mathf.Approximately(next, targetAlpha))
        {
            state = TransitionState.Idle;
        }
    }

    // alpha を即時反映する。
    // CanvasGroup があればそちらを優先し、無ければ Image の色 alpha を直接変更する。
    private void SetAlphaImmediate(float alpha)
    {
        float clamped = Mathf.Clamp01(alpha);

        if (overlayCanvasGroup != null)
        {
            overlayCanvasGroup.alpha = clamped;
            return;
        }

        if (overlayImage != null)
        {
            Color color = overlayImage.color;
            color.a = clamped;
            overlayImage.color = color;
        }
    }

    // 未設定参照の自動補完を行う。
    // 既存設定を壊さないため、未設定時だけ同一 GameObject から取得を試みる。
    private void ResolveReferencesIfNeeded()
    {
        if (overlayCanvasGroup == null)
        {
            overlayCanvasGroup = GetComponent<CanvasGroup>();
        }

        if (overlayImage == null)
        {
            overlayImage = GetComponent<Image>();
        }
    }
}