using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public sealed class SonarChargerActivator : EnemySafeZoneActivatorBase
{
    [Header("起動対象の敵")]
    [Tooltip("起動対象の SonarChargerEnemy です。")]
    [SerializeField] private SonarChargerEnemy targetEnemy;

    [Header("ゲーム進行管理")]
    [Tooltip("初回有効発動時に経過時間計測の開始通知を送る GameController です。未使用なら未設定で構いません。")]
    [SerializeField] private GameController gameController;

    [Header("スポーン位置使用フラグ")]
    [Tooltip("起動時に敵をこの位置へ移動させるかです。")]
    [SerializeField] private bool useSpawnPointOnActivate = false;

    [Header("スポーン位置")]
    [Tooltip("起動時のスポーン位置です。未設定時はこの GameObject の Transform を使います。")]
    [SerializeField] private Transform spawnPoint;

    protected override void Awake()
    {
        base.Awake();

        if (spawnPoint == null)
        {
            spawnPoint = transform;
        }
    }

    protected override bool HasValidTarget()
    {
        return targetEnemy != null;
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

        if (useSpawnPointOnActivate && spawnPoint != null)
        {
            targetEnemy.BeginChase(spawnPoint.position, spawnPoint.rotation);
        }
        else
        {
            targetEnemy.BeginChase();
        }
    }

    protected override void ResetTargetEnemyForRespawn()
    {
        if (targetEnemy != null)
        {
            targetEnemy.ResetEncounterForRespawn();
        }
    }
}