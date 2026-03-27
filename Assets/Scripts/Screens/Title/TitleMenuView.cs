using UnityEngine;

public sealed class TitleMenuView : MonoBehaviour
{
    [Header("メニュー項目")]
    [Tooltip("タイトルメニュー各項目のTransform。配列順は TitleMenuItem の順番に合わせる。")]
    [SerializeField] private Transform[] itemTransforms;

    [Header("拡大縮小設定")]
    [Tooltip("未選択時の拡大率。初期スケールに対する倍率で扱う。")]
    [SerializeField] private float normalScaleMultiplier = 1.0f;

    [Tooltip("選択中項目の拡大率。初期スケールに対する倍率で扱う。")]
    [SerializeField] private float selectedScaleMultiplier = 1.15f;

    [Tooltip("現在スケールが目標スケールへ追従する速さ。大きいほど素早く変化する。")]
    [SerializeField] private float scaleFollowSpeed = 10.0f;

    private Vector3[] baseScales;
    private int selectedIndex;

    // 初期スケールを記録し、後続の拡大縮小の基準値を作る。
    private void Awake()
    {
        if (itemTransforms == null || itemTransforms.Length == 0)
        {
            Debug.LogError("[TitleMenuView] Item transforms are not assigned.", this);
            enabled = false;
            return;
        }

        baseScales = new Vector3[itemTransforms.Length];

        for (int i = 0; i < itemTransforms.Length; i++)
        {
            if (itemTransforms[i] == null)
            {
                Debug.LogError($"[TitleMenuView] Item transform is missing at index {i}.", this);
                enabled = false;
                return;
            }

            baseScales[i] = itemTransforms[i].localScale;
        }
    }

    // 毎フレーム、各項目のスケールを目標値へ滑らかに近づける。
    private void Update()
    {
        for (int i = 0; i < itemTransforms.Length; i++)
        {
            float targetMultiplier = i == selectedIndex
                ? selectedScaleMultiplier
                : normalScaleMultiplier;

            Vector3 targetScale = baseScales[i] * targetMultiplier;

            itemTransforms[i].localScale = Vector3.Lerp(
                itemTransforms[i].localScale,
                targetScale,
                scaleFollowSpeed * Time.deltaTime);
        }
    }

    // 選択中の項目番号を受け取り、以後の描画反映先を切り替える。
    public void ApplySelection(TitleSceneController.TitleMenuItem selectedItem)
    {
        selectedIndex = (int)selectedItem;
    }
}