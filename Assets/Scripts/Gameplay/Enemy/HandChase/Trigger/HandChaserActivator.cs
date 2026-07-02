using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public sealed class HandChaserActivator : EnemySafeZoneActivatorBase
{
    private const float AxisThreshold = 0.0001f;

    [Header("起動対象の敵")]
    [Tooltip("起動対象の HandChaserEnemy です。")]
    [SerializeField] private HandChaserEnemy targetEnemy;

    [Header("ゲーム進行")]
    [Tooltip("初回有効発動時に経過時間計測の開始通知を送る GameController です。")]
    [SerializeField] private GameController gameController;

    [Header("出現予告UI")]
    [Tooltip("HandEnemy 出現前に画面端へ表示する警告UIです。")]
    [SerializeField] private HandEnemySpawnWarningView spawnWarningView;

    [Tooltip("出現方向の判定に使う HandChaserMovement です。未設定時は targetEnemy から自動取得します。")]
    [SerializeField] private HandChaserMovement targetMovement;

    [Tooltip("出現予告UIを使うかどうかです。")]
    [SerializeField] private bool useSpawnWarning = true;

    protected override void Awake()
    {
        base.Awake();

        if (targetMovement == null && targetEnemy != null)
        {
            targetMovement = targetEnemy.GetComponent<HandChaserMovement>();
        }
    }

    protected override bool HasValidTarget()
    {
        return targetEnemy != null;
    }

    protected override void OnPlayerInsideSafeZone()
    {
        StartSpawnWarningIfNeeded();
    }

    protected override void OnPlayerExitSafeZoneConfirmed()
    {
        FadeOutSpawnWarning();
    }

    protected override void OnSpawnSequenceStarted()
    {
        if (gameController != null)
        {
            gameController.StartOrResumeElapsedTime();
        }
    }

    protected override void ActivateTargetEnemy()
    {
        if (targetEnemy == null)
        {
            return;
        }

        targetEnemy.BeginChase();

        AudioEvent.EmitAt(this, "Spawn", targetEnemy.transform.position);
    }

    protected override void ResetTargetEnemyForRespawn()
    {
        if (targetEnemy != null)
        {
            targetEnemy.ResetEncounterForRespawn();
        }
    }

    protected override void OnSpawnDelayCanceled()
    {
        StopSpawnWarning();
    }

    protected override void OnSafeZoneRoomDeactivated()
    {
        StopSpawnWarning();
    }

    protected override void OnSafeZoneDisabled()
    {
        StopSpawnWarning();
    }

    protected override void OnBeforeResetToRespawnState()
    {
        StopSpawnWarning();
    }

    private void StartSpawnWarningIfNeeded()
    {
        if (!useSpawnWarning || spawnWarningView == null)
        {
            return;
        }

        if (spawnWarningView.IsPlaying)
        {
            return;
        }

        SpawnWarningScreenEdge edge = ResolveSpawnWarningEdge();
        spawnWarningView.PlayLoop(edge);
    }

    private void StopSpawnWarning()
    {
        if (spawnWarningView == null)
        {
            return;
        }

        spawnWarningView.StopAndHide();
    }

    private void FadeOutSpawnWarning()
    {
        if (spawnWarningView == null)
        {
            return;
        }

        spawnWarningView.FadeOutAndHide();
    }

    private SpawnWarningScreenEdge ResolveSpawnWarningEdge()
    {
        if (targetMovement == null)
        {
            return SpawnWarningScreenEdge.Left;
        }

        switch (targetMovement.Direction)
        {
            case MoveDirection.Right:
                return SpawnWarningScreenEdge.Left;

            case MoveDirection.Left:
                return SpawnWarningScreenEdge.Right;

            case MoveDirection.Up:
                return SpawnWarningScreenEdge.Bottom;

            case MoveDirection.Down:
                return SpawnWarningScreenEdge.Top;

            case MoveDirection.Custom:
                return ResolveSpawnWarningEdgeFromAxis(targetMovement.CustomMoveAxis);

            default:
                return SpawnWarningScreenEdge.Left;
        }
    }

    private SpawnWarningScreenEdge ResolveSpawnWarningEdgeFromAxis(Vector3 axis)
    {
        if (axis.sqrMagnitude <= AxisThreshold)
        {
            return SpawnWarningScreenEdge.Left;
        }

        Vector3 normalizedAxis = axis.normalized;

        if (Mathf.Abs(normalizedAxis.x) >= Mathf.Abs(normalizedAxis.y))
        {
            return normalizedAxis.x >= 0f
                ? SpawnWarningScreenEdge.Left
                : SpawnWarningScreenEdge.Right;
        }

        return normalizedAxis.y >= 0f
            ? SpawnWarningScreenEdge.Bottom
            : SpawnWarningScreenEdge.Top;
    }
}