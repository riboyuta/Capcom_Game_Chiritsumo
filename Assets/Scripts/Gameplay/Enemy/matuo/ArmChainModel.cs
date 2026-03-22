using System.Collections.Generic;
using UnityEngine;

// 責務:
// - 腕の確定節点列を純ロジックで保持する
// - 現在手の平位置から、segmentLength ごとに新しい確定点を追加する
// - 最大節数を超えた古い点を先頭から捨てて、最新側を維持する
//
// 非責務:
// - ワールド座標との変換は担当しない
// - 見た目生成や Sprite / Mesh 更新は担当しない
// - 移動制御や当たり判定は担当しない
//
// 依存先:
// - Mathf: 安全な下限処理と節数計算に使用
// - List<Vector2>: 節点列の保持に使用
//
// 前提条件:
// - maxSegmentCount は「点数」ではなく「節数（点間の本数）」の上限として扱う
// - Reset() 後に CommitHeadPoint() を呼ぶ
// - segmentLength は 0 より十分大きい値を想定する
public sealed class ArmChainModel
{
    // =====================================================================
    // 定数
    // =====================================================================

    // 0 長さや極小距離による不安定な正規化を避けるための最小長さ。
    private const float MinSegmentLength = 0.0001f;

    // =====================================================================
    // 実行時状態
    // =====================================================================

    // 現在の確定節点列。
    // 先頭が最も古く、末尾が最新の確定点。
    private readonly List<Vector2> chainPoints = new();

    // 節 1 本あたりの基準長さ。
    // Reset 時に確定し、CommitHeadPoint の追加間隔として使う。
    private float segmentLength;

    // 許可する最大節数。
    // 「点数」ではなく「点間の本数」で管理する。
    private int maxSegmentCount;

    // 最後に確定した点。
    // 新しい点を積む基準位置として使う。
    private Vector2 lastCommittedPoint;

    // Reset 済みかどうか。
    // 初期化前の Commit を防ぐためのガード。
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
    // 初期点を 1 つだけ持つ状態から開始し、最大節数制限も即時反映する。
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

    // 現在手の平位置に向かって、segmentLength ごとに新しい確定点を追加する。
    // 戻り値は今回追加した点数。
    public int CommitHeadPoint(Vector2 currentPalmPoint)
    {
        if (!isInitialized)
        {
            return 0;
        }

        if (segmentLength <= MinSegmentLength)
        {
            return 0;
        }

        // 入力値や内部状態に非有限値が混ざっている場合は更新を止める。
        if (!IsFinite(currentPalmPoint) || !IsFinite(lastCommittedPoint))
        {
            return 0;
        }

        int addedCount = 0;

        while (true)
        {
            Vector2 delta = currentPalmPoint - lastCommittedPoint;
            float distance = delta.magnitude;

            // 次の 1 点を置くのに必要な距離が足りないなら終了する。
            if (distance < segmentLength)
            {
                break;
            }

            // 極小距離での正規化を避ける安全ガード。
            if (distance <= MinSegmentLength)
            {
                break;
            }

            Vector2 dir = delta / distance;
            Vector2 newPoint = lastCommittedPoint + dir * segmentLength;

            if (!IsFinite(newPoint))
            {
                break;
            }

            chainPoints.Add(newPoint);
            lastCommittedPoint = newPoint;
            addedCount++;

            // 追加のたびに最大節数を守り、最新側を残す。
            TrimToMaxSegmentCount();
        }

        if (chainPoints.Count > 0)
        {
            lastCommittedPoint = chainPoints[chainPoints.Count - 1];
        }

        return addedCount;
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

    // 最大節数を超えた古い点を先頭から捨てる。
    // 「最新の節構成を保つ」ことを優先し、末尾側は残す。
    private void TrimToMaxSegmentCount()
    {
        int safeMaxSegmentCount = Mathf.Max(0, maxSegmentCount);
        maxSegmentCount = safeMaxSegmentCount;

        while (SegmentCount > safeMaxSegmentCount && chainPoints.Count > 0)
        {
            chainPoints.RemoveAt(0);
        }

        if (chainPoints.Count > 0)
        {
            lastCommittedPoint = chainPoints[chainPoints.Count - 1];
        }
    }
}