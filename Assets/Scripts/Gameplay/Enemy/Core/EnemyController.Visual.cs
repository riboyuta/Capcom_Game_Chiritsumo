using UnityEngine;

// EnemyUnitController の見た目同期を担当する partial。
// 通常時の Root 位置更新や Animator パラメータ更新を行う。
public sealed partial class EnemyUnitController
{
    // 見た目に関わる更新処理をまとめて行う。
    private void TickVisual(float deltaTime)
    {
        UpdateRootPosition();
        UpdateArmSegments();
        UpdateAnimator();
    }

    // 通常時の基準位置として Root の X 座標を全体圧へ同期する。
    // 横スクロール想定のため、X 方向のみ更新する。
    private void UpdateRootPosition()
    {
        if (m_root == null)
        {
            return;
        }

        Vector3 pos = m_root.position;
        pos.x = m_pressureX;
        m_root.position = pos;
    }

    // Root と Palm の間に腕セグメントを並べて描画する。
    // 距離に応じて必要本数を増減させ、関節が増える見た目を作る。
    private void UpdateArmSegments()
    {
        if (m_root == null || m_palm == null)
        {
            SetAllArmSegmentsActive(false);
            return;
        }

        if (m_armSegmentsRoot == null || m_armSegmentPrefab == null)
        {
            SetAllArmSegmentsActive(false);
            return;
        }

        float segmentLength = Mathf.Max(0.01f, m_armSegmentLength);
        int maxCount = Mathf.Max(0, m_maxArmSegmentCount);

        Vector3 rootPos = m_root.position;
        Vector3 palmPos = m_palm.position;

        Vector3 delta = palmPos - rootPos;
        float fullDistance = delta.magnitude;

        if (fullDistance <= 0.0001f || maxCount == 0)
        {
            SetAllArmSegmentsActive(false);
            return;
        }

        Vector3 direction = delta / fullDistance;

        float usableDistance = fullDistance - m_rootSegmentOffset - m_palmSegmentOffset;
        usableDistance = Mathf.Max(0.0f, usableDistance);

        if (usableDistance <= 0.0001f)
        {
            SetAllArmSegmentsActive(false);
            return;
        }

        int requiredCount = Mathf.CeilToInt(usableDistance / segmentLength);
        requiredCount = Mathf.Clamp(requiredCount, 0, maxCount);

        EnsureArmSegmentPool(requiredCount);

        Vector3 startPos = rootPos + direction * m_rootSegmentOffset;

        for (int i = 0; i < m_armSegmentInstances.Count; i++)
        {
            bool shouldBeActive = i < requiredCount;
            Transform segment = m_armSegmentInstances[i];

            if (segment.gameObject.activeSelf != shouldBeActive)
            {
                segment.gameObject.SetActive(shouldBeActive);
            }

            if (!shouldBeActive)
            {
                continue;
            }

            // 各セグメントの中心位置を Root → Palm の間へ等間隔で並べる。
            float centerDistance = Mathf.Min(segmentLength * (i + 0.5f), usableDistance);
            Vector3 worldPos = startPos + direction * centerDistance;

            segment.position = worldPos;
            segment.rotation = CalculateSegmentRotation(direction);
        }
    }

    // 必要本数ぶんの腕セグメントをプールする。
    // 毎フレーム Instantiate / Destroy しないようにする。
    private void EnsureArmSegmentPool(int requiredCount)
    {
        while (m_armSegmentInstances.Count < requiredCount)
        {
            GameObject segmentObject = Instantiate(m_armSegmentPrefab, m_armSegmentsRoot);
            segmentObject.name = $"ArmSegment_{m_armSegmentInstances.Count:00}";
            m_armSegmentInstances.Add(segmentObject.transform);
        }
    }

    // すべての腕セグメントを一括で表示 / 非表示にする。
    private void SetAllArmSegmentsActive(bool active)
    {
        for (int i = 0; i < m_armSegmentInstances.Count; i++)
        {
            if (m_armSegmentInstances[i] != null &&
                m_armSegmentInstances[i].gameObject.activeSelf != active)
            {
                m_armSegmentInstances[i].gameObject.SetActive(active);
            }
        }
    }

    // 腕セグメントの向きを決める。
    // 横スクロール寄りの 3D 空間を想定し、X/Y 平面上の方向へ向かせる。
    // セグメント素材が「右向き」を正面として作られている前提。
    private Quaternion CalculateSegmentRotation(Vector3 direction)
    {
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        return Quaternion.Euler(0.0f, 0.0f, angle);
    }

    // 現在状態を Animator へ渡して見た目を更新する。
    private void UpdateAnimator()
    {
        if (m_animator == null)
        {
            return;
        }

        m_animator.SetInteger("State", (int)m_state);
    }
}