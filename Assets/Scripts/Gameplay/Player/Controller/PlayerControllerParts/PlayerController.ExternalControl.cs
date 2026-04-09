using UnityEngine;

public sealed partial class PlayerController
{
    // 現在、外部制御中か。
    // フェーズ2では未対応のため false 固定。
    public bool IsExternallyControlled => externalControlSystem != null && externalControlSystem.IsExternallyControlled;
    // 現在の外部制御モード。
    // フェーズ2では未対応のため None 固定。
    public ExternalControlMode CurrentExternalControlMode =>
        externalControlSystem != null ? externalControlSystem.CurrentExternalControlMode : ExternalControlMode.None;

    // 指定した外部制御要求を受け入れ可能か。
    // フェーズ2では未対応のため常に false。
    public bool CanAcceptExternalControl(in PlayerExternalControlRequest request)
    {
        if (externalControlSystem == null)
        {
            return false;
        }

        return externalControlSystem.CanAcceptExternalControl(request);
    }

    // 外部制御を開始し、成功時は制御セッションを返す。
    // フェーズ2では未対応のため Invalid を返して false。
    public bool TryBeginExternalControl(
        in PlayerExternalControlRequest request,
        out PlayerExternalControlSession session)
    {
        if (externalControlSystem == null)
        {
            session = PlayerExternalControlSession.Invalid;
            return false;
        }

        return externalControlSystem.TryBeginExternalControl(request, out session);
    }

    // プレイヤーの向き変更を要求する。
    // フェーズ2では単発要求として先に実装する。
    public void RequestFacing(int facing)
    {
        if (externalControlSystem == null)
        {
            SetFacingInternal(facing);
            return;
        }

        externalControlSystem.RequestFacingThisFrame(facing);
        externalControlSystem.ApplyResolvedControl();
    }

    // プレイヤーを指定位置へワープさせることを要求する。
    // フェーズ2では安全な最小実装を先に入れる。
    public void RequestWarp(Vector3 targetPosition, WarpOptions options = default)
    {
        // 推測:
        // 既存で Rigidbody をキャッシュしている前提。
        transform.position = targetPosition;

        if (externalControlSystem == null)
        {
            transform.position = targetPosition;


            if (rb != null && options.ClearVelocity)
            {
                rb.linearVelocity = Vector3.zero;
            }

            if (options.UpdateFacing)
            {
                SetFacingInternal(options.Facing);
            }

            return;
        }
        externalControlSystem.RequestWarp(targetPosition, options);
        externalControlSystem.ApplyResolvedControl();
    }

    // 向き更新の内部関数。
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

    }
}