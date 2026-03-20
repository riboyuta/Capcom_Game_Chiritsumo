using System.Collections.Generic;
using UnityEngine;

// EnemyUnitController の見た目同期を担当する partial。
// 通常時の Root / Palm / Arm / Animator の見た目更新を行う。
public sealed partial class EnemyUnitController
{
    [Header("Visual: Palm")]
    [SerializeField] private Transform palmVisual;
    [SerializeField] private bool usePalmAsVisualTarget = true;

    [Header("Visual: Arm")]
    [SerializeField] private float armSegmentAngleOffset = 0.0f;
    [SerializeField] private bool useEvenArmSpacing = true;
    [SerializeField] private bool scaleArmSegmentToSpacing = false;
    [SerializeField] private float armSegmentBaseVisualLength = 1.0f;

    // 見た目に関わる更新処理をまとめて行う。
    // Update メソッドから毎フレーム呼び出され、位置・手・腕・アニメーションを順番に更新する。
    private void TickVisual(float deltaTime)
    {
        // Root の位置を全体圧に同期
        UpdateRootPosition();

        // Palm の見た目位置を更新
        UpdatePalmVisual(deltaTime);

        // 腕セグメントを Root と Palm の間に配置
        UpdateArmSegments();

        // アニメーターのパラメータを更新
        UpdateAnimator();
    }

    // 通常時の基準位置として Root の X 座標を全体圧へ同期する。
    // 横スクロール想定のため、X 方向のみ更新する。
    // Y/Z 座標は現在値を維持する。
    private void UpdateRootPosition()
    {
        if (root == null)
        {
            return;
        }

        Vector3 pos = root.position;
        pos.x = pressureX;
        root.position = pos;
    }

    // Palm の見た目を更新する。
    // usePalmAsVisualTarget=true の場合は palmVisual を palm に位置だけ追従させる。
    // 回転は将来のアニメーション制御と競合しないように触らない。
    private void UpdatePalmVisual(float deltaTime)
    {
        Transform visual = GetPalmVisualTransform();
        if (visual == null)
        {
            return;
        }

        // palmVisual を palm に完全追従させる構成なら位置だけ同期
        if (usePalmAsVisualTarget && palm != null && visual != palm)
        {
            visual.position = palm.position;
        }
    }

    // Root と Palm の間に腕セグメントを並べて描画する。
    // 距離に応じて必要本数を増減させ、関節が増える見た目を作る。
    // Palm が攻撃で遠くへ移動しても、腕が伸びるように見せる。
    private void UpdateArmSegments()
    {
        if (root == null || palm == null)
        {
            SetAllArmSegmentsActive(false);
            return;
        }

        if (armSegmentsRoot == null || armSegmentPrefab == null)
        {
            SetAllArmSegmentsActive(false);
            return;
        }

        float logicalSegmentLength = Mathf.Max(0.01f, armSegmentLength);
        int maxCount = Mathf.Max(0, maxArmSegmentCount);

        Vector3 rootPos = root.position;
        Vector3 palmPos = palm.position;

        Vector3 delta = palmPos - rootPos;
        float fullDistance = delta.magnitude;

        if (fullDistance <= 0.0001f || maxCount == 0)
        {
            SetAllArmSegmentsActive(false);
            return;
        }

        Vector3 direction = delta / fullDistance;

        float usableDistance = fullDistance - rootSegmentOffset - palmSegmentOffset;
        usableDistance = Mathf.Max(0.0f, usableDistance);

        if (usableDistance <= 0.0001f)
        {
            SetAllArmSegmentsActive(false);
            return;
        }

        int requiredCount = Mathf.CeilToInt(usableDistance / logicalSegmentLength);
        requiredCount = Mathf.Clamp(requiredCount, 0, maxCount);

        if (requiredCount <= 0)
        {
            SetAllArmSegmentsActive(false);
            return;
        }

        EnsureArmSegmentPool(requiredCount);

        Vector3 startPos = rootPos + direction * rootSegmentOffset;

        float spacing = useEvenArmSpacing
            ? (usableDistance / requiredCount)
            : logicalSegmentLength;

        Quaternion rotation = CalculateSegmentRotation(direction);

        for (int i = 0; i < armSegmentInstances.Count; i++)
        {
            Transform segment = armSegmentInstances[i];
            if (segment == null)
            {
                continue;
            }

            bool shouldBeActive = i < requiredCount;

            if (segment.gameObject.activeSelf != shouldBeActive)
            {
                segment.gameObject.SetActive(shouldBeActive);
            }

            if (!shouldBeActive)
            {
                continue;
            }

            float centerDistance;

            if (useEvenArmSpacing)
            {
                centerDistance = spacing * (i + 0.5f);
            }
            else
            {
                centerDistance = Mathf.Min(logicalSegmentLength * (i + 0.5f), usableDistance);
            }

            Vector3 worldPos = startPos + direction * centerDistance;

            segment.position = worldPos;
            segment.rotation = rotation;

            if (scaleArmSegmentToSpacing)
            {
                ApplyArmSegmentScale(segment, spacing);
            }
        }
    }

    // 必要本数ぶんの腕セグメントをプールする。
    private void EnsureArmSegmentPool(int requiredCount)
    {
        while (armSegmentInstances.Count < requiredCount)
        {
            GameObject segmentObject = Instantiate(armSegmentPrefab, armSegmentsRoot);
            segmentObject.name = $"ArmSegment_{armSegmentInstances.Count:00}";
            armSegmentInstances.Add(segmentObject.transform);
        }
    }

    // すべての腕セグメントを一括で表示 / 非表示にする。
    private void SetAllArmSegmentsActive(bool active)
    {
        for (int i = 0; i < armSegmentInstances.Count; i++)
        {
            Transform segment = armSegmentInstances[i];
            if (segment == null)
            {
                continue;
            }

            if (segment.gameObject.activeSelf != active)
            {
                segment.gameObject.SetActive(active);
            }
        }
    }

    // 腕セグメントの向きを決める。
    // セグメント素材が「右向き」を正面として作られている前提。
    private Quaternion CalculateSegmentRotation(Vector3 direction)
    {
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        return Quaternion.Euler(0.0f, 0.0f, angle + armSegmentAngleOffset);
    }

    // 腕セグメントの見た目長をローカルスケールで補正する。
    private void ApplyArmSegmentScale(Transform segment, float spacing)
    {
        if (segment == null)
        {
            return;
        }

        float baseLength = Mathf.Max(0.0001f, armSegmentBaseVisualLength);
        float scaleRatio = spacing / baseLength;

        Vector3 localScale = segment.localScale;
        localScale.x = scaleRatio;
        segment.localScale = localScale;
    }

    // 現在使う Palm の見た目 Transform を取得する。
    // palmVisual が未設定なら palm をそのまま使う。
    private Transform GetPalmVisualTransform()
    {
        if (palmVisual != null)
        {
            return palmVisual;
        }

        return palm;
    }

    // 現在状態を Animator へ渡して見た目を更新する。
    // State 値: 0=Idle, 1=Windup, 2=Attack, 3=Recovery
    private void UpdateAnimator()
    {
        if (animator == null)
        {
            return;
        }

        animator.SetInteger("State", (int)state);
    }
}