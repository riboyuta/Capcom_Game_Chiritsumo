using UnityEngine;

public sealed partial class PlayerController
{
    // 現在、外部制御中か。
    // フェーズ2では未対応のため false 固定。
    public bool IsExternallyControlled => false;

    // 現在の外部制御モード。
    // フェーズ2では未対応のため None 固定。
    public ExternalControlMode CurrentExternalControlMode => ExternalControlMode.None;

    // 指定した外部制御要求を受け入れ可能か。
    // フェーズ2では未対応のため常に false。
    public bool CanAcceptExternalControl(in PlayerExternalControlRequest request)
    {
        return false;
    }

    // 外部制御を開始し、成功時は制御セッションを返す。
    // フェーズ2では未対応のため Invalid を返して false。
    public bool TryBeginExternalControl(
        in PlayerExternalControlRequest request,
        out PlayerExternalControlSession session)
    {
        session = PlayerExternalControlSession.Invalid;
        return false;
    }

    // プレイヤーの向き変更を要求する。
    // フェーズ2では単発要求として先に実装する。
    public void RequestFacing(int facing)
    {
        SetFacingInternal(facing);
    }

    // プレイヤーを指定位置へワープさせることを要求する。
    // フェーズ2では安全な最小実装を先に入れる。
    public void RequestWarp(Vector3 targetPosition, WarpOptions options = default)
    {
        // 推測:
        // 既存で Rigidbody をキャッシュしている前提。
        transform.position = targetPosition;

        if (rb != null && options.ClearVelocity)
        {
            rb.linearVelocity = Vector3.zero;
        }

        if (options.UpdateFacing)
        {
            RequestFacing(options.Facing);
        }
    }

    // 向き更新の内部関数。
    // 既存実装があるならそれを呼ぶ。無ければ後で既存流儀に寄せる。
    private void SetFacingInternal(int nextFacing)
    {
        if (nextFacing == 0)
        {
            return;
        }

        int normalizedFacing = nextFacing > 0 ? 1 : -1;

        if (runtimeState.facing == normalizedFacing)
        {
            return;
        }

        runtimeState.facing = normalizedFacing;

        // 推測:
        // 向き変更時に見た目同期が必要なら、ここで Sprite / View / Animator 更新を呼ぶ。
        // 例:
        // playerView.SetFacing(facing);
        // UpdateVisualFacing();
    }
}