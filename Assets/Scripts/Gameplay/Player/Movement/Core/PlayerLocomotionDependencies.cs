using System;
using Game.Input;
using UnityEngine;

// 移動システム全体で共有される依存関係をまとめた構造体。
internal readonly struct PlayerLocomotionDependencies
{
    // ============================================================
    // 状態・設定
    // ============================================================

    internal readonly PlayerRuntimeState RuntimeState;
    internal readonly PlayerFrameRequests FrameRequests;
    internal readonly PlayerMovementSettings Settings;

    // ============================================================
    // Unity コンポーネント
    // ============================================================

    internal readonly Rigidbody Rb;
    internal readonly CapsuleCollider CapsuleCollider;
    internal readonly Transform Transform;
    internal readonly PlayerInputReader InputReader;

    // ============================================================
    // 判定デリゲート
    // ============================================================

    internal readonly Func<bool> CanAcceptMoveInput;
    internal readonly Func<bool> CanAcceptJumpInput;
    internal readonly Func<bool> CanAcceptDashInput;
    internal readonly Func<bool> CanAcceptGrabInput;
    internal readonly Func<bool> IsActionLocked;
    internal readonly Func<bool> IsExternallyControlled;
    internal readonly Func<float> GetWorldCapsuleRadius;

    // ============================================================
    // 音響・演出デリゲート
    // ============================================================

    internal readonly Action PlayJumpSound;
    internal readonly Action PlayWallKickSound;
    internal readonly Action PlayWallKickVibration;

    // コンストラクタ。
    internal PlayerLocomotionDependencies(
        PlayerRuntimeState runtimeState,
        PlayerFrameRequests frameRequests,
        PlayerMovementSettings settings,
        Rigidbody rb,
        CapsuleCollider capsuleCollider,
        Transform transform,
        PlayerInputReader inputReader,
        Func<bool> canAcceptMoveInput,
        Func<bool> canAcceptJumpInput,
        Func<bool> canAcceptDashInput,
        Func<bool> canAcceptGrabInput,
        Func<bool> isActionLocked,
        Func<bool> isExternallyControlled,
        Func<float> getWorldCapsuleRadius,
        Action playJumpSound,
        Action playWallKickSound,
        Action playWallKickVibration)
    {
        RuntimeState = runtimeState;
        FrameRequests = frameRequests;
        Settings = settings;
        Rb = rb;
        CapsuleCollider = capsuleCollider;
        Transform = transform;
        InputReader = inputReader;
        CanAcceptMoveInput = canAcceptMoveInput;
        CanAcceptJumpInput = canAcceptJumpInput;
        CanAcceptDashInput = canAcceptDashInput;
        CanAcceptGrabInput = canAcceptGrabInput;
        IsActionLocked = isActionLocked;
        IsExternallyControlled = isExternallyControlled;
        GetWorldCapsuleRadius = getWorldCapsuleRadius;
        PlayJumpSound = playJumpSound;
        PlayWallKickSound = playWallKickSound;
        PlayWallKickVibration = playWallKickVibration;
    }
}
