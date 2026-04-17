using System.Collections.Generic;
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

    private int initialCollectedCount;
    private bool initialIsCompleted;

    // 全ての鍵が集まったら true になるプロパティ
    public bool IsCompleted { get; private set; }

    private void Awake()
    {
        // インスペクタで鍵が指定されていない場合、自動的に子オブジェクトから検索する
        if (keys == null || keys.Length == 0)
        {
            keys = GetComponentsInChildren<KeyCollectible>(true);
        }

        EnsureStableKeys();

        totalKeysCount = keys.Length;
        
        foreach (var key in keys)
        {
            if (key != null)
            {
                key.Initialize(this);
            }
        }
    }

    // keys 配列の null を除去して、初期管理集合を安定させる
    private void EnsureStableKeys()
    {
        if (keys == null)
        {
            keys = System.Array.Empty<KeyCollectible>();
            return;
        }

        bool hasNull = false;
        for (int i = 0; i < keys.Length; i++)
        {
            if (keys[i] == null)
            {
                hasNull = true;
                break;
            }
        }

        if (!hasNull)
        {
            return;
        }

        List<KeyCollectible> sanitizedKeys = new List<KeyCollectible>(keys.Length);
        for (int i = 0; i < keys.Length; i++)
        {
            if (keys[i] != null)
            {
                sanitizedKeys.Add(keys[i]);
            }
        }

        Debug.LogWarning($"[{nameof(KeyManager)}] null の KeyCollectible 参照を除外しました: {name}", this);
        keys = sanitizedKeys.ToArray();
    }

    // 初期の進行状態を保存する
    private void CaptureManagerInitialState()
    {
        initialCollectedCount = collectedCount;
        initialIsCompleted = IsCompleted;
    }

    // 保存した初期の進行状態を復元する
    private void RestoreManagerInitialState()
    {
        collectedCount = initialCollectedCount;
        IsCompleted = initialIsCompleted;
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
        if (hasCapturedInitialState)
        {
            return;
        }

        CaptureManagerInitialState();
        hasCapturedInitialState = true;
    }

    public void ResetToRespawnState()
    {
        if (!hasCapturedInitialState)
        {
            CaptureInitialState();
        }

        RestoreManagerInitialState();
    }
}
