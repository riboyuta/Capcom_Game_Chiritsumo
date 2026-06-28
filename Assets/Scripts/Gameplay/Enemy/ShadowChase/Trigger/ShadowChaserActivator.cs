using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public sealed class ShadowChaserActivator : EnemySafeZoneActivatorBase
{
    [Header("起動対象の敵")]
    [Tooltip("起動対象の ShadowChaserEnemy です。")]
    [SerializeField] private ShadowChaserEnemy targetEnemy;

    [Header("スポーン位置")]
    [Tooltip("セーフゾーン退出後に敵を出現させる位置です。未設定時はこの GameObject の Transform を使います。")]
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

    protected override void ActivateTargetEnemy()
    {
        if (targetEnemy == null || spawnPoint == null)
        {
            return;
        }

        targetEnemy.Activate(spawnPoint.position, spawnPoint.rotation);
    }
}