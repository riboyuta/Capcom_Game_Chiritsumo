using System;
using System.Collections.Generic;
using UnityEngine;

// 責務:
// - chainPoints（各ワープ後の手位置列）の真実を保持する
// - ワープ発生時にのみ新しい点を 1 つ追加する
// - maxSegmentCount を「節数上限」として維持する
//
// 非責務:
// - 毎フレーム距離計算による自動節生成
// - world / logic plane 変換
// - view 更新 / pooling / MonoBehaviour 更新
//
// 依存先:
// - List<Vector2>: 節点列の保持に使用
// - Mathf: 節数計算と上限安全化に使用
// - float.IsFinite: 非有限値ガードに使用
//
// 前提条件:
// - Reset() 後に PushWarpPoint() を呼ぶ
// - maxSegmentCount は「点数」ではなく「節数（点間本数）」の上限として扱う
// - 1 回のワープに対して 1 点だけ追加する運用を前提にする
public sealed class ArmChainModel
{
    // =====================================================================
    // 定数
    // =====================================================================

    // 同一点とみなすための許容誤差。
    // ワープ後位置が末尾点とほぼ同じときは重複追加を防ぐ。
    private const float PointEqualityEpsilon = 0.0001f;

    // =====================================================================
    // 実行時状態
    // =====================================================================

    // chainPoints は「各ワープ後の手位置列」。
    // 先頭が最古、末尾が最新。
    private readonly List<Vector2> chainPoints = new();

    // 将来の調整用に保持する節基準長さ。
    // 現在の実装では距離ベース自動追加には使わない。
    private float segmentLength;

    // maxSegmentCount は「点数」ではなく「節数（点間本数）」の上限。
    private int maxSegmentCount;

    // 末尾点と常に一致させる最新確定点。
    private Vector2 lastCommittedPoint;

    // Reset 済みかどうか。
    // 初期化前の Push を防ぐガードに使う。
    private bool isInitialized;

    // =====================================================================
    // 公開参照口
    // =====================================================================

    public IReadOnlyList<Vector2> ChainPoints => chainPoints;
    public Vector2 LastCommittedPoint => lastCommittedPoint;
    public float SegmentLength => segmentLength;
    public int MaxSegmentCount => maxSegmentCount;

    // 節数は「点数 - 1」で求める。
    // 点が 0 個または 1 個のときは節 0 本として扱う。
    public int SegmentCount => Mathf.Max(0, chainPoints.Count - 1);

    public bool IsInitialized => isInitialized;

    // =====================================================================
    // 公開操作
    // =====================================================================

    // モデルを初期化する。
    // 初期点を 1 つだけ持つ状態から開始し、節数上限も即時反映する。
    public void Reset(Vector2 initialPoint, float segmentLength, int maxSegmentCount)
    {
        chainPoints.Clear();

        this.segmentLength = segmentLength;
        this.maxSegmentCount = Mathf.Max(0, maxSegmentCount);

        chainPoints.Add(initialPoint);
        lastCommittedPoint = initialPoint;
        isInitialized = true;

        TrimToMaxSegmentCount();
    }

    // 新しいワープ後位置を 1 点だけ追加する。
    // 毎フレーム距離からの自動生成は行わない。
    // 戻り値は今回追加した点数（0 or 1）。
    public int PushWarpPoint(Vector2 newPoint)
    {
        if (!isInitialized)
        {
            return 0;
        }

        if (!IsFinite(newPoint))
        {
            return 0;
        }

        // 念のため点列が空なら、そのまま初回点として受ける。
        if (chainPoints.Count == 0)
        {
            chainPoints.Add(newPoint);
            lastCommittedPoint = newPoint;
            TrimToMaxSegmentCount();
            return 1;
        }

        Vector2 tail = chainPoints[chainPoints.Count - 1];

        // 末尾点とほぼ同じ位置なら重複点を増やさない。
        if (NearlyEqual(tail, newPoint, PointEqualityEpsilon))
        {
            return 0;
        }

        chainPoints.Add(newPoint);
        lastCommittedPoint = newPoint;

        TrimToMaxSegmentCount();
        return 1;
    }

    // 旧 API との互換入口。
    // 以前の距離ベース自動生成は廃止し、現在は PushWarpPoint に委譲するだけにしている。
    [Obsolete("Use PushWarpPoint(newPoint). CommitHeadPoint no longer performs distance-based auto generation.")]
    public int CommitHeadPoint(Vector2 currentPalmPoint)
    {
        return PushWarpPoint(currentPalmPoint);
    }

    // モデルを完全に空状態へ戻す。
    public void Clear()
    {
        chainPoints.Clear();
        segmentLength = 0f;
        maxSegmentCount = 0;
        lastCommittedPoint = Vector2.zero;
        isInitialized = false;
    }

    // =====================================================================
    // 内部補助
    // =====================================================================

    // Vector2 の両成分が有限値かどうかを判定する。
    private static bool IsFinite(Vector2 value)
    {
        return float.IsFinite(value.x) && float.IsFinite(value.y);
    }

    // 2 点がほぼ同じ位置かどうかを判定する。
    // 平方距離で比較して sqrt を避ける。
    private static bool NearlyEqual(Vector2 a, Vector2 b, float epsilon)
    {
        float dx = a.x - b.x;
        float dy = a.y - b.y;
        return (dx * dx) + (dy * dy) <= epsilon * epsilon;
    }

    // SegmentCount が maxSegmentCount を超える間、先頭を削除して最新側を残す。
    // 古い節から捨てることで、「最新のワープ履歴」を優先して保持する。
    private void TrimToMaxSegmentCount()
    {
        maxSegmentCount = Mathf.Max(0, maxSegmentCount);

        while (SegmentCount > maxSegmentCount && chainPoints.Count > 0)
        {
            chainPoints.RemoveAt(0);
        }

        if (chainPoints.Count > 0)
        {
            lastCommittedPoint = chainPoints[chainPoints.Count - 1];
        }
    }
}