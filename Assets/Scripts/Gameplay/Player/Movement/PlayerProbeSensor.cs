using UnityEngine;

// PlayerController 内部専用の物理プローブ補助クラス。
internal sealed class PlayerProbeSensor
{
    // 接地/壁判定に使うカプセルコライダー。
    private readonly CapsuleCollider capsuleCollider;

    // 回転とスケールを含むワールド変換参照。
    private readonly Transform transform;

    // 判定距離やレイヤー設定参照。
    private readonly PlayerMovementSettings movementSettings;

    // Ground 判定デバッグ可視化用の SphereCast 開始位置。
    private Vector3 groundCheckOrigin;

    // Ground 判定デバッグ可視化用の SphereCast 半径。
    private float groundCheckRadius;

    // Ground 判定デバッグ可視化用の SphereCast 距離。
    private float groundCheckDistance;

    // Ground 判定デバッグ可視化用のヒット結果。
    private bool groundCheckHit;

    // Wall 判定デバッグ可視化用の左右 SphereCast 開始位置。
    private Vector3 leftWallCheckOrigin;
    private Vector3 rightWallCheckOrigin;

    // Wall 判定デバッグ可視化用の SphereCast 半径。
    private float wallCheckRadius;

    // Wall 判定デバッグ可視化用の SphereCast 距離。
    private float wallCheckDistance;

    // Wall 判定デバッグ可視化用の左右ヒット結果。
    private bool leftWallCheckHit;
    private bool rightWallCheckHit;

    // Ground 判定デバッグ可視化用の最新開始位置を返す。
    internal Vector3 GroundCheckOrigin => groundCheckOrigin;

    // Ground 判定デバッグ可視化用の最新半径を返す。
    internal float GroundCheckRadius => groundCheckRadius;

    // Ground 判定デバッグ可視化用の最新距離を返す。
    internal float GroundCheckDistance => groundCheckDistance;

    // Ground 判定デバッグ可視化用の最新ヒット結果を返す。
    internal bool GroundCheckHit => groundCheckHit;

    // Wall 判定デバッグ可視化用の左判定開始位置を返す。
    internal Vector3 LeftWallCheckOrigin => leftWallCheckOrigin;

    // Wall 判定デバッグ可視化用の右判定開始位置を返す。
    internal Vector3 RightWallCheckOrigin => rightWallCheckOrigin;

    // Wall 判定デバッグ可視化用の最新半径を返す。
    internal float WallCheckRadius => wallCheckRadius;

    // Wall 判定デバッグ可視化用の最新距離を返す。
    internal float WallCheckDistance => wallCheckDistance;

    // Wall 判定デバッグ可視化用の左ヒット結果を返す。
    internal bool LeftWallCheckHit => leftWallCheckHit;

    // Wall 判定デバッグ可視化用の右ヒット結果を返す。
    internal bool RightWallCheckHit => rightWallCheckHit;

    // 依存参照を受け取ってセンサーを初期化する。
    internal PlayerProbeSensor(
        CapsuleCollider capsuleCollider,
        Transform transform,
        PlayerMovementSettings movementSettings)
    {
        this.capsuleCollider = capsuleCollider;
        this.transform = transform;
        this.movementSettings = movementSettings;
    }

    // 接地判定を実行し、最新のデバッグ可視化データも更新する。
    internal bool CheckGrounded()
    {
        // カプセルの上方向を取得する。
        // 通常は Vector3.up と同じだが、回転を考慮して transform.up を使う。
        Vector3 up = transform.up;

        // CapsuleCollider.center はローカル座標なので、
        // ワールド座標へ変換して判定の基準位置を求める。
        Vector3 worldCenter = transform.TransformPoint(capsuleCollider.center);

        // 現在のスケールを考慮したワールド空間での半径を求める。
        float worldRadius = GetWorldCapsuleRadius();

        // カプセルの半高さを求める。
        // 半径より小さくならないように補正する。
        float halfHeight = Mathf.Max(capsuleCollider.height * 0.5f, capsuleCollider.radius);

        // Y スケールを考慮してワールド空間での半高さへ変換する。
        float worldHalfHeight = halfHeight * Mathf.Abs(transform.lossyScale.y);

        // 下側の球の中心位置を求める。
        // 接地判定の開始位置として使う。
        Vector3 bottomSphereCenter = worldCenter - up * (worldHalfHeight - worldRadius);

        // 接地判定距離に少し余裕を足す。
        // 境界付近の取りこぼしを減らすため。
        float castDistance = movementSettings.Detection.GroundCheckDistance + 0.01f;

        // 下方向へ SphereCast して接地しているか調べる。
        bool hit = Physics.SphereCast(
            bottomSphereCenter,
            worldRadius * 0.95f,
            -up,
            out _,
            castDistance,
            movementSettings.Detection.GroundLayerMask,
            QueryTriggerInteraction.Ignore);

        // デバッグ描画や確認用に今回の判定情報を保存する。
        groundCheckOrigin = bottomSphereCenter;
        groundCheckRadius = worldRadius * 0.95f;
        groundCheckDistance = castDistance;
        groundCheckHit = hit;

        return hit;
    }

    // 壁接触判定を実行し、接触結果と壁方向を返す。
    internal void CheckWallContact(float wallReattachLockTimer, out bool isTouchingWall, out int wallSide)
    {
        // ローカル右方向を取得する。
        // プレイヤーの回転を考慮して transform.right を使う。
        Vector3 right = transform.right;

        // カプセル中心をワールド座標へ変換する。
        Vector3 worldCenter = transform.TransformPoint(capsuleCollider.center);

        // 現在のスケールを考慮したワールド空間での半径を求める。
        float worldRadius = GetWorldCapsuleRadius();

        // 壁判定で使う SphereCast 半径を決める。
        // 極端に小さくならないように下限を持たせる。
        float castRadius = Mathf.Max(0.01f, movementSettings.Detection.WallCheckRadius);

        // 壁判定距離に少し余裕を足す。
        float castDistance = movementSettings.Detection.WallCheckDistance + 0.01f;

        // デバッグ表示用に左右の判定起点と設定値を保存する。
        leftWallCheckOrigin = worldCenter;
        rightWallCheckOrigin = worldCenter;
        wallCheckRadius = castRadius;
        wallCheckDistance = castDistance;

        // 左方向へ SphereCast して左壁の接触を調べる。
        // カプセル半径分も足して、胴体の外側まで含めて確認する。
        bool hitLeft = Physics.SphereCast(
            worldCenter,
            castRadius,
            -right,
            out _,
            castDistance + worldRadius,
            movementSettings.Detection.WallLayerMask,
            QueryTriggerInteraction.Ignore);

        // 右方向へ SphereCast して右壁の接触を調べる。
        bool hitRight = Physics.SphereCast(
            worldCenter,
            castRadius,
            right,
            out _,
            castDistance + worldRadius,
            movementSettings.Detection.WallLayerMask,
            QueryTriggerInteraction.Ignore);

        leftWallCheckHit = hitLeft;
        rightWallCheckHit = hitRight;

        // 壁キック後の再付着ロック中は壁アクション候補を無効化する。
        if (wallReattachLockTimer > 0f)
        {
            wallSide = 0;
            isTouchingWall = false;
            return;
        }

        // 両方ヒット、または両方非ヒットのときは
        // 壁方向を確定できないので未接触として扱う。
        if (hitLeft == hitRight)
        {
            wallSide = 0;
            isTouchingWall = false;
            return;
        }

        // 左だけヒットなら -1、右だけヒットなら 1 とする。
        wallSide = hitLeft ? -1 : 1;
        isTouchingWall = true;
    }

    // ワールド空間でのカプセル半径を取得する。
    internal float GetWorldCapsuleRadius()
    {
        // カプセル半径は水平断面の大きさに影響されるため、
        // X と Z のうち大きい方のスケールを使う。
        float scaleX = Mathf.Abs(transform.lossyScale.x);
        float scaleZ = Mathf.Abs(transform.lossyScale.z);
        float maxHorizontalScale = Mathf.Max(scaleX, scaleZ);

        // 極端に小さい値にならないように下限を持たせる。
        return Mathf.Max(0.01f, capsuleCollider.radius * maxHorizontalScale);
    }
}