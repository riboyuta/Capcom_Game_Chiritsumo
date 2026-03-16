using UnityEngine;

// 同一 GameObject への多重アタッチを防ぐ。
[DisallowMultipleComponent]

// PlayerController の状態可視化専用なので依存を必須化する。
[RequireComponent(typeof(PlayerController))]
public sealed class PlayerDebugView : MonoBehaviour
{
    // 可視化対象の PlayerController。
    [SerializeField] private PlayerController playerController;

    private GUIStyle labelStyle;

    private void Awake()
    {
        // Inspector 未設定時は同一 GameObject から取得する。
        if (playerController == null)
        {
            playerController = GetComponent<PlayerController>();
        }

        // 依存が満たせない場合は描画不能なので無効化する。
        if (playerController == null)
        {
            Debug.LogError("PlayerDebugView requires PlayerController on the same GameObject.", this);
            enabled = false;
            return;
        }


        labelStyle = new GUIStyle();
        labelStyle.fontSize = 16;
        labelStyle.normal.textColor = Color.white;
    }

    private void OnGUI()
    {
        if (playerController == null)
        {
            return;
        }

        // OnGUI で軽量に状態を重ね表示する。
        const float startX = 16f;
        float y = 16f;
        const float lineHeight = 22f;

        DrawLine($"接地中: {playerController.IsGrounded}", startX, ref y, lineHeight);
        DrawLine($"速度 X: {playerController.CurrentVelocity.x:F3}", startX, ref y, lineHeight);
        DrawLine($"速度 Y: {playerController.CurrentVelocity.y:F3}", startX, ref y, lineHeight);
        DrawLine($"ジャンプ要求: {playerController.JumpRequested}", startX, ref y, lineHeight);
        DrawLine($"コヨーテタイマー: {playerController.CoyoteTimer:F3}", startX, ref y, lineHeight);
        DrawLine($"ジャンプバッファタイマー: {playerController.JumpBufferTimer:F3}", startX, ref y, lineHeight);
        DrawLine($"接地ヒット: {playerController.GroundCheckHit}", startX, ref y, lineHeight);
        DrawLine($"壁接触中: {playerController.IsTouchingWall}", startX, ref y, lineHeight);
        DrawLine($"壁方向: {playerController.WallSide}", startX, ref y, lineHeight);
        DrawLine($"壁滑り中: {playerController.IsWallSliding}", startX, ref y, lineHeight);
        DrawLine($"急降下中: {playerController.IsFastFalling}", startX, ref y, lineHeight);
        DrawLine($"壁キック入力ロック: {playerController.WallJumpControlLockTimer:F3}", startX, ref y, lineHeight);
        DrawLine($"向き: {playerController.Facing}", startX, ref y, lineHeight);
        DrawLine($"前ステップ中: {playerController.IsStepping}", startX, ref y, lineHeight);
        DrawLine($"前ステップタイマー: {playerController.StepTimer:F3}", startX, ref y, lineHeight);
        DrawLine($"前ステップクールダウン: {playerController.StepCooldownTimer:F3}", startX, ref y, lineHeight);
        DrawLine($"前ステップ要求: {playerController.StepRequested}", startX, ref y, lineHeight);
        DrawLine($"前ステップバッファタイマー: {playerController.StepBufferTimer:F3}", startX, ref y, lineHeight);
    }

    private void OnDrawGizmos()
    {
        // Edit/Play いずれでも参照を補完して描画の取りこぼしを減らす。
        PlayerController target = playerController != null ? playerController : GetComponent<PlayerController>();
        if (target == null)
        {
            return;
        }

        DrawGroundCheckGizmo(target);
        DrawWallCheckGizmo(target, true);
        DrawWallCheckGizmo(target, false);
    }

    private void DrawGroundCheckGizmo(PlayerController target)
    {
        // Ground 判定ヒット結果に応じて色を切り替える。
        Gizmos.color = target.GroundCheckHit ? Color.green : Color.red;

        Vector3 origin = target.GroundCheckOrigin;
        float radius = target.GroundCheckRadius;
        float distance = target.GroundCheckDistance;
        Vector3 castEnd = origin + (-target.transform.up * distance);

        // SphereCast の開始球と終了球、方向線を描く。
        Gizmos.DrawWireSphere(origin, radius);
        Gizmos.DrawWireSphere(castEnd, radius);
        Gizmos.DrawLine(origin, castEnd);
    }

    private void DrawWallCheckGizmo(PlayerController target, bool isLeft)
    {
        bool hit = isLeft ? target.LeftWallCheckHit : target.RightWallCheckHit;
        Vector3 origin = isLeft ? target.LeftWallCheckOrigin : target.RightWallCheckOrigin;
        float radius = target.WallCheckRadius;
        float distance = target.WallCheckDistance;
        Vector3 direction = isLeft ? -target.transform.right : target.transform.right;

        Gizmos.color = hit ? Color.cyan : Color.yellow;

        Vector3 castEnd = origin + (direction * distance);
        Gizmos.DrawWireSphere(origin, radius);
        Gizmos.DrawWireSphere(castEnd, radius);
        Gizmos.DrawLine(origin, castEnd);
    }

    // 同形式の行を重複なく描くための補助メソッド。
    private void DrawLine(string text, float x, ref float y, float lineHeight)
    {
        GUI.Label(new Rect(x, y, 360f, lineHeight), text, labelStyle);
        y += lineHeight;
    }
}