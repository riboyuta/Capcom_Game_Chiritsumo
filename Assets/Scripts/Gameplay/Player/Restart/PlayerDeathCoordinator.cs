using System;
using System.Collections;
using UnityEngine;

internal sealed class PlayerDeathCoordinator
{
    private readonly MonoBehaviour coroutineRunner;
    private readonly PlayerHealthReactionSystem healthReactionSystem;
    private readonly Rigidbody rb;
    private readonly Transform playerTransform;
    private readonly PlayerRuntimeState runtimeState;
    private readonly PlayerFrameRequests frameRequests;
    private readonly PlayerLocomotionSystem locomotionSystem;
    private readonly PlayerMovementSettings movementSettings;
    private readonly Action stopAllRumble;
    private readonly Action stopAllSounds;
    private readonly Action resetVisualOneShotFlags;
    private readonly Action<string> logRespawn;
    private readonly Action<string> logRespawnWarning;

    private CheckpointSystem checkpointSystem;
    private StageResetSystem stageResetSystem;
    private PlayerCameraController playerCameraController;
    private PlayerDeathView playerDeathView;
    private PlayerController.DeathCause lastDeathCause = PlayerController.DeathCause.Damage;
    private Coroutine respawnSequenceCoroutine;

    internal PlayerDeathCoordinator(
        MonoBehaviour coroutineRunner,
        PlayerHealthReactionSystem healthReactionSystem,
        CheckpointSystem checkpointSystem,
        StageResetSystem stageResetSystem,
        PlayerDeathView playerDeathView,
        PlayerCameraController playerCameraController,
        Rigidbody rb,
        Transform playerTransform,
        PlayerRuntimeState runtimeState,
        PlayerFrameRequests frameRequests,
        PlayerLocomotionSystem locomotionSystem,
        PlayerMovementSettings movementSettings,
        Action stopAllRumble,
        Action stopAllSounds,
        Action resetVisualOneShotFlags,
        Action<string> logRespawn,
        Action<string> logRespawnWarning)
    {
        this.coroutineRunner = coroutineRunner;
        this.healthReactionSystem = healthReactionSystem;
        this.checkpointSystem = checkpointSystem;
        this.stageResetSystem = stageResetSystem;
        this.playerDeathView = playerDeathView;
        this.playerCameraController = playerCameraController;
        this.rb = rb;
        this.playerTransform = playerTransform;
        this.runtimeState = runtimeState;
        this.frameRequests = frameRequests;
        this.locomotionSystem = locomotionSystem;
        this.movementSettings = movementSettings;
        this.stopAllRumble = stopAllRumble;
        this.stopAllSounds = stopAllSounds;
        this.resetVisualOneShotFlags = resetVisualOneShotFlags;
        this.logRespawn = logRespawn;
        this.logRespawnWarning = logRespawnWarning;
    }

    internal void StartRespawnSequence(PlayerController.DeathCause deathCause)
    {
        lastDeathCause = deathCause;
        CaptureDeathFacingForVisual();

        if (respawnSequenceCoroutine != null)
        {
            coroutineRunner.StopCoroutine(respawnSequenceCoroutine);
        }

        respawnSequenceCoroutine = coroutineRunner.StartCoroutine(CoRespawnSequence());
    }

    private float ConfiguredDamageDeathIntroDuration => playerDeathView != null ? playerDeathView.DamageDeathIntroDuration : 0f;
    private float ConfiguredDamageDeathTiltAngle => playerDeathView != null ? playerDeathView.DamageDeathTiltAngle : 80f;
    private float ConfiguredDamageDeathZoomSizeOffset => playerDeathView != null ? playerDeathView.DamageDeathZoomSizeOffset : -0.35f;
    private float ConfiguredDamageDeathZoomSmoothTime => playerDeathView != null ? playerDeathView.DamageDeathZoomSmoothTime : 0.08f;
    private float ConfiguredBlackRespawnThreshold => playerDeathView != null ? playerDeathView.BlackRespawnThreshold : 0.85f;

    private IEnumerator CoRespawnSequence()
    {
        LogRespawn("Respawn sequence started");

        if (lastDeathCause == PlayerController.DeathCause.Damage)
        {
            PlayDamageDeathZoom();
            PlayDamageDeathIntro();
            yield return WaitForDamageDeathIntro();
        }

        ResolvePlayerDeathViewIfNeeded();
        if (playerDeathView != null)
        {
            if (lastDeathCause == PlayerController.DeathCause.Hazard)
            {
                LogRespawn("Hazard death uses immediate black transition");
                LogRespawn("Hazard black in started");
            }
            else
            {
                LogRespawn("Death transition in started");
            }

            playerDeathView.PlayTransitionIn(lastDeathCause);

            yield return new WaitUntil(() =>
                playerDeathView == null ||
                playerDeathView.GetBlackAmount() >= ConfiguredBlackRespawnThreshold);

            LogRespawn("Death transition reached respawn threshold");
        }
        else
        {
            LogRespawnWarning("PlayerDeathView missing (transition/intro unavailable)");
        }

        if (stageResetSystem == null)
        {
            stageResetSystem = UnityEngine.Object.FindFirstObjectByType<StageResetSystem>();
        }

        if (stageResetSystem != null)
        {
            stageResetSystem.ResetAllToRespawnState();
        }
        else
        {
            LogRespawnWarning("StageResetSystem missing (stage objects were not reset)");
        }

        if (checkpointSystem == null)
        {
            checkpointSystem = UnityEngine.Object.FindFirstObjectByType<CheckpointSystem>();
        }

        if (checkpointSystem == null)
        {
            LogRespawnWarning("Respawn checkpoint missing");
            yield return FinishRespawnSequence();
            yield break;
        }

        Transform checkpoint = checkpointSystem.GetCurrentCheckpoint();
        if (checkpoint == null)
        {
            LogRespawnWarning("Respawn checkpoint missing");
            yield return FinishRespawnSequence();
            yield break;
        }

        ResetCameraToWorldDefaults();

        LogRespawn($"Respawn checkpoint resolved: {checkpoint.name}");
        RespawnAt(checkpoint.position);
        LogRespawn("Respawn hidden by black");

        yield return FinishRespawnSequence();
    }

    private IEnumerator FinishRespawnSequence()
    {
        if (lastDeathCause == PlayerController.DeathCause.Damage)
        {
            ResetDamageDeathPresentation();
        }

        if (playerDeathView != null)
        {
            if (lastDeathCause == PlayerController.DeathCause.Hazard)
            {
                LogRespawn("Hazard black out started");
            }
            else
            {
                LogRespawn("Death transition out started");
            }

            playerDeathView.PlayTransitionOut(lastDeathCause);

            yield return new WaitUntil(() =>
                playerDeathView == null ||
                playerDeathView.GetBlackAmount() <= 0.01f);

            playerDeathView.ResetTransitionImmediate();
            LogRespawn("Death transition reset");
        }

        healthReactionSystem?.MarkRespawnReady();
        respawnSequenceCoroutine = null;
    }

    private void ResetCameraToWorldDefaults()
    {
        if (playerCameraController == null)
        {
            playerCameraController = UnityEngine.Object.FindFirstObjectByType<PlayerCameraController>();
        }

        if (playerCameraController == null)
        {
            LogRespawnWarning("PlayerCameraController missing (camera reset skipped)");
            return;
        }

        playerCameraController.ResetRuntimeStateForRespawn();
    }

    private void PlayDamageDeathZoom()
    {
        if (playerCameraController == null)
        {
            playerCameraController = UnityEngine.Object.FindFirstObjectByType<PlayerCameraController>();
        }

        if (playerCameraController == null)
        {
            LogRespawnWarning("PlayerCameraController missing (damage death zoom skipped)");
            return;
        }

        float targetSize = Mathf.Max(0.01f, playerCameraController.EffectiveSize + ConfiguredDamageDeathZoomSizeOffset);
        playerCameraController.SetActiveOrthographicSizeSmoothTimeOverride(ConfiguredDamageDeathZoomSmoothTime);
        playerCameraController.SetActiveOrthographicSizeOverride(targetSize);
        LogRespawn("Damage death zoom applied");
    }

    private void PlayDamageDeathIntro()
    {
        ResolvePlayerDeathViewIfNeeded();

        if (playerDeathView == null)
        {
            LogRespawnWarning("Damage death rotation target missing");
            return;
        }

        playerDeathView.ConfigureDamageDeathIntro(
            ConfiguredDamageDeathIntroDuration,
            ConfiguredDamageDeathTiltAngle);
        playerDeathView.PlayDamageDeathIntro();
    }

    private IEnumerator WaitForDamageDeathIntro()
    {
        float duration = ConfiguredDamageDeathIntroDuration;
        if (duration <= 0f)
        {
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (playerDeathView == null || playerDeathView.IsIntroComplete())
            {
                break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private void ResetDamageDeathPresentation()
    {
        ResolvePlayerDeathViewIfNeeded();

        if (playerDeathView == null)
        {
            return;
        }

        playerDeathView.ResetDeathPresentation();
    }

    private void ResolvePlayerDeathViewIfNeeded()
    {
        if (playerDeathView == null)
        {
            playerDeathView = coroutineRunner.GetComponentInChildren<PlayerDeathView>();
        }
    }

    private void RespawnAt(Vector3 worldPosition)
    {
        ResetForRespawn();

        if (rb != null)
        {
            rb.position = worldPosition;
            rb.linearVelocity = Vector3.zero;
        }
        else if (playerTransform != null)
        {
            playerTransform.position = worldPosition;
        }
    }

    private void ResetForRespawn()
    {
        stopAllRumble?.Invoke();
        stopAllSounds?.Invoke();

        healthReactionSystem?.ResetForRespawn();

        runtimeState.isGrounded = false;
        runtimeState.isTouchingWall = false;
        runtimeState.wallSide = 0;
        runtimeState.wallJumpControlLockTimer = 0.0f;
        runtimeState.wallReattachLockTimer = 0.0f;
        runtimeState.isWallSliding = false;
        runtimeState.isDashing = false;
        runtimeState.isFastFalling = false;
        runtimeState.dashTimer = 0.0f;
        runtimeState.groundDashCooldownTimer = 0.0f;
        runtimeState.currentDashCharges = Mathf.Max(1, movementSettings.Dash.MaxCharges);
        runtimeState.wasGroundedLastFrame = false;
        runtimeState.dashStartVerticalVelocity = 0.0f;
        frameRequests.jumpRequested = false;
        frameRequests.dashRequested = false;
        resetVisualOneShotFlags?.Invoke();
        locomotionSystem?.ResetRuntimeTimers();

        ResetDamageDeathPresentation();
        ClearDeathFacingLock();
    }

    private void CaptureDeathFacingForVisual()
    {
        runtimeState.fixedDeathFacing = NormalizeFacingSign(runtimeState.facing);
        runtimeState.isDeathFacingFixed = true;
    }

    private void ClearDeathFacingLock()
    {
        runtimeState.isDeathFacingFixed = false;
    }

    private int NormalizeFacingSign(int direction)
    {
        if (direction < 0)
        {
            return -1;
        }

        return 1;
    }

    private void LogRespawn(string message)
    {
        logRespawn?.Invoke(message);
    }

    private void LogRespawnWarning(string message)
    {
        logRespawnWarning?.Invoke(message);
    }
}