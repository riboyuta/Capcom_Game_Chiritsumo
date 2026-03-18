using UnityEngine;

// 同一 GameObject に複数付与されるのを防ぐ。
[DisallowMultipleComponent]
// このコンポーネントは Camera を必須とする。
[RequireComponent(typeof(Camera))]
public sealed class PlayerCameraController : MonoBehaviour
{
    [Header("追従に使用するカメラ")]
    // 追従処理に使う Camera 参照。
    [SerializeField] private Camera targetCamera;

    [Header("追従対象アンカー")]
    // カメラが追従する基準位置。通常はプレイヤーの子 Anchor を想定する。
    [SerializeField] private Transform targetAnchor;

    [Header("カメラ移動範囲")]
    // カメラが移動可能なワールド範囲。
    [SerializeField] private CameraBounds cameraBounds;

    [Header("アンカー自動探索を使うか")]
    // targetAnchor 未設定時にプレイヤーから自動探索するか。
    [SerializeField] private bool autoFindPlayerAnchor = true;

    [Header("プレイヤー探索用タグ")]
    // プレイヤー探索に使うタグ名。
    [SerializeField] private string playerTag = "Player";

    [Header("アンカー子オブジェクト名")]
    // プレイヤー配下から探すアンカー名。
    [SerializeField] private string anchorChildName = "CameraTargetAnchor";

    [Header("カメラオフセット")]
    // ターゲット位置に対するカメラの相対オフセット。
    [SerializeField] private Vector3 cameraOffset = new Vector3(0f, 0f, -10f);

    [Header("X軸追従スムーズ時間")]
    // X 軸方向の追従スムージング時間。
    [SerializeField] private float smoothTimeX = 0.08f;

    [Header("Y軸追従スムーズ時間")]
    // Y 軸方向の追従スムージング時間。
    [SerializeField] private float smoothTimeY = 0.12f;

    [Header("デバッグGizmoを描画するか")]
    // Scene 上に追従関連の Gizmo を描画するか。
    [SerializeField] private bool drawDebugGizmos = true;

    [Header("Viewport位置をログ出力するか")]
    // ターゲットが Viewport のどこに居るかをログ出力するか。
    [SerializeField] private bool logViewportPosition = false;

    // SmoothDamp が内部で使用する X 軸速度。
    private float velocityX;
    // SmoothDamp が内部で使用する Y 軸速度。
    private float velocityY;

    // ターゲット追従後、まだ境界制限前の理想カメラ位置。
    private Vector3 desiredPosition;
    // 境界制限を適用した最終目標位置。
    private Vector3 clampedPosition;

    // デバッグ・参照用：制限前の理想位置を公開する。
    public Vector3 DesiredPosition => desiredPosition;
    // デバッグ・参照用：制限後の位置を公開する。
    public Vector3 ClampedPosition => clampedPosition;

    private void Reset()
    {
        // 同一 GameObject 上の Camera を自動取得する。
        targetCamera = GetComponent<Camera>();
    }

    private void Awake()
    {
        // 実行時に Camera 参照が未設定なら自動取得する。
        if (targetCamera == null)
        {
            targetCamera = GetComponent<Camera>();
        }

        // 必要であれば追従対象アンカーを解決する。
        ResolveTargetAnchor();
    }

    private void LateUpdate()
    {
        // 実行に必要な参照が揃っていなければ更新しない。
        if (!ValidateRuntimeReferences())
        {
            return;
        }

        // アンカー位置にオフセットを足した理想カメラ位置を求める。
        desiredPosition = targetAnchor.position + cameraOffset;

        // CameraBounds 内に収まるよう理想位置を制限する。
        clampedPosition = GetClampedPosition(desiredPosition);

        // X 軸をスムーズに追従させる。
        float finalX = Mathf.SmoothDamp(
            current: transform.position.x,
            target: clampedPosition.x,
            currentVelocity: ref velocityX,
            smoothTime: smoothTimeX);

        // Y 軸をスムーズに追従させる。
        float finalY = Mathf.SmoothDamp(
            current: transform.position.y,
            target: clampedPosition.y,
            currentVelocity: ref velocityY,
            smoothTime: smoothTimeY);

        // Z はクランプ後の値をそのまま採用し、2D カメラ距離を維持する。
        transform.position = new Vector3(finalX, finalY, clampedPosition.z);

        // 必要に応じてターゲット位置の Viewport 座標を確認する。
        if (logViewportPosition)
        {
            Vector3 viewport = targetCamera.WorldToViewportPoint(targetAnchor.position);
            Debug.Log(
                $"CameraTarget Viewport : x={viewport.x:F2}, y={viewport.y:F2}",
                this);
        }
    }

    private bool ValidateRuntimeReferences()
    {
        // Camera が無ければ追従計算できない。
        if (targetCamera == null)
        {
            Debug.LogWarning("PlayerCameraController: Camera reference is missing.", this);
            return false;
        }

        // TargetAnchor が無ければ自動探索を試みる。
        if (targetAnchor == null)
        {
            ResolveTargetAnchor();

            // それでも見つからなければ更新不可。
            if (targetAnchor == null)
            {
                Debug.LogWarning("PlayerCameraController: Target anchor is missing.", this);
                return false;
            }
        }

        // CameraBounds が無ければ境界制限できない。
        if (cameraBounds == null)
        {
            Debug.LogWarning("PlayerCameraController: CameraBounds reference is missing.", this);
            return false;
        }

        // この実装は Orthographic Camera 前提。
        if (!targetCamera.orthographic)
        {
            Debug.LogWarning("PlayerCameraController: This version assumes an Orthographic camera.", this);
            return false;
        }

        return true;
    }

    private void ResolveTargetAnchor()
    {
        // 自動探索を使わない、または既に設定済みなら何もしない。
        if (!autoFindPlayerAnchor || targetAnchor != null)
        {
            return;
        }

        GameObject player = null;

        try
        {
            // 指定タグを持つプレイヤーを探す。
            player = GameObject.FindWithTag(playerTag);
        }
        catch (UnityException)
        {
            // タグ未登録などで例外が出る場合は探索を中断する。
            return;
        }

        // プレイヤーが見つからなければ終了。
        if (player == null)
        {
            return;
        }

        // プレイヤー配下から指定名のアンカーを探す。
        Transform foundAnchor = player.transform.Find(anchorChildName);

        // 見つかったらその Transform、無ければプレイヤー本体を追従対象にする。
        targetAnchor = foundAnchor != null ? foundAnchor : player.transform;
    }

    private Vector3 GetClampedPosition(Vector3 desired)
    {
        // カメラ移動可能範囲を取得する。
        Bounds bounds = cameraBounds.WorldBounds;

        // Orthographic Camera の半分の表示サイズを求める。
        float halfHeight = targetCamera.orthographicSize;
        float halfWidth = halfHeight * targetCamera.aspect;

        // カメラ表示領域が Bounds からはみ出さないように有効範囲を計算する。
        float minX = bounds.min.x + halfWidth;
        float maxX = bounds.max.x - halfWidth;
        float minY = bounds.min.y + halfHeight;
        float maxY = bounds.max.y - halfHeight;

        // 表示領域の方が Bounds より大きい場合は中心固定にする。
        float clampedX = (minX > maxX) ? bounds.center.x : Mathf.Clamp(desired.x, minX, maxX);
        float clampedY = (minY > maxY) ? bounds.center.y : Mathf.Clamp(desired.y, minY, maxY);

        // Z は入力値を維持したまま返す。
        return new Vector3(clampedX, clampedY, desired.z);
    }

    public void SetBounds(CameraBounds newBounds)
    {
        // 外部から CameraBounds を差し替えるための API。
        cameraBounds = newBounds;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        // デバッグ描画が無効なら何もしない。
        if (!drawDebugGizmos)
        {
            return;
        }

        // 理想位置を黄色で表示する。
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(desiredPosition, 0.15f);

        // 境界制限後の位置を緑で表示する。
        Gizmos.color = Color.green;
        Gizmos.DrawSphere(clampedPosition, 0.18f);

        // 理想位置から制限後位置までの差を線で表示する。
        Gizmos.color = Color.white;
        Gizmos.DrawLine(desiredPosition, clampedPosition);

        // 追従対象アンカー自体の位置も表示する。
        if (targetAnchor != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawSphere(targetAnchor.position, 0.12f);
        }
    }
#endif
}