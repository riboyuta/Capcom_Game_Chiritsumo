using UnityEngine;

public sealed class StageMapResettable : MonoBehaviour, IRespawnResettable
{
    [SerializeField] private StageLoader stageLoader;

    // 参照が未設定でも動くように、必要時に StageLoader を遅延解決します。
    void Awake()
    {
        ResolveStageLoaderIfNeeded();
    }

    // このコンポーネントは全再生成で復帰するため、初期状態の個別保存は行いません。
    public void CaptureInitialState()
    {
        ResolveStageLoaderIfNeeded();
    }

    // 死亡時に StageLoader へマップ全再構築を依頼します。
    public void ResetToRespawnState()
    {
        ResolveStageLoaderIfNeeded();

        if (stageLoader == null)
        {
            Debug.LogWarning("[StageMapResettable] StageLoader が見つからないため、マップ再構築をスキップします。", this);
            return;
        }

        stageLoader.RebuildStageForRespawn();
    }

    // Inspector 未設定時にシーン内から StageLoader を補完します。
    void ResolveStageLoaderIfNeeded()
    {
        if (stageLoader != null)
        {
            return;
        }

        stageLoader = FindFirstObjectByType<StageLoader>();
    }
}