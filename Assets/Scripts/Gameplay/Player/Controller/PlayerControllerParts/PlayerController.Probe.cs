using UnityEngine;

public sealed partial class PlayerController
{
    private bool CheckGrounded()
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
        float castDistance = movementSettings.groundCheckDistance + 0.01f;

        // 下方向へ SphereCast して接地しているか調べる。
        bool hit = Physics.SphereCast(
            bottomSphereCenter,
            worldRadius * 0.95f,
            -up,
            out _,
            castDistance,
            movementSettings.groundLayerMask,
            QueryTriggerInteraction.Ignore);

        // デバッグ描画や確認用に今回の判定情報を保存する。
        groundCheckOrigin = bottomSphereCenter;
        groundCheckRadius = worldRadius * 0.95f;
        groundCheckDistance = castDistance;
        groundCheckHit = hit;

        return hit;
    }

    private void CheckWallContact()
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
        float castRadius = Mathf.Max(0.01f, movementSettings.wallCheckRadius);

        // 壁判定距離に少し余裕を足す。
        float castDistance = movementSettings.wallCheckDistance + 0.01f;

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
            movementSettings.groundLayerMask,
            QueryTriggerInteraction.Ignore);

        // 右方向へ SphereCast して右壁の接触を調べる。
        bool hitRight = Physics.SphereCast(
            worldCenter,
            castRadius,
            right,
            out _,
            castDistance + worldRadius,
            movementSettings.groundLayerMask,
            QueryTriggerInteraction.Ignore);

        leftWallCheckHit = hitLeft;
        rightWallCheckHit = hitRight;

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

    private float GetWorldCapsuleRadius()
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