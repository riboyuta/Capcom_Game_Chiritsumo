using System;
using System.Collections;
using UnityEngine;

public enum PlayerDeathCause
{
    Damage,
    Hazard
}

internal sealed class PlayerDeathCoordinator
{
    private readonly MonoBehaviour coroutineRunner;
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
    private PlayerDeathCause lastDeathCause = PlayerDeathCause.Damage;
    private Coroutine respawnSequenceCoroutine;

    private bool isDead;
    private bool isDeathSequencePlaying;

    internal bool IsDead => isDead;
    internal bool IsDeadState => isDead;
    internal bool IsDeathSequencePlaying => isDeathSequencePlaying;

    internal PlayerDeathCoordinator(
        MonoBehaviour coroutineRunner,
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

    internal void StartRespawnSequence(PlayerDeathCause deathCause)
    {
        isDead = true;
        isDeathSequencePlaying = true;
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

    private IEnumerator CoRespawnSequence()
    {
        LogRespawn("Respawn sequence started");

        if (lastDeathCause == PlayerDeathCause.Damage)
        {
            PlayDamageDeathIntro();
            yield return WaitForDamageDeathIntro();
        }

        ResolvePlayerDeathViewIfNeeded();
        if (playerDeathView != null)
        {
            LogRespawn("Respawn blink close started");
            yield return playerDeathView.PlayRespawnTransitionIn();
            LogRespawn("Respawn blink close complete");
        }
        else
        {
            LogRespawnWarning("PlayerDeathView missing (respawn transition unavailable)");
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
        if (lastDeathCause == PlayerDeathCause.Damage)
        {
            ResetDamageDeathPresentation();
        }

        if (playerDeathView != null)
        {
            LogRespawn("Respawn blink open started");
            yield return playerDeathView.PlayRespawnTransitionOut();
            playerDeathView.ResetRespawnTransitionImmediate();
            LogRespawn("Respawn blink open complete");
        }

        respawnSequenceCoroutine = null;
        isDead = false;
        isDeathSequencePlaying = false;
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

        runtimeState.isGrounded = false;
        runtimeState.isTouchingWall = false;
        runtimeState.wallSide = 0;
        runtimeState.wallJumpControlLockTimer = 0.0f;
        runtimeState.wallReattachLockTimer = 0.0f;
        runtimeState.isWallSliding = false;
        runtimeState.isDashing = false;
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