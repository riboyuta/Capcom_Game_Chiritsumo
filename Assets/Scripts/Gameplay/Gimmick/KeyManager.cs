using UnityEngine;

// 鍵のグループを管理するマネージャー。
// 紐づけられた全ての鍵が取得されたかどうかの状態(IsCompleted)を持つ。
public class KeyManager : MonoBehaviour, IRespawnResettable
{
    [Header("管理する鍵のリスト")]
    [Tooltip("このマネージャーが管理する鍵オブジェクトを登録します。未登録の場合、起動時にこのオブジェクトの子階層から自動検索します。")]
    [SerializeField] private KeyCollectible[] keys;

    private int collectedCount = 0;
    private int totalKeysCount = 0;
    private bool hasCapturedInitialState;

    // 全ての鍵が集まったら true になるプロパティ
    public bool IsCompleted { get; private set; }

    private void Awake()
    {
        // インスペクタで鍵が指定されていない場合、自動的に子オブジェクトから検索する
        if (keys == null || keys.Length == 0)
        {
            keys = GetComponentsInChildren<KeyCollectible>(true);
        }

        totalKeysCount = keys.Length;
        
        foreach (var key in keys)
        {
            if (key != null)
            {
                key.Initialize(this);
            }
        }
    }

    // 鍵が取得されたときに KeyCollectible から呼ばれる
    public void NotifyKeyCollected()
    {
        collectedCount++;
        
        if (totalKeysCount > 0 && collectedCount >= totalKeysCount)
        {
            IsCompleted = true;
            
            // SE: コンプリートしたときの音
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayOverlap("SFX_gimmick_switchwall"); // ※仮の音を設定。必要になれば専用音に変更してください
            }
        }
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
        // リスポーン時は取得カウントと完了状態を初期化する
        collectedCount = 0;
        IsCompleted = false;
    }
}
