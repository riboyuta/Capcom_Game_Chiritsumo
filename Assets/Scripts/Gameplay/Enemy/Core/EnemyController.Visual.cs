using UnityEngine;

// EnemyUnitController の見た目同期を担当する partial。
// 通常時の Root 位置更新や Animator パラメータ更新を行う。
public sealed partial class EnemyUnitController
{
    // 見た目に関わる更新処理をまとめて行う。
    // Update メソッドから毎フレーム呼び出され、位置・腕・アニメーションを順番に更新する。
    private void TickVisual(float deltaTime)
    {
        // Root の位置を全体圧に同期
        UpdateRootPosition();
        // 腕セグメントを Root と Palm の間に配置
        UpdateArmSegments();
        // アニメーターのパラメータを更新
        UpdateAnimator();
    }

    // 通常時の基準位置として Root の X 座標を全体圧へ同期する。
    // 横スクロール想定のため、X 方向のみ更新する。
    // Y/Z 座標は初期配置を維持する。
    private void UpdateRootPosition()
    {
        // Root が未設定なら何もしない
        if (root == null)
        {
            return;
        }

        // 現在の位置を取得し、X 座標だけを圧位置に更新
        Vector3 pos = root.position;
        pos.x = pressureX;
        root.position = pos;
    }

    // Root と Palm の間に腕セグメントを並べて描画する。
    // 距離に応じて必要本数を増減させ、関節が増える見た目を作る。
    // Palm が攻撃で遠くへ移動しても、腕が伸びるように見せる。
    private void UpdateArmSegments()
    {
        // Root または Palm が未設定ならすべてのセグメントを非表示
        if (root == null || palm == null)
        {
            SetAllArmSegmentsActive(false);
            return;
        }

        // セグメントの親またはプレハブが未設定ならすべてを非表示
        if (armSegmentsRoot == null || armSegmentPrefab == null)
        {
            SetAllArmSegmentsActive(false);
            return;
        }

        // セグメント1本あたりの長さを取得（最小値0.01で安全性を確保）
        float segmentLength = Mathf.Max(0.01f, armSegmentLength);
        // 表示する最大セグメント数を取得
        int maxCount = Mathf.Max(0, maxArmSegmentCount);

        // Root と Palm の現在位置を取得
        Vector3 rootPos = root.position;
        Vector3 palmPos = palm.position;

        // Root から Palm へのベクトルと距離を計算
        Vector3 delta = palmPos - rootPos;
        float fullDistance = delta.magnitude;

        // 距離がほぼゼロまたは最大数が0ならすべてを非表示
        if (fullDistance <= 0.0001f || maxCount == 0)
        {
            SetAllArmSegmentsActive(false);
            return;
        }

        // Root から Palm への方向ベクトルを正規化
        Vector3 direction = delta / fullDistance;

        // オフセットを除いた実際にセグメントを配置できる距離を計算
        // rootSegmentOffset: Root 側の除外範囲
        // palmSegmentOffset: Palm 側の除外範囲
        float usableDistance = fullDistance - rootSegmentOffset - palmSegmentOffset;
        usableDistance = Mathf.Max(0.0f, usableDistance);

        // 使用可能距離がほぼゼロならすべてを非表示
        if (usableDistance <= 0.0001f)
        {
            SetAllArmSegmentsActive(false);
            return;
        }

        // 必要なセグメント数を計算（距離をセグメント長で割り、切り上げ）
        int requiredCount = Mathf.CeilToInt(usableDistance / segmentLength);
        // 最大数で制限（0 ～ maxCount の範囲にクランプ）
        requiredCount = Mathf.Clamp(requiredCount, 0, maxCount);

        // 必要本数分のセグメントオブジェクトをプールから確保
        EnsureArmSegmentPool(requiredCount);

        // セグメント配置の開始位置（Root からオフセット分進んだ位置）
        Vector3 startPos = rootPos + direction * rootSegmentOffset;

        // プール内のすべてのセグメントを更新
        for (int i = 0; i < armSegmentInstances.Count; i++)
        {
            // このセグメントが表示されるべきか判定
            bool shouldBeActive = i < requiredCount;
            Transform segment = armSegmentInstances[i];

            // 表示状態が変わる場合のみ更新（パフォーマンス最適化）
            if (segment.gameObject.activeSelf != shouldBeActive)
            {
                segment.gameObject.SetActive(shouldBeActive);
            }

            // 非表示のセグメントは位置更新をスキップ
            if (!shouldBeActive)
            {
                continue;
            }

            // 各セグメントの中心位置を Root → Palm の間へ等間隔で並べる。
            // i 番目のセグメントは、開始位置から (i + 0.5) * segmentLength の距離に配置
            float centerDistance = Mathf.Min(segmentLength * (i + 0.5f), usableDistance);
            Vector3 worldPos = startPos + direction * centerDistance;

            // セグメントの位置と回転を設定
            segment.position = worldPos;
            segment.rotation = CalculateSegmentRotation(direction);
        }
    }

    // 必要本数ぶんの腕セグメントをプールする。
    // 毎フレーム Instantiate / Destroy しないようにする。
    // 不足分だけ生成し、余分は非表示にすることで再利用する。
    private void EnsureArmSegmentPool(int requiredCount)
    {
        // プールのセグメント数が必要数に達するまで生成を繰り返す
        while (armSegmentInstances.Count < requiredCount)
        {
            // プレハブからインスタンスを生成し、指定された親の下に配置
            GameObject segmentObject = Instantiate(armSegmentPrefab, armSegmentsRoot);
            // デバッグしやすいように連番名を付ける
            segmentObject.name = $"ArmSegment_{armSegmentInstances.Count:00}";
            // プールリストに追加
            armSegmentInstances.Add(segmentObject.transform);
        }
    }

    // すべての腕セグメントを一括で表示 / 非表示にする。
    // Root または Palm が未設定の場合や、距離がゼロの場合に呼び出される。
    private void SetAllArmSegmentsActive(bool active)
    {
        // プール内のすべてのセグメントをループ処理
        for (int i = 0; i < armSegmentInstances.Count; i++)
        {
            // セグメントが存在し、現在の表示状態が目標と異なる場合のみ更新
            if (armSegmentInstances[i] != null &&
                armSegmentInstances[i].gameObject.activeSelf != active)
            {
                armSegmentInstances[i].gameObject.SetActive(active);
            }
        }
    }

    // 腕セグメントの向きを決める。
    // 横スクロール寄りの 3D 空間を想定し、X/Y 平面上の方向へ向かせる。
    // セグメント素材が「右向き」を正面として作られている前提。
    // direction: Root から Palm への正規化された方向ベクトル
    private Quaternion CalculateSegmentRotation(Vector3 direction)
    {
        // X/Y 平面上の角度を計算（ラジアンから度数法へ変換）
        // Atan2 を使うことで、すべての方向を正しく計算できる
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        // Z 軸回転でセグメントを回転させる（2D スプライトを 3D 空間で回転）
        return Quaternion.Euler(0.0f, 0.0f, angle);
    }

    // 現在状態を Animator へ渡して見た目を更新する。
    // Animator Controller で「State」パラメータを使用してアニメーションを切り替える。
    // State 値: 0=Idle, 1=Windup, 2=Attack, 3=Recovery
    private void UpdateAnimator()
    {
        // Animator が未設定なら何もしない
        if (animator == null)
        {
            return;
        }

        // 現在の状態を整数値に変換して Animator に渡す
        animator.SetInteger("State", (int)state);
    }
}