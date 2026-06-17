using UnityEngine;

[DisallowMultipleComponent]
public sealed class SonarChargerMovement : MonoBehaviour
{
    // Rigidbodyキャッシュ
    private Rigidbody rb;

    // 初期状態保持（リスポーン用）
    private Vector3 initialPosition;
    private Quaternion initialRotation;
    private bool hasCapturedInitialState;

    // Follow状態: 直前の移動方向
    private Vector3 lastMoveDirection = Vector3.right;

    // Charge状態: 突進方向とカメラ境界情報
    private Vector3 chargeDirection = Vector3.right;
    private Bounds chargeViewBounds;
    private PlayerCameraController activeCameraController;
    private bool hasEnteredChargeViewBounds;
    private bool hasChargeViewBounds;

    // Charge状態: 突進開始位置と経過時間
    private Vector3 chargeStartPosition;
    private float chargeElapsedTime;

    // Rebound状態: 跳ね返り開始/終了位置とタイマー
    private Vector3 reboundStartPosition;
    private Vector3 reboundEndPosition;
    private float reboundTimer;

    public Vector3 ChargeDirection => chargeDirection;

    public Vector3 BuildChargeDirectionTo(
    Vector3 targetPosition,
    SonarChargerSettings settings)
    {
        Vector3 current = GetWorldPosition();
        Vector3 toTarget = Flatten(targetPosition - current);

        float minDistance = Mathf.Max(0.001f, settings.minChargeTargetDistance);

        if (toTarget.sqrMagnitude < minDistance * minDistance)
        {
            toTarget = lastMoveDirection.sqrMagnitude > 0.0001f
                ? lastMoveDirection
                : Vector3.right;
        }

        return toTarget.normalized;
    }

    public Vector3 BuildPredictionEndPosition(
    Vector3 start,
    Vector3 direction,
    PlayerCameraController cameraController,
    SonarChargerSettings settings)
    {
        direction = Flatten(direction);

        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = chargeDirection.sqrMagnitude > 0.0001f
                ? chargeDirection
                : Vector3.right;
        }

        direction.Normalize();

        float length = settings.alertPredictionLineLength > 0.0f
            ? settings.alertPredictionLineLength
            : settings.maxChargeDistance;

        if (length <= 0.0f)
        {
            length = 12.0f;
        }

        Vector3 end = start + direction * length;
        end.z = start.z;

        return end;
    }

    // 初期化: Rigidbodyをキャッシュ
    public void Initialize(Rigidbody rigidbody)
    {
        rb = rigidbody;
    }

    public void CaptureInitialState()
    {
        if (hasCapturedInitialState)
        {
            return;
        }

        initialPosition = transform.position;
        initialRotation = transform.rotation;
        hasCapturedInitialState = true;
    }

    public void ResetToInitialState()
    {
        if (!hasCapturedInitialState)
        {
            return;
        }

        SetWorldPosition(initialPosition);
        transform.rotation = initialRotation;
        ResetAllChargeStates();
    }

    // Follow状態: 指定された速度で目標位置へ移動
    public void TickFollow(
        Vector3 targetPosition,
        float moveSpeed,
        float deltaTime)
    {
        Vector3 current = GetWorldPosition();

        // 目標位置への方向をXY平面上で計算
        Vector3 toTarget = Flatten(targetPosition - current);

        // 目標位置とほぼ同じ位置なら停止
        if (toTarget.sqrMagnitude <= 0.0001f)
        {
            StopImmediate();
            return;
        }

        Vector3 direction = toTarget.normalized;
        lastMoveDirection = direction;

        float speed = Mathf.Max(0.0f, moveSpeed);
        float safeDeltaTime = Mathf.Max(0.0f, deltaTime);

        Vector3 next =
            current +
            direction *
            speed *
            safeDeltaTime;

        next.z = current.z;
        SetWorldPosition(next);
    }

    //// 既存呼び出しとの一時的な互換用
    //// SonarChargerEnemy側を距離連動速度へ変更した後に削除する
    //public void TickFollow(
    //Vector3 targetPosition,
    //float moveSpeed,
    //float deltaTime)

    // Charge状態開始: 突進方向を計算し、カメラ境界情報を初期化
    public void StartCharge(Vector3 targetPosition, PlayerCameraController cameraController, SonarChargerSettings settings)
    {
        Vector3 current = GetWorldPosition();
        Vector3 toTarget = Flatten(targetPosition - current);
        float minDistance = Mathf.Max(0.001f, settings.minChargeTargetDistance);

        // 目標までの距離が短すぎる場合、最後の移動方向または右を使用
        if (toTarget.sqrMagnitude < minDistance * minDistance)
        {
            toTarget = lastMoveDirection.sqrMagnitude > 0.0001f ? lastMoveDirection : Vector3.right;
        }

        // 突進方向を正規化して記録
        chargeDirection = toTarget.normalized;
        // カメラコントローラーを記録
        activeCameraController = cameraController;
        // 現在のカメラ境界情報を取得
        UpdateChargeViewBounds();

        // 開始時にカメラ境界内にいるかを記録
        hasEnteredChargeViewBounds = hasChargeViewBounds && IsInsideChargeViewBounds(current, settings.cameraBoundaryPadding);
        // 突進開始位置と経過時間を初期化
        chargeStartPosition = current;
        chargeElapsedTime = 0.0f;
    }

    // Charge状態更新: 直線突進し、カメラ境界に到達したらtrueを返す
    public bool TickCharge(SonarChargerSettings settings, float deltaTime)
    {
        Vector3 current = GetWorldPosition();
        // 次のフレームの位置を計算（突進速度 * デルタタイム）
        Vector3 next = current + chargeDirection * settings.chargeSpeed * deltaTime;
        next.z = current.z; // Z座標は固定
        chargeElapsedTime += deltaTime;

        // 毎フレーム、カメラ境界情報を更新（カメラが動く可能性あり）
        UpdateChargeViewBounds();

        // カメラ境界が有効な場合、停止判定を行う
        if (hasChargeViewBounds)
        {
            if (ShouldStopCharge(current, next, settings.cameraBoundaryPadding))
            {
                // 境界内にクランプして停止
                next = ClampInsideChargeViewBounds(next, settings.cameraBoundaryPadding);
                SetWorldPosition(next);
                StopImmediate();
                return true; // 境界到達
            }
        }

        // 万が一カメラ境界に入れない場合、最大距離/時間で制限
        if (HasExceededChargeLimit(next, settings))
        {
            SetWorldPosition(next);
            StopImmediate();
            return true; // 制限超過
        }

        // 突進継続
        SetWorldPosition(next);
        return false;
    }

    // Charge制限判定: 最大距離/時間を超えたかをチェック
    private bool HasExceededChargeLimit(Vector3 nextPosition, SonarChargerSettings settings)
    {
        if (settings == null)
        {
            return false;
        }

        // 最大距離制限のチェック
        if (settings.maxChargeDistance > 0.0f)
        {
            float distance = Vector2.Distance(
                new Vector2(chargeStartPosition.x, chargeStartPosition.y),
                new Vector2(nextPosition.x, nextPosition.y));

            if (distance >= settings.maxChargeDistance)
            {
                return true; // 距離制限超過
            }
        }

        // 最大時間制限のチェック
        if (settings.maxChargeTime > 0.0f && chargeElapsedTime >= settings.maxChargeTime)
        {
            return true; // 時間制限超過
        }

        return false;
    }

    // Rebound状態開始: 跳ね返り開始/終了位置を計算
    public void StartRebound(SonarChargerSettings settings)
    {
        reboundTimer = 0.0f;
        reboundStartPosition = GetWorldPosition();

        // 跳ね返りは突進方向の逆（方向が無い場合はゼロ）
        Vector3 direction = chargeDirection.sqrMagnitude > 0.0001f ? -chargeDirection : Vector3.zero;
        reboundEndPosition = reboundStartPosition + direction * settings.reboundDistance;
        reboundEndPosition.z = reboundStartPosition.z; // Z座標は固定
    }

    // Rebound状態更新: イージングで跳ね返り、完了したらtrueを返す
    public bool TickRebound(SonarChargerSettings settings, float deltaTime)
    {
        float duration = Mathf.Max(0.001f, settings.reboundDuration);
        reboundTimer += deltaTime;
        // 正規化された時間（0.0〜1.0）
        float t = Mathf.Clamp01(reboundTimer / duration);
        // イージング関数適用：最初は速く、最後は遅く
        float easedT = 1.0f - Mathf.Pow(1.0f - t, 2.0f);

        // 開始位置と終了位置間で補間
        Vector3 next = Vector3.Lerp(reboundStartPosition, reboundEndPosition, easedT);
        SetWorldPosition(next);

        return t >= 1.0f; // 完了判定
    }

    // 移動停止: Rigidbodyの速度をゼロにする
    public void StopImmediate()
    {
        if (rb == null)
        {
            return;
        }

        bool wasKinematic = rb.isKinematic;
        // Kinematicの場合、一時的に解除して速度をゼロにする
        if (wasKinematic)
        {
            rb.isKinematic = false;
        }

        // 線形速度と角速度をゼロクリア
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        // Kinematic状態を復元
        rb.isKinematic = wasKinematic;
    }

    // 線分とカメラ境界の交差判定（Liang-Barskyアルゴリズム）
    private bool SegmentIntersectsChargeViewBounds(Vector3 start, Vector3 end, float padding)
    {
        if (!TryGetPaddedChargeViewBounds(padding, out float minX, out float maxX, out float minY, out float maxY))
        {
            return false;
        }

        Vector2 p0 = new Vector2(start.x, start.y);
        Vector2 p1 = new Vector2(end.x, end.y);
        Vector2 delta = p1 - p0;

        float tMin = 0.0f;
        float tMax = 1.0f;

        if (!ClipSegmentAxis(p0.x, delta.x, minX, maxX, ref tMin, ref tMax))
        {
            return false;
        }

        if (!ClipSegmentAxis(p0.y, delta.y, minY, maxY, ref tMin, ref tMax))
        {
            return false;
        }

        return true;
    }

    // 軸別クリップ判定（Liang-Barskyアルゴリズムの軸別処理）
    private static bool ClipSegmentAxis(float start, float delta, float min, float max, ref float tMin, ref float tMax)
    {
        const float Epsilon = 0.00001f;

        if (Mathf.Abs(delta) < Epsilon)
        {
            return start >= min && start <= max;
        }

        float invDelta = 1.0f / delta;
        float t1 = (min - start) * invDelta;
        float t2 = (max - start) * invDelta;

        if (t1 > t2)
        {
            (t1, t2) = (t2, t1);
        }

        tMin = Mathf.Max(tMin, t1);
        tMax = Mathf.Min(tMax, t2);
        return tMin <= tMax;
    }

    // カメラ境界情報をパディング付きで取得
    private bool TryGetPaddedChargeViewBounds(float padding, out float minX, out float maxX, out float minY, out float maxY)
    {
        minX = maxX = minY = maxY = 0.0f;

        if (!hasChargeViewBounds)
        {
            return false;
        }

        padding = Mathf.Max(0.0f, padding);
        minX = chargeViewBounds.min.x + padding;
        maxX = chargeViewBounds.max.x - padding;
        minY = chargeViewBounds.min.y + padding;
        maxY = chargeViewBounds.max.y - padding;

        ClampToCenter(ref minX, ref maxX, chargeViewBounds.center.x);
        ClampToCenter(ref minY, ref maxY, chargeViewBounds.center.y);

        return true;
    }

    // 指定位置がカメラ境界内かを判定
    private bool IsInsideChargeViewBounds(Vector3 position, float padding)
    {
        if (!TryGetPaddedChargeViewBounds(padding, out float minX, out float maxX, out float minY, out float maxY))
        {
            return false;
        }

        return position.x >= minX && position.x <= maxX && position.y >= minY && position.y <= maxY;
    }

    // 位置をカメラ境界内にクランプ
    private Vector3 ClampInsideChargeViewBounds(Vector3 position, float padding)
    {
        if (!TryGetPaddedChargeViewBounds(padding, out float minX, out float maxX, out float minY, out float maxY))
        {
            return position;
        }

        position.x = Mathf.Clamp(position.x, minX, maxX);
        position.y = Mathf.Clamp(position.y, minY, maxY);
        return position;
    }

    // カメラ境界情報を現在のカメラから取得
    private void UpdateChargeViewBounds()
    {
        if (activeCameraController == null)
        {
            hasChargeViewBounds = false;
            return;
        }

        Bounds bounds = activeCameraController.GetCurrentViewBounds();

        if (bounds.size.sqrMagnitude <= 0.0001f)
        {
            hasChargeViewBounds = false;
            return;
        }

        chargeViewBounds = bounds;
        hasChargeViewBounds = true;
    }

    // RigidbodyまたはTransformから位置を取得
    private Vector3 GetWorldPosition()
    {
        return rb != null ? rb.position : transform.position;
    }

    // RigidbodyまたはTransformに位置を設定
    private void SetWorldPosition(Vector3 position)
    {
        if (rb != null)
        {
            rb.position = position;
        }
        else
        {
            transform.position = position;
        }
    }

    // ヘルパー: 全状態をリセット
    private void ResetAllChargeStates()
    {
        lastMoveDirection = Vector3.right;
        chargeDirection = Vector3.right;
        hasChargeViewBounds = false;
        activeCameraController = null;
        hasEnteredChargeViewBounds = false;
        chargeStartPosition = initialPosition;
        chargeElapsedTime = 0.0f;
        reboundTimer = 0.0f;
    }

    // ヘルパー: 突進停止判定（カメラ内に入った後に外に出たか）
    private bool ShouldStopCharge(Vector3 current, Vector3 next, float padding)
    {
        bool currentInside = IsInsideChargeViewBounds(current, padding);
        bool nextInside = IsInsideChargeViewBounds(next, padding);
        bool segmentTouchesBounds = SegmentIntersectsChargeViewBounds(current, next, padding);

        // まだカメラ境界内に入っていない場合
        if (!hasEnteredChargeViewBounds)
        {
            // 現在または次の位置が境界内、または線分が境界を横切る場合
            if (currentInside || nextInside || segmentTouchesBounds)
            {
                hasEnteredChargeViewBounds = true; // 境界内に入ったとマーク
            }
        }

        // 一度境界内に入った後、次の位置が外なら停止
        return hasEnteredChargeViewBounds && !nextInside;
    }

    // ユーティリティ: Vector3のZ座標を0にする
    private static Vector3 Flatten(Vector3 vector)
    {
        vector.z = 0.0f;
        return vector;
    }

    // ユーティリティ: min > maxの場合はcenterに調整
    private static void ClampToCenter(ref float min, ref float max, float center)
    {
        if (min > max)
        {
            min = max = center;
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (hasChargeViewBounds)
        {
            Gizmos.color = new Color(1.0f, 0.4f, 0.0f, 0.2f);
            Gizmos.DrawCube(chargeViewBounds.center, chargeViewBounds.size);

            Gizmos.color = new Color(1.0f, 0.4f, 0.0f, 0.9f);
            Gizmos.DrawWireCube(chargeViewBounds.center, chargeViewBounds.size);
        }

        Gizmos.color = Color.red;
        Gizmos.DrawRay(transform.position, chargeDirection * 1.5f);
    }
#endif
}