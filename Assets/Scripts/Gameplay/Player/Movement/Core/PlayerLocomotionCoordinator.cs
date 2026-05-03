using System;
using Game.Input;
using UnityEngine;

// PlayerController 内部専用の通常移動システム統合コーディネーター。
// 各サブシステムを統合し、外部に同じインターフェースを提供する。
internal sealed class PlayerLocomotionCoordinator
{
    // ============================================================
    // サブシステム
    // ============================================================

    private readonly PlayerMovementCore movementCore;
    private readonly PlayerJumpSystem jumpSystem;
    private readonly PlayerDashSystem dashSystem;
    private readonly PlayerWallActionSystem wallActionSystem;
    private readonly PlayerLedgeClimbSystem ledgeClimbSystem;
    private readonly PlayerInputAssistSystem inputAssistSystem;

    // ============================================================
    // 共有依存関係
    // ============================================================

    private readonly PlayerLocomotionDependencies deps;

    // ============================================================
    // 内部状態
    // ============================================================

    // 今Tickで解決済みの移動補正。
    private PlayerLocomotionModifierRequest resolvedLocomotionModifier = PlayerLocomotionModifierRequest.Identity;

    // ============================================================
    // デバッグ・外部公開プロパティ
    // ============================================================

    // デバッグ表示用のコヨーテタイマー。
    internal float CoyoteTimer => jumpSystem.CoyoteTimer;

    // デバッグ表示用のジャンプバッファタイマー。
    internal float JumpBufferTimer => jumpSystem.JumpBufferTimer;

    // デバッグ表示用のジャンプホールドタイマー。
    internal float JumpHoldTimer => jumpSystem.JumpHoldTimer;

    // デバッグ表示用のダッシュバッファタイマー。
    internal float DashBufferTimer => dashSystem.DashBufferTimer;

    // 見た目更新用の通常ジャンプ単発フラグ。
    internal bool JustJumpedThisFrame => jumpSystem.JustJumpedThisFrame;

    // 見た目更新用の壁ジャンプ単発フラグ。
    internal bool JustWallJumpedThisFrame => jumpSystem.JustWallJumpedThisFrame;

    // LocomotionCoordinator の依存を受け取るコンストラクタ。
    internal PlayerLocomotionCoordinator(
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
        // 共通依存関係を構築。
        deps = new PlayerLocomotionDependencies(
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

        // 各サブシステムを構築。
        movementCore = new PlayerMovementCore(deps);
        jumpSystem = new PlayerJumpSystem(deps);
        dashSystem = new PlayerDashSystem(deps);
        wallActionSystem = new PlayerWallActionSystem(deps);
        ledgeClimbSystem = new PlayerLedgeClimbSystem(deps);
        inputAssistSystem = new PlayerInputAssistSystem(deps);
    }

    // ============================================================
    // 初期化・リセット
    // ============================================================

    // 可変ジャンプカット抑制フラグを更新する。
    internal void SetSuppressVariableJumpCutThisTick(bool value)
    {
        jumpSystem.SetSuppressVariableJumpCutThisTick(value);
    }

    // 見た目用単発フラグをリセットする。
    internal void ResetOneShotFlags()
    {
        jumpSystem.ResetOneShotFlags();
    }

    // 復帰時に Locomotion 内部タイマーを初期化する。
    internal void ResetRuntimeTimers()
    {
        jumpSystem.ResetRuntimeTimers();
        dashSystem.ResetRuntimeTimers();
        wallActionSystem.ResetRuntimeTimers();
        ledgeClimbSystem.ResetRuntimeTimers();
        resolvedLocomotionModifier = PlayerLocomotionModifierRequest.Identity;
    }

    // 物理Tick用の移動補正を解決する。
    internal void ResolveLocomotionModifiersThisTick()
    {
        resolvedLocomotionModifier = deps.FrameRequests.requestedLocomotionModifierThisTick;
        deps.FrameRequests.requestedLocomotionModifierThisTick = PlayerLocomotionModifierRequest.Identity;
    }

    // ============================================================
    // タイマー更新
    // ============================================================

    // ジャンプ補助タイマーを更新する。
    internal void UpdateJumpAssistTimers(float deltaTime)
    {
        jumpSystem.UpdateJumpAssistTimers(deltaTime);
    }

    // 壁掴まり制限時間を更新する。
    internal void UpdateWallGrabLimitTimer(float deltaTime)
    {
        wallActionSystem.UpdateWallGrabLimitTimer(deltaTime);
    }

    // 移動入力から向きを更新する。
    internal void UpdateFacingFromMoveInput()
    {
        movementCore.UpdateFacingFromMoveInput();
    }

    // 壁キック関連ロックタイマーを更新する。
    internal void UpdateWallJumpLockTimer(float deltaTime)
    {
        wallActionSystem.UpdateWallJumpLockTimer(deltaTime);
    }

    // ダッシュ継続タイマーを更新する。
    internal void UpdateDashTimers(float deltaTime)
    {
        dashSystem.UpdateDashTimers(deltaTime, () => dashSystem.EndDash(() => jumpSystem.SetDashEndJumpCutLockTimer()));
    }

    // ダッシュ入力バッファタイマーを更新する。
    internal void UpdateDashBufferTimer(float deltaTime)
    {
        dashSystem.UpdateDashBufferTimer(deltaTime);
    }

    // 地上ダッシュ連続制限タイマーを更新する。
    internal void UpdateGroundDashCooldownTimer(float deltaTime)
    {
        dashSystem.UpdateGroundDashCooldownTimer(deltaTime);
    }

    // 着地直後だけ横制御を強める補正タイマーを更新する。
    internal void UpdateLandingAssistTimer(float deltaTime)
    {
        movementCore.UpdateLandingAssistTimer(deltaTime);
    }

    // 着地時に地上ダッシュ連続制限を解除する。
    internal void HandleGroundDashCooldownOnLanding()
    {
        movementCore.HandleGroundDashCooldownOnLanding();
    }

    // ============================================================
    // ジャンプ処理
    // ============================================================

    // ジャンプ入力を処理して速度へ反映する。
    internal void ApplyJump()
    {
        jumpSystem.ApplyJump(() => wallActionSystem.TryApplyWallKick());
    }

    // 可変ジャンプの早離しカットを適用する。
    internal void ApplyVariableJumpCut()
    {
        jumpSystem.ApplyVariableJumpCut();
    }

    // ============================================================
    // 壁アクション
    // ============================================================

    // 壁滑り状態と落下速度上限を更新する。
    internal void ApplyWallSlide()
    {
        wallActionSystem.ApplyWallSlide();
    }

    // 急降下開始条件を満たす場合に状態を切り替える。
    internal void TryStartFastFall()
    {
        movementCore.TryStartFastFall();
    }

    // 壁捕まり状態の進入・離脱判定を更新する。
    internal void UpdateWallGrabState()
    {
        wallActionSystem.UpdateWallGrabState();
    }

    // 壁捕まり中の専用移動を適用する。
    internal void ApplyWallGrabMovement()
    {
        wallActionSystem.ApplyWallGrabMovement(() => ledgeClimbSystem.TryStartLedgeClimb());
    }

    // ============================================================
    // 基本移動
    // ============================================================

    // 通常の横移動速度を更新する。
    internal void ApplyHorizontalMovement(float deltaTime)
    {
        bool isNearApex = jumpSystem.IsNearJumpApex(deps.Rb.linearVelocity.y);
        movementCore.ApplyHorizontalMovement(deltaTime, resolvedLocomotionModifier, isNearApex);
    }

    // カスタム重力と落下上限を適用する。
    internal void ApplyCustomGravity()
    {
        inputAssistSystem.TryApplyCornerCorrection();
        bool isNearApex = jumpSystem.IsNearJumpApex(deps.Rb.linearVelocity.y);
        movementCore.ApplyCustomGravity(resolvedLocomotionModifier, jumpSystem.JumpHoldTimer, isNearApex);
    }

    // ============================================================
    // ダッシュ処理
    // ============================================================

    // ダッシュリソース回復状態を更新する。
    internal void UpdateDashResourceState()
    {
        dashSystem.UpdateDashResourceState();
    }

    // 現在ダッシュを使用可能かを判定する。
    internal bool CanUseDashNowInternal()
    {
        return dashSystem.CanUseDashNowInternal();
    }

    // 指定理由でダッシュ回復を試みる。
    internal bool TryRefillDash(DashRefillReason reason)
    {
        return dashSystem.TryRefillDash(reason);
    }

    // ダッシュ開始を試みる。
    internal void TryStartDash()
    {
        dashSystem.TryStartDash((side) => wallActionSystem.ExitWallGrab());
    }

    // ダッシュ中の専用速度を適用する。
    internal void ApplyDashVelocity()
    {
        dashSystem.ApplyDashVelocity(resolvedLocomotionModifier, () => inputAssistSystem.TryApplyDashCornerCorrection());
    }

    // ダッシュ終了後の接地スナップ可否を判定する。
    internal bool CanSnapToGroundAfterDash()
    {
        // DashSystem内部で判定されるため、ここでは常にfalseを返す（実装はEndDash内部）
        return false;
    }

    // ============================================================
    // 崖乗り上げ
    // ============================================================

    // 崖乗り上げ中の移動を更新する。
    internal void UpdateLedgeClimb()
    {
        ledgeClimbSystem.UpdateLedgeClimb();
    }
}
