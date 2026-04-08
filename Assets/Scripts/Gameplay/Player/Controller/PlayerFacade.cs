using UnityEngine;
using static PlayerController;

// PlayerController への公開窓口だけを担当する
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerController))]
public sealed class PlayerFacade : MonoBehaviour
{
    // 実際のプレイヤー制御本体
    private PlayerController playerController;

    // 必須コンポーネントを取得してキャッシュする
    private void Awake()
    {
        playerController = GetComponent<PlayerController>();
    }

    // 現在ダッシュ中か
    public bool IsDashActive => playerController.IsDashActive;

    // 今ダッシュを使えるか
    public bool CanUseDashNow => playerController.CanUseDashNow;

    // 壁掴み中か
    public bool IsWallGrabbing => playerController.IsWallGrabbing;

    // 接地中か
    public bool IsGrounded => playerController.IsGrounded;

    // 空中状態か
    public bool IsAirborne => playerController.IsAirborne;

    // 向き
    public int Facing => playerController.Facing;

    // 移動入力方向
    public Vector2 MoveInputDirection => playerController.MoveInputDirection;

    // 斜め入力か
    public bool IsMoveInputDiagonal => playerController.IsMoveInputDiagonal;

    // ダッシュ回復を試みる
    public bool TryRefillDash(DashRefillReason reason)
    {
        return playerController.TryRefillDash(reason);
    }

    // このフレームだけ入力ブロックを要求する
    public void RequestInputBlockThisFrame(InputBlockFlags flags)
    {
        playerController.RequestInputBlockThisFrame(flags);
    }

    // 死亡を要求する
    public void RequestKill(Vector3 damageDirection)
    {
        playerController.RequestKill(damageDirection);
    }

    // ノックバックを要求する
    public void RequestKnockback(Vector3 force)
    {
        playerController.RequestKnockback(force);
    }

    // 外部打ち上げが発生したことを通知する
    public void NotifyExternalLaunch()
    {
        playerController.NotifyExternalLaunch();
    }

    // この physics tick の移動補正を要求する
    public void RequestLocomotionModifierThisTick(PlayerLocomotionModifierRequest request)
    {
        playerController.RequestLocomotionModifierThisTick(request);
    }
}