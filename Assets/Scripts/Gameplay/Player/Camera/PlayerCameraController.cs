using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

// 同じ GameObject に複数付けて、カメラ制御が競合しないようにする。
[DisallowMultipleComponent]
// このコンポーネントは Camera を必須とする。（※CameraShakeManagerと分離するためRequireComponentを解除しました）
// [RequireComponent(typeof(Camera))]
public sealed class PlayerCameraController : MonoBehaviour
{
    // -----------------------------
    // Inspector 設定値
    // -----------------------------

    [Header("追従に使用するカメラ")]
    [Tooltip("追従処理と画面サイズ計算に使う Camera 参照です。通常はこの GameObject 自身の Camera を設定します。Orthographic 前提の実装です。")]
    // カメラ追従の計算に使う Camera。
    // null の場合は Awake / Reset で同一 GameObject から取得を試みる。
    [SerializeField] private Camera targetCamera;

    [Header("追従対象アンカー")]
    [Tooltip("カメラが追従する基準位置です。通常はプレイヤー直下の CameraTargetAnchor などを設定します。未設定時は自動探索設定に応じて補完を試みます。")]
    // 通常追従で使う対象位置。
    [SerializeField] private Transform targetAnchor;

    [Header("ワールド全体のfallback境界")]
    [Tooltip("常に使える基本のカメラ移動境界です。エリア別の一時境界が未設定のときはこの境界を使ってカメラ位置を制限します。")]
    [FormerlySerializedAs("cameraBounds")]
    // 通常時に使うカメラ移動可能範囲。
    // activeBounds が無いときのフォールバックとして使う。
    [SerializeField] private CameraBounds worldBounds;

    [Header("アンカー自動探索を使うか")]
    [Tooltip("有効にすると、targetAnchor 未設定時に playerTag と anchorChildName を使って追従対象を自動探索します。手動設定を優先したい場合は無効にします。")]
    // true のとき、targetAnchor が未設定なら自動でプレイヤーを探す。
    [SerializeField] private bool autoFindPlayerAnchor = true;

    [Header("プレイヤー探索用タグ")]
    [Tooltip("アンカー自動探索時にプレイヤーを見つけるためのタグ名です。GameObject.FindWithTag で検索されるため、実際のタグ設定と一致させてください。")]
    // 自動探索時にプレイヤーを見つけるためのタグ名。
    [SerializeField] private string playerTag = "Player";

    [Header("アンカー子オブジェクト名")]
    [Tooltip("プレイヤー配下から探す追従用アンカーの子オブジェクト名です。見つからない場合はプレイヤー本体 Transform を追従対象として使います。")]
    // プレイヤー配下から探す追従アンカーの名前。
    // 見つからなければプレイヤー本体 Transform を使う。
    [SerializeField] private string anchorChildName = "CameraTargetAnchor";

    [Header("カメラオフセット")]
    [Tooltip("追従対象アンカーに加算するカメラ位置オフセットです。2D では通常 Z を負値にしてカメラを手前へ置きます。")]
    // 追従対象に対して、どれだけずらした位置にカメラを置くか。
    // 2D では一般的に Z = -10 付近を使う。
    [SerializeField] private Vector3 cameraOffset = new Vector3(0f, 0f, -10f);

    [Header("X軸追従スムーズ時間")]
    [Tooltip("X 軸方向の追従をどれくらい滑らかにするかの時間です。小さいほど素早く追従し、大きいほど遅れて追従します。")]
    // X 方向の追従の滑らかさ。
    // 小さいと即追従、大きいと遅れて追従する。
    [SerializeField] private float smoothTimeX = 0.08f;

    [Header("Y軸追従スムーズ時間")]
    [Tooltip("Y 軸方向の追従をどれくらい滑らかにするかの時間です。ジャンプや落下の見え方に影響します。小さいほど即追従、大きいほどゆったり追従します。")]
    // Y 方向の追従の滑らかさ。
    // ジャンプや落下時のカメラ感触に強く影響する。
    [SerializeField] private float smoothTimeY = 0.12f;

    [Header("Orthographic Size 補間時間")]
    [Tooltip("Zone による orthographicSize 上書きが切り替わる際の補間時間です。0 のときは即時反映します。")]
    [SerializeField] private float orthographicSizeSmoothTime = 0.10f;

    [Header("デバッグGizmoを描画するか")]
    [Tooltip("有効にすると、Scene ビュー上に目標位置・Clamp 後位置・追従対象位置の Gizmo を描画します。カメラ挙動確認用です。")]
    // Scene ビュー上にデバッグ用 Gizmo を描画するか。
    [SerializeField] private bool drawDebugGizmos = true;

    [Header("Viewport位置をログ出力するか")]
    [Tooltip("有効にすると、追従対象アンカーの Viewport 座標を毎フレームログ出力します。画面内の見え方確認用ですが、ログ量は増えます。")]
    // true のとき、追従対象の Viewport 座標を毎フレーム出力する。
    // ログが大量に出るので常時 ON にはしない方がよい。
    [SerializeField] private bool logViewportPosition = false;

    // -----------------------------
    // ランタイム状態
    // -----------------------------

    // Zone から一時的に上書きされる現在有効な境界。
    // true のときだけ activeBoundsOverride を使う。
    private bool hasActiveBoundsOverride;

    // Zone から直接渡されるワールド座標系 Bounds。
    // hasActiveBoundsOverride == true の間だけ有効。
    private Bounds activeBoundsOverride;

    // 通常時に戻るための World 基準 Orthographic Size。
    private float worldOrthographicSize;

    // Zone からの Orthographic Size 一時上書き。
    private bool hasActiveOrthographicSizeOverride;
    private float activeOrthographicSizeOverride;
    private float orthographicSizeVelocity;

    // 通常時に戻るための World 基準 追従スムーズ時間。
    private float worldSmoothTimeX;
    private float worldSmoothTimeY;

    // Zone からの追従スムーズ時間 一時上書き。
    private bool hasActiveFollowSmoothingOverride;
    private float activeSmoothTimeXOverride;
    private float activeSmoothTimeYOverride;

    // 通常時に戻るための World 基準 Orthographic Size 補間時間。
    private float worldOrthographicSizeSmoothTime;

    // Zone からの Orthographic Size 補間時間 一時上書き。
    private bool hasActiveOrthographicSizeSmoothTimeOverride;
    private float activeOrthographicSizeSmoothTimeOverride;

    // 現在有効な Zone 群。
    private readonly List<CameraZone> activeZones = new List<CameraZone>();
    // Zone ごとの「最後に入った順」を記録する。
    private readonly Dictionary<CameraZone, int> zoneEnterOrders = new Dictionary<CameraZone, int>();
    private int zoneEnterSequence;

    // SmoothDamp 用の内部速度。
    // ref で渡してフレーム間で保持する必要がある。
    private float velocityX;
    private float velocityY;

    // 一時追従ターゲットのランタイム状態。
    // Inspector では設定せず、SetTemporaryTarget / ClearTemporaryTarget でのみ変更する。
    private Transform temporaryTargetAnchor;
    // 一時追従ターゲットの有効期限( Time.time 基準 )。
    private float temporaryTargetExpireTime = -1f;

    // Clamp 前の「行きたい位置」。
    private Vector3 desiredPosition;
    // 境界適用後の「実際に目指す位置」。
    private Vector3 clampedPosition;

    // -----------------------------
    // 読み取り専用公開プロパティ
    // -----------------------------

    // 実際に使用するワールド座標系境界。
    // override が有効ならそちらを優先し、無ければ worldBounds を使う。
    private Bounds EffectiveWorldBounds => hasActiveBoundsOverride
        ? activeBoundsOverride
        : worldBounds.WorldBounds;

    // 実際に使用する Orthographic Size。
    // override が有効ならそちらを優先し、無ければ world の通常値を使う。
    private float EffectiveOrthographicSize => hasActiveOrthographicSizeOverride
        ? activeOrthographicSizeOverride
        : worldOrthographicSize;

    // 実際に使用する X/Y 追従スムーズ時間。
    private float EffectiveSmoothTimeX => hasActiveFollowSmoothingOverride
        ? activeSmoothTimeXOverride
        : worldSmoothTimeX;

    private float EffectiveSmoothTimeY => hasActiveFollowSmoothingOverride
        ? activeSmoothTimeYOverride
        : worldSmoothTimeY;

    // 実際に使用する Orthographic Size 補間時間。
    private float EffectiveOrthographicSizeSmoothTime => hasActiveOrthographicSizeSmoothTimeOverride
        ? activeOrthographicSizeSmoothTimeOverride
        : worldOrthographicSizeSmoothTime;

    // デバッグや外部参照用の読み取り専用公開プロパティ。
    public Vector3 DesiredPosition => desiredPosition;
    public Vector3 ClampedPosition => clampedPosition;
    public bool HasActiveBoundsOverride => hasActiveBoundsOverride;
    public Bounds ActiveBoundsOverride => activeBoundsOverride;
    public CameraBounds WorldBounds => worldBounds;
    public Bounds EffectiveBounds => EffectiveWorldBounds;
    public bool HasActiveOrthographicSizeOverride => hasActiveOrthographicSizeOverride;
    public float ActiveOrthographicSizeOverride => activeOrthographicSizeOverride;
    public float EffectiveSize => EffectiveOrthographicSize;
    public bool HasActiveFollowSmoothingOverride => hasActiveFollowSmoothingOverride;
    public float ActiveSmoothTimeXOverride => activeSmoothTimeXOverride;
    public float ActiveSmoothTimeYOverride => activeSmoothTimeYOverride;
    public float EffectiveFollowSmoothTimeX => EffectiveSmoothTimeX;
    public float EffectiveFollowSmoothTimeY => EffectiveSmoothTimeY;
    public bool HasActiveOrthographicSizeSmoothTimeOverride => hasActiveOrthographicSizeSmoothTimeOverride;
    public float ActiveOrthographicSizeSmoothTimeOverride => activeOrthographicSizeSmoothTimeOverride;
    public float EffectiveSizeSmoothTime => EffectiveOrthographicSizeSmoothTime;
    public bool HasTemporaryTarget => temporaryTargetAnchor != null && !IsTemporaryTargetExpired();
    public Transform TemporaryTargetAnchor => temporaryTargetAnchor;
    public float TemporaryTargetExpireTime => temporaryTargetExpireTime;

    // -----------------------------
    // Unity lifecycle
    // -----------------------------

    private void Reset()
    {
        // コンポーネント追加時や Reset 時に、同一 GameObject の Camera を自動設定する。
        targetCamera = GetComponent<Camera>();
    }

    private void Awake()
    {
        if (targetCamera == null)
        {
            targetCamera = GetComponent<Camera>();
        }

        if (targetCamera != null)
        {
            worldOrthographicSize = Mathf.Max(0.01f, targetCamera.orthographicSize);
        }

        worldSmoothTimeX = Mathf.Max(0f, smoothTimeX);
        worldSmoothTimeY = Mathf.Max(0f, smoothTimeY);
        worldOrthographicSizeSmoothTime = Mathf.Max(0f, orthographicSizeSmoothTime);

        // 必要に応じて追従対象アンカーを自動解決する。
        ResolveTargetAnchor();
    }

    private void LateUpdate()
    {
        if (!ValidateRuntimeReferences())
        {
            return;
        }

        ApplyOrthographicSize();

        Transform effectiveTarget = GetEffectiveTargetAnchor();
        if (effectiveTarget == null)
        {
            return;
        }

        // 追従対象位置 + オフセット で理想位置を作る。
        desiredPosition = effectiveTarget.position + cameraOffset;

        // 理想位置をカメラ境界内に収めた最終候補位置を作る。
        clampedPosition = GetClampedPosition(desiredPosition);

        // X と Y を別々の SmoothDamp で補間する。
        // これにより、軸ごとの追従感を個別に調整できる。
        float finalX = Mathf.SmoothDamp(
            current: transform.position.x,
            target: clampedPosition.x,
            currentVelocity: ref velocityX,
            smoothTime: EffectiveSmoothTimeX);

        float finalY = Mathf.SmoothDamp(
            current: transform.position.y,
            target: clampedPosition.y,
            currentVelocity: ref velocityY,
            smoothTime: EffectiveSmoothTimeY);

        // Z は Clamp 後位置をそのまま採用する。
        transform.position = new Vector3(finalX, finalY, clampedPosition.z);

        // 必要なら、追従対象が画面内のどこに居るかを Viewport 座標で確認する。
        if (logViewportPosition)
        {
            Vector3 viewport = targetCamera.WorldToViewportPoint(effectiveTarget.position);
            Debug.Log(
                $"CameraTarget Viewport : x={viewport.x:F2}, y={viewport.y:F2}",
                this);
        }
    }

    // -----------------------------
    // 初期化・参照解決
    // -----------------------------

    private bool ValidateRuntimeReferences()
    {
        // Camera が無ければ追従計算できない。
        if (targetCamera == null)
        {
            Debug.LogWarning("PlayerCameraController: Camera reference is missing.", this);
            return false;
        }

        // Anchor が無ければ自動探索を試みる。
        if (targetAnchor == null)
        {
            ResolveTargetAnchor();

            // それでも見つからなければ更新不能。
            if (targetAnchor == null)
            {
                Debug.LogWarning("PlayerCameraController: Target anchor is missing.", this);
                return false;
            }
        }

        // フォールバック境界が無いと Clamp 計算ができない。
        if (worldBounds == null)
        {
            Debug.LogWarning("PlayerCameraController: World bounds reference is missing.", this);
            return false;
        }

        // この実装は Orthographic カメラ前提。
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
            // 指定タグのプレイヤーを探す。
            player = GameObject.FindWithTag(playerTag);
        }
        catch (UnityException)
        {
            // タグ未定義などで例外が出るケースを吸収する。
            return;
        }

        // プレイヤーが見つからなければ終了。
        if (player == null)
        {
            return;
        }

        // プレイヤー配下に指定名のアンカーがあるか探す。
        Transform foundAnchor = player.transform.Find(anchorChildName);

        // アンカーがあればそれを使い、無ければプレイヤー本体を追従対象にする。
        targetAnchor = foundAnchor != null ? foundAnchor : player.transform;
    }

    // -----------------------------
    // temporary target 管理
    // -----------------------------

    public void SetTemporaryTarget(Transform target, float duration)
    {
        if (target == null)
        {
            return;
        }

        temporaryTargetAnchor = target;
        temporaryTargetExpireTime = Time.time + Mathf.Max(0.01f, duration);
    }

    public void ClearTemporaryTarget()
    {
        temporaryTargetAnchor = null;
        temporaryTargetExpireTime = -1f;
    }

    public Transform GetEffectiveTargetAnchor()
    {
        if (temporaryTargetAnchor != null && !IsTemporaryTargetExpired())
        {
            return temporaryTargetAnchor;
        }

        if (temporaryTargetAnchor != null && IsTemporaryTargetExpired())
        {
            ClearTemporaryTarget();
        }

        return targetAnchor;
    }

    private bool IsTemporaryTargetExpired()
    {
        return temporaryTargetExpireTime <= Time.time;
    }

    // -----------------------------
    // zone 管理・再評価
    // -----------------------------

    public void ApplyZone(CameraZone zone)
    {
        if (zone == null)
        {
            return;
        }

        CameraBounds zoneBounds = zone.ZoneBounds;
        if (zoneBounds == null)
        {
            Debug.LogWarning("PlayerCameraController: CameraZone has no CameraBounds.", zone);
            return;
        }

        if (!activeZones.Contains(zone))
        {
            activeZones.Add(zone);
        }

        zoneEnterSequence++;
        zoneEnterOrders[zone] = zoneEnterSequence;

        ReevaluateActiveZone();
    }

    public void ClearZone(CameraZone zone)
    {
        if (zone == null)
        {
            return;
        }

        activeZones.Remove(zone);
        zoneEnterOrders.Remove(zone);

        ReevaluateActiveZone();
    }

    public void ResetRuntimeStateForRespawn()
    {
        activeZones.Clear();
        zoneEnterOrders.Clear();
        zoneEnterSequence = 0;

        ApplyWorldFallback();

        ClearTemporaryTarget();
        orthographicSizeVelocity = 0f;
        velocityX = 0f;
        velocityY = 0f;
    }

    private void ReevaluateActiveZone()
    {
        CameraZone resolvedZone = ResolveBestZone();
        ApplyResolvedZone(resolvedZone);
    }

    private CameraZone ResolveBestZone()
    {
        for (int i = activeZones.Count - 1; i >= 0; i--)
        {
            CameraZone activeZone = activeZones[i];
            if (activeZone != null && activeZone.ZoneBounds != null)
            {
                continue;
            }

            activeZones.RemoveAt(i);
            zoneEnterOrders.Remove(activeZone);
        }

        CameraZone bestZone = null;

        for (int i = 0; i < activeZones.Count; i++)
        {
            CameraZone candidate = activeZones[i];
            if (candidate == null)
            {
                continue;
            }

            if (bestZone == null || IsHigherPriority(candidate, bestZone))
            {
                bestZone = candidate;
            }
        }

        return bestZone;
    }

    private bool IsHigherPriority(CameraZone candidate, CameraZone currentBest)
    {
        if (candidate.Priority != currentBest.Priority)
        {
            return candidate.Priority > currentBest.Priority;
        }

        int candidateOrder = GetZoneEnterOrder(candidate);
        int currentBestOrder = GetZoneEnterOrder(currentBest);
        return candidateOrder > currentBestOrder;
    }

    private int GetZoneEnterOrder(CameraZone zone)
    {
        return zoneEnterOrders.TryGetValue(zone, out int order) ? order : int.MinValue;
    }

    private void ApplyResolvedZone(CameraZone zone)
    {
        if (zone == null)
        {
            ApplyWorldFallback();
            return;
        }

        ApplyZoneOverrides(zone);
    }

    private void ApplyZoneOverrides(CameraZone zone)
    {
        SetActiveBoundsOverride(zone.ZoneBounds.WorldBounds);

        if (zone.HasOrthographicSizeOverride)
        {
            SetActiveOrthographicSizeOverride(zone.OrthographicSizeOverride);
        }
        else
        {
            ClearActiveOrthographicSizeOverride();
        }

        if (zone.HasFollowSmoothingOverride)
        {
            SetActiveFollowSmoothingOverride(zone.SmoothTimeXOverride, zone.SmoothTimeYOverride);
        }
        else
        {
            ClearActiveFollowSmoothingOverride();
        }

        if (zone.HasOrthographicSizeSmoothTimeOverride)
        {
            SetActiveOrthographicSizeSmoothTimeOverride(zone.OrthographicSizeSmoothTimeOverride);
        }
        else
        {
            ClearActiveOrthographicSizeSmoothTimeOverride();
        }
    }

    private void ApplyWorldFallback()
    {
        ClearActiveBoundsOverride();
        ClearActiveOrthographicSizeOverride();
        ClearActiveFollowSmoothingOverride();
        ClearActiveOrthographicSizeSmoothTimeOverride();
    }

    // Zone 反映時の内部 override 操作 API。
    public void SetActiveBoundsOverride(Bounds newBounds)
    {
        activeBoundsOverride = newBounds;
        hasActiveBoundsOverride = true;
    }

    public void ClearActiveBoundsOverride()
    {
        hasActiveBoundsOverride = false;
    }

    public void SetActiveOrthographicSizeOverride(float newSize)
    {
        activeOrthographicSizeOverride = Mathf.Max(0.01f, newSize);
        hasActiveOrthographicSizeOverride = true;
    }

    public void ClearActiveOrthographicSizeOverride()
    {
        hasActiveOrthographicSizeOverride = false;
    }

    public void SetActiveFollowSmoothingOverride(float newSmoothTimeX, float newSmoothTimeY)
    {
        activeSmoothTimeXOverride = Mathf.Max(0f, newSmoothTimeX);
        activeSmoothTimeYOverride = Mathf.Max(0f, newSmoothTimeY);
        hasActiveFollowSmoothingOverride = true;
    }

    public void ClearActiveFollowSmoothingOverride()
    {
        hasActiveFollowSmoothingOverride = false;
    }

    public void SetActiveOrthographicSizeSmoothTimeOverride(float newSmoothTime)
    {
        activeOrthographicSizeSmoothTimeOverride = Mathf.Max(0f, newSmoothTime);
        hasActiveOrthographicSizeSmoothTimeOverride = true;
    }

    public void ClearActiveOrthographicSizeSmoothTimeOverride()
    {
        hasActiveOrthographicSizeSmoothTimeOverride = false;
    }

    public Bounds GetEffectiveBounds()
    {
        return EffectiveWorldBounds;
    }

    // -----------------------------
    // camera 実行
    // -----------------------------

    private Vector3 GetClampedPosition(Vector3 desired)
    {
        // 現在使うべきワールド境界を取得する。
        Bounds bounds = EffectiveWorldBounds;

        // Orthographic カメラの画面半サイズを求める。
        float halfHeight = targetCamera.orthographicSize;
        float halfWidth = halfHeight * targetCamera.aspect;

        // カメラ中心が取りうる最小・最大位置を計算する。
        // 画面端が境界をはみ出さないよう、半画面サイズ分を内側に寄せる。
        float minX = bounds.min.x + halfWidth;
        float maxX = bounds.max.x - halfWidth;
        float minY = bounds.min.y + halfHeight;
        float maxY = bounds.max.y - halfHeight;

        // 境界が画面より狭い場合は Clamp 不能になるので、中心固定にする。
        float clampedX = (minX > maxX) ? bounds.center.x : Mathf.Clamp(desired.x, minX, maxX);
        float clampedY = (minY > maxY) ? bounds.center.y : Mathf.Clamp(desired.y, minY, maxY);

        // Z は desired 側の値をそのまま使う。
        return new Vector3(clampedX, clampedY, desired.z);
    }

    private void ApplyOrthographicSize()
    {
        float targetSize = Mathf.Max(0.01f, EffectiveOrthographicSize);
        float smoothTime = Mathf.Max(0f, EffectiveOrthographicSizeSmoothTime);
        if (smoothTime <= 0f)
        {
            targetCamera.orthographicSize = targetSize;
            orthographicSizeVelocity = 0f;
            return;
        }

        targetCamera.orthographicSize = Mathf.SmoothDamp(
            current: targetCamera.orthographicSize,
            target: targetSize,
            currentVelocity: ref orthographicSizeVelocity,
            smoothTime: smoothTime);
    }

    // -----------------------------
    // debug / gizmo
    // -----------------------------

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        // Gizmo 無効なら何も描かない。
        if (!drawDebugGizmos)
        {
            return;
        }

        // 理想位置(desiredPosition)を黄色で表示。
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(desiredPosition, 0.15f);

        // Clamp 後位置(clampedPosition)を緑で表示。
        Gizmos.color = Color.green;
        Gizmos.DrawSphere(clampedPosition, 0.18f);

        // 理想位置から Clamp 後位置までの差を線で表示。
        Gizmos.color = Color.white;
        Gizmos.DrawLine(desiredPosition, clampedPosition);

        // 通常追従アンカー位置をマゼンタで表示。
        if (targetAnchor != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawSphere(targetAnchor.position, 0.12f);
        }

        // 一時追従アンカー位置をシアンで表示。
        if (temporaryTargetAnchor != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(temporaryTargetAnchor.position, 0.12f);
        }
    }
#endif
}