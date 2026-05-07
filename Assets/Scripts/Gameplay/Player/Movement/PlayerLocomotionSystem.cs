using System;
using Game.Input;
using UnityEngine;

// PlayerController 内部専用の通常移動システム。
// 各サブシステムへの委譲を行うファサード。
internal sealed class PlayerLocomotionSystem
{
    // 実際の処理を行うコーディネーター。
    private readonly PlayerLocomotionCoordinator coordinator;

    // ============================================================
    // デバッグ・外部公開プロパティ（コーディネーターへ委譲）
    // ============================================================

    internal float CoyoteTimer => coordinator.CoyoteTimer;
    internal float JumpBufferTimer => coordinator.JumpBufferTimer;
    internal float JumpHoldTimer => coordinator.JumpHoldTimer;
    internal float DashBufferTimer => coordinator.DashBufferTimer;
    internal bool JustJumpedThisFrame => coordinator.JustJumpedThisFrame;
    internal bool JustWallJumpedThisFrame => coordinator.JustWallJumpedThisFrame;

    // LocomotionSystem の依存を受け取り、内部でコーディネーターを構築する。
    internal PlayerLocomotionSystem(
        PlayerRuntimeState runtimeState,
        PlayerFrameRequests frameRequests,
        PlayerMovementSettings movementSettings,
        Rigidbody rb,
        CapsuleCollider capsuleCollider,
        Transform transform,
        PlayerInputReader playerInputReader,
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
        // コーディネーターへ依存を渡して構築する。
        coordinator = new PlayerLocomotionCoordinator(
            runtimeState,
            frameRequests,
            movementSettings,
            rb,
            capsuleCollider,
            transform,
            playerInputReader,
            canAcceptMoveInput,
            canAcceptJumpInput,
            canAcceptDashInput,
            canAcceptGrabInput,
            isActionLocked,
            isExternallyControlled,
            getWorldCapsuleRadius,
            playJumpSound,
            playWallKickSound,
            playWallKickVibration);
    }

    // ============================================================
    // 初期化・リセット（コーディネーターへ委譲）
    // ============================================================

    internal void SetSuppressVariableJumpCutThisTick(bool value) => coordinator.SetSuppressVariableJumpCutThisTick(value);
    internal void ResetOneShotFlags() => coordinator.ResetOneShotFlags();
    internal void ResetRuntimeTimers() => coordinator.ResetRuntimeTimers();
    internal void ResolveLocomotionModifiersThisTick() => coordinator.ResolveLocomotionModifiersThisTick();

    // ============================================================
    // タイマー更新（コーディネーターへ委譲）
    // ============================================================

    internal void UpdateJumpAssistTimers(float deltaTime) => coordinator.UpdateJumpAssistTimers(deltaTime);
    internal void UpdateWallGrabLimitTimer(float deltaTime) => coordinator.UpdateWallGrabLimitTimer(deltaTime);
    internal void UpdateFacingFromMoveInput() => coordinator.UpdateFacingFromMoveInput();
    internal void UpdateWallJumpLockTimer(float deltaTime) => coordinator.UpdateWallJumpLockTimer(deltaTime);
    internal void UpdateDashTimers(float deltaTime) => coordinator.UpdateDashTimers(deltaTime);
    internal void UpdateDashBufferTimer(float deltaTime) => coordinator.UpdateDashBufferTimer(deltaTime);
    internal void UpdateGroundDashCooldownTimer(float deltaTime) => coordinator.UpdateGroundDashCooldownTimer(deltaTime);
    internal void HandleGroundDashCooldownOnLanding() => coordinator.HandleGroundDashCooldownOnLanding();

    // ============================================================
    // ジャンプ処理（コーディネーターへ委譲）
    // ============================================================

    internal void ApplyJump() => coordinator.ApplyJump();
    internal void ApplyVariableJumpCut() => coordinator.ApplyVariableJumpCut();

    // ============================================================
    // 壁アクション（コーディネーターへ委譲）
    // ============================================================

    internal void ApplyWallSlide() => coordinator.ApplyWallSlide();
    internal void UpdateWallGrabState() => coordinator.UpdateWallGrabState();
    internal void ApplyWallGrabMovement() => coordinator.ApplyWallGrabMovement();

    // ============================================================
    // 基本移動（コーディネーターへ委譲）
    // ============================================================

    internal void ApplyHorizontalMovement(float deltaTime) => coordinator.ApplyHorizontalMovement(deltaTime);
    internal void ApplyCustomGravity() => coordinator.ApplyCustomGravity();

    // ============================================================
    // ダッシュ処理（コーディネーターへ委譲）
    // ============================================================

    internal void UpdateDashResourceState() => coordinator.UpdateDashResourceState();
    internal bool CanUseDashNowInternal() => coordinator.CanUseDashNowInternal();
    internal bool TryRefillDash(DashRefillReason reason) => coordinator.TryRefillDash(reason);
    internal void TryStartDash() => coordinator.TryStartDash();
    internal void ApplyDashVelocity() => coordinator.ApplyDashVelocity();
    internal bool CanSnapToGroundAfterDash() => coordinator.CanSnapToGroundAfterDash();

    // ============================================================
    // ストンプ処理（コーディネーターへ委譲）
    // ============================================================

    internal bool TryStartStomp() => coordinator.TryStartStomp();
    internal void UpdateStompTimer(float deltaTime) => coordinator.UpdateStompTimer(deltaTime);
    internal void UpdateStompCancelByInput() => coordinator.UpdateStompCancelByInput();
    internal void UpdateStompEndByLanding() => coordinator.UpdateStompEndByLanding();
    internal void ApplyStompVelocity() => coordinator.ApplyStompVelocity();
    internal void EndStompByLanding() => coordinator.EndStompByLanding();
    internal void EndStompForWallGrab() => coordinator.EndStompForWallGrab();
    internal void EndStomp() => coordinator.EndStomp();

    // ============================================================
    // 崖乗り上げ（コーディネーターへ委譲）
    // ============================================================

    internal void UpdateLedgeClimb() => coordinator.UpdateLedgeClimb();
}
