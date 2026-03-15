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

        // 画面デバッグ表示用のラベルスタイルを初期化する。
        labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            normal = { textColor = Color.white }
        };
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

        DrawLine($"Grounded: {playerController.IsGrounded}", startX, ref y, lineHeight);
        DrawLine($"Velocity X: {playerController.CurrentVelocity.x:F3}", startX, ref y, lineHeight);
        DrawLine($"Velocity Y: {playerController.CurrentVelocity.y:F3}", startX, ref y, lineHeight);
        DrawLine($"Jump Requested: {playerController.JumpRequested}", startX, ref y, lineHeight);
        DrawLine($"Ground Hit: {playerController.GroundCheckHit}", startX, ref y, lineHeight);
    }

    private void OnDrawGizmos()
    {
        // Edit/Play いずれでも参照を補完して描画の取りこぼしを減らす。
        PlayerController target = playerController != null ? playerController : GetComponent<PlayerController>();
        if (target == null)
        {
            return;
        }

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

    // 同形式の行を重複なく描くための補助メソッド。
    private void DrawLine(string text, float x, ref float y, float lineHeight)
    {
        GUI.Label(new Rect(x, y, 360f, lineHeight), text, labelStyle);
        y += lineHeight;
    }
}