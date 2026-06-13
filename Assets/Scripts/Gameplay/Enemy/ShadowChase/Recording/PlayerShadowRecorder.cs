using System.Collections.Generic;
using UnityEngine;

// プレイヤーの追尾用 snapshot を一定間隔で履歴保存する。
// 記録は LateUpdate で行い、PlayerController 側で確定済みの
// CurrentVisualState を取りやすくする。
// PlayerController 本体へ記録呼び出しを追加しない構成。
[DisallowMultipleComponent]
public sealed class PlayerShadowRecorder : MonoBehaviour
{
    [Header("オブジェクト参照")]
    [Tooltip("snapshot を生成する PlayerController です。未設定時は同一 GameObject から取得を試みます。")]
    [SerializeField] private PlayerController playerController;

    [Header("履歴記録設定")]
    [Tooltip("何秒ごとに snapshot を記録するかです。小さいほど滑らかですが履歴数は増えます。")]
    [SerializeField] private float recordInterval = 0.02f;

    [Tooltip("履歴を何秒分保持するかです。ShadowChaserEnemy の delayTime より長くしてください。")]
    [SerializeField] private float maxHistoryDuration = 2.0f;

    [Header("デバッグ表示設定")]
    [Tooltip("履歴ラインを Scene 上に表示します。")]
    [SerializeField] private bool showDebugPath = true;

    [Tooltip("履歴ラインの色です。")]
    [SerializeField] private Color debugPathColor = Color.magenta;

    [Header("実行時デバッグ情報")]
    [Tooltip("最後に記録した snapshot の時刻です。")]
    [SerializeField] private float lastRecordedTime;

    [Tooltip("現在保持している履歴数です。")]
    [SerializeField] private int debugHistoryCount;

    private readonly List<PlayerShadowSnapshot> history = new List<PlayerShadowSnapshot>();

    // 次に記録可能になる時刻。
    private float nextRecordTime = 0.0f;

    // 履歴の有効な開始インデックス（パフォーマンス最適化: RemoveRange を避けるため）
    private int historyStartIndex = 0;

    public IReadOnlyList<PlayerShadowSnapshot> History => history;

    public PlayerController TargetPlayerController => playerController;

    // 初期化処理。
    // PlayerController の参照を取得し、記録間隔の設定を行う。
    private void Awake()
    {
        // PlayerController が未設定なら同一 GameObject から取得
        if (playerController == null)
        {
            playerController = GetComponent<PlayerController>();
        }

        // PlayerController が見つからなければエラー
        if (playerController == null)
        {
            Debug.LogError("PlayerShadowRecorder には PlayerController が必要です。", this);
            enabled = false;
            return;
        }

        // 記録間隔と履歴保持時間を最小値以上に設定
        recordInterval = Mathf.Max(0.001f, recordInterval);
        maxHistoryDuration = Mathf.Max(recordInterval, maxHistoryDuration);

        ResetHistoryToCurrent();
    }

    // LateUpdate で記録を行うことで、フレーム内の最新状態を取得する。
    // 一定間隔で snapshot を記録し、古い履歴を削除する。
    private void LateUpdate()
    {
        if (!enabled)
        {
            return;
        }

        if (playerController == null)
        {
            return;
        }

        float now = Time.time;

        // 次の記録時刻に達していなければスキップ
        if (now < nextRecordTime)
        {
            debugHistoryCount = history.Count - historyStartIndex;
            return;
        }

        // snapshot を記録し、古い履歴を削除
        RecordSnapshot(now);
        TrimOldHistory(now);

        // 次の記録時刻を更新。長いフレームでも極端に取りこぼしすぎないように基準時刻を積み上げる。
        if (nextRecordTime <= 0f)
        {
            nextRecordTime = now + recordInterval;
        }
        else
        {
            while (nextRecordTime <= now)
            {
                nextRecordTime += recordInterval;
            }
        }

        debugHistoryCount = history.Count - historyStartIndex;
    }

    private void OnEnable()
    {
        if (playerController == null)
        {
            return;
        }

        ResetHistoryToCurrent();
    }

    private void OnDisable()
    {
        ClearHistory();
    }

    // 現在の PlayerController の状態を snapshot として記録する。
    private void RecordSnapshot(float currentTime)
    {
        PlayerShadowSnapshot snapshot = playerController.CaptureShadowSnapshot();

        // snapshot 側の time は CaptureShadowSnapshot 側で入れている想定だが、
        // 念のため現在時刻で上書きして履歴時刻を統一する。
        snapshot.time = currentTime;

        history.Add(snapshot);
        lastRecordedTime = currentTime;

        // 一定数溜まったら物理削除してメモリを解放
        if (history.Count > 1000 && historyStartIndex > 500)
        {
            history.RemoveRange(0, historyStartIndex);
            historyStartIndex = 0;
        }
    }

    // 追尾履歴をすべて破棄する。
    // リスポーンや強制ワープ直後に、死亡前・別部屋の履歴を参照しないようにする。
    public void ClearHistory()
    {
        history.Clear();
        historyStartIndex = 0;

        lastRecordedTime = 0f;
        debugHistoryCount = 0;

        nextRecordTime = Time.time;
    }

    public void ResetHistoryToPosition(Vector3 worldPosition)
    {
        ClearHistory();
        SeedSnapshotAtPosition(Time.time, worldPosition);
    }

    private void SeedSnapshotAtPosition(float currentTime, Vector3 worldPosition)
    {
        if (playerController == null)
        {
            return;
        }

        PlayerShadowSnapshot snapshot = playerController.CaptureShadowSnapshot();

        snapshot.time = currentTime;
        snapshot.position = worldPosition;

        history.Add(snapshot);

        historyStartIndex = 0;
        lastRecordedTime = currentTime;
        debugHistoryCount = history.Count;

        nextRecordTime = currentTime + recordInterval;
    }

    // 現在位置を起点として履歴を作り直す。
    // Clear だけだと delayTime 分の履歴が溜まるまで Shadow が目標を取れないため、現在 snapshot を 1 件入れる。
    public void ResetHistoryToCurrent()
    {
        ClearHistory();
        SeedCurrentSnapshot(Time.time);
    }

    // 現在の PlayerController 状態を履歴に 1 件だけ追加する。
    public void SeedCurrentSnapshot(float currentTime)
    {
        if (playerController == null)
        {
            return;
        }

        PlayerShadowSnapshot snapshot = playerController.CaptureShadowSnapshot();
        snapshot.time = currentTime;

        history.Add(snapshot);

        historyStartIndex = 0;
        lastRecordedTime = currentTime;
        debugHistoryCount = history.Count;

        nextRecordTime = currentTime + recordInterval;
    }

    // maxHistoryDuration より古い履歴を削除する。
    // 履歴が無限に増えないようにする。
    // パフォーマンス最適化: RemoveRange を避けてインデックスで管理
    private void TrimOldHistory(float currentTime)
    {
        float thresholdTime = currentTime - maxHistoryDuration;

        // 有効な開始インデックスを更新（O(1) 操作）
        int count = history.Count;
        while (historyStartIndex < count && history[historyStartIndex].time < thresholdTime)
        {
            historyStartIndex++;
        }
    }

    // delay 秒前の snapshot を返す。
    // useInterpolation=true なら前後 2 点から補間する。
    public bool TryGetSnapshotAtDelay(float delay, bool useInterpolation, out PlayerShadowSnapshot snapshot)
    {
        float targetTime = Time.time - Mathf.Max(0.0f, delay);
        return TryGetSnapshotAtTime(targetTime, useInterpolation, out snapshot);
    }

    // 指定時刻の snapshot を返す。
    // useInterpolation=true なら前後 2 点から補間する。
    public bool TryGetSnapshotAtTime(float targetTime, bool useInterpolation, out PlayerShadowSnapshot snapshot)
    {
        snapshot = default;

        int count = history.Count;
        int validCount = count - historyStartIndex;

        if (validCount <= 0)
        {
            return false;
        }

        // 履歴が 1 つだけならそれを返す
        if (validCount == 1)
        {
            snapshot = history[historyStartIndex];
            return true;
        }

        int lastIndex = count - 1;

        // 一番古い時刻より前を要求されたら最古を返す。
        if (targetTime <= history[historyStartIndex].time)
        {
            snapshot = history[historyStartIndex];
            return true;
        }

        // 一番新しい時刻より後を要求されたら最新を返す。
        if (targetTime >= history[lastIndex].time)
        {
            snapshot = history[lastIndex];
            return true;
        }

        // 履歴を検索して、指定時刻を挿む 2 点を探す（有効な範囲のみ）
        for (int i = historyStartIndex + 1; i < count; ++i)
        {
            PlayerShadowSnapshot previous = history[i - 1];
            PlayerShadowSnapshot next = history[i];

            if (targetTime > next.time)
            {
                continue;
            }

            // 補間しない場合は前の点を返す
            if (!useInterpolation)
            {
                snapshot = previous;
                return true;
            }

            // 前後 2 点を補間して返す
            float range = next.time - previous.time;
            if (range <= Mathf.Epsilon)
            {
                snapshot = previous;
                return true;
            }

            float t = Mathf.InverseLerp(previous.time, next.time, targetTime);
            snapshot = LerpSnapshot(previous, next, t);
            return true;
        }

        // ここまで来たら最新を返す
        snapshot = history[lastIndex];
        return true;
    }

    // 2 つの snapshot を補間して新しい snapshot を生成する。
    // 位置・回転・速度は線形補間、離散的なフラグは近い方を採用する。
    private PlayerShadowSnapshot LerpSnapshot(PlayerShadowSnapshot a, PlayerShadowSnapshot b, float t)
    {
        PlayerShadowSnapshot result = new PlayerShadowSnapshot();

        result.time = Mathf.Lerp(a.time, b.time, t);

        // 位置・回転・速度は補間する。
        result.position = Vector3.Lerp(a.position, b.position, t);
        result.rotation = Quaternion.Slerp(a.rotation, b.rotation, t);
        result.velocity = Vector3.Lerp(a.velocity, b.velocity, t);

        // 離散的な情報は近い方を採用する。
        bool useA = t < 0.5f;

        result.facing = useA ? a.facing : b.facing;

        // 見た目状態は補間しない。
        // Dash と Idle の中間状態のようなものは存在しないため、近い snapshot を採用する。
        result.animationSnapshot = useA
            ? a.animationSnapshot
            : b.animationSnapshot;

        return result;
    }

    private void OnDrawGizmosSelected()
    {
        if (!showDebugPath)
        {
            return;
        }

        int count = history.Count;
        int validCount = count - historyStartIndex;

        if (validCount < 2)
        {
            return;
        }

        Gizmos.color = debugPathColor;

        // 有効な範囲のみ描画
        for (int i = historyStartIndex + 1; i < count; ++i)
        {
            Gizmos.DrawLine(history[i - 1].position, history[i].position);
        }
    }
}