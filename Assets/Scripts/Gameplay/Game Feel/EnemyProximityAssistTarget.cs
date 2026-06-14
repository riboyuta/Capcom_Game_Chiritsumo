using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
public sealed class EnemyProximityAssistTarget : MonoBehaviour
{
    [Header("所属Room / 手動上書き")]
    [Tooltip("親階層からRoomを取得できない配置で、この距離判定対象が属するRoomを指定します。未設定時は親階層から自動取得します。")]
    [SerializeField] private Room roomOverride;

    // HitBoxに付いたMarkerだけを、時間補助の距離判定候補として登録する。
    private static readonly List<EnemyProximityAssistTarget> registeredTargets = new List<EnemyProximityAssistTarget>();

    private BoxCollider cachedCollider;
    private HandChaserMovement cachedMovement;
    private Room cachedOwnerRoom;

    public static IReadOnlyList<EnemyProximityAssistTarget> RegisteredTargets
    {
        get
        {
            PruneDestroyedTargets();
            return registeredTargets;
        }
    }

    public BoxCollider Collider
    {
        get
        {
            ResolveReferencesIfNeeded();
            return cachedCollider;
        }
    }

    public HandChaserMovement Movement
    {
        get
        {
            ResolveReferencesIfNeeded();
            return cachedMovement;
        }
    }

    public Room OwnerRoom
    {
        get
        {
            ResolveReferencesIfNeeded();
            return roomOverride != null ? roomOverride : cachedOwnerRoom;
        }
    }

    public bool IsValid
    {
        get
        {
            ResolveReferencesIfNeeded();

            return isActiveAndEnabled
                && cachedCollider != null
                && cachedCollider.enabled
                && cachedCollider.gameObject.activeInHierarchy
                && cachedMovement != null
                && cachedMovement.IsActive
                && OwnerRoom != null;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void InitializeRegistry()
    {
        // Domain Reload無効時でも、前回Playの破棄済み参照を持ち越さない。
        registeredTargets.Clear();
    }

    public static void CopyRegisteredTargets(List<EnemyProximityAssistTarget> results)
    {
        if (results == null)
        {
            return;
        }

        results.Clear();
        PruneDestroyedTargets();

        for (int i = 0; i < registeredTargets.Count; i++)
        {
            EnemyProximityAssistTarget target = registeredTargets[i];
            if (target != null)
            {
                results.Add(target);
            }
        }
    }

    public static void CollectValidTargets(Room ownerRoom, List<EnemyProximityAssistTarget> results)
    {
        if (results == null)
        {
            return;
        }

        results.Clear();
        PruneDestroyedTargets();

        for (int i = 0; i < registeredTargets.Count; i++)
        {
            EnemyProximityAssistTarget target = registeredTargets[i];
            if (target == null || !target.IsValid)
            {
                continue;
            }

            if (ownerRoom != null && target.OwnerRoom != ownerRoom)
            {
                continue;
            }

            results.Add(target);
        }
    }

    private void Reset()
    {
        ResolveReferences();
    }

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        Register(this);
    }

    private void OnDisable()
    {
        Unregister(this);
    }

    private void OnDestroy()
    {
        Unregister(this);
    }

    private void OnValidate()
    {
        ResolveReferences();
    }

    private static void Register(EnemyProximityAssistTarget target)
    {
        if (target == null)
        {
            return;
        }

        PruneDestroyedTargets();

        if (!registeredTargets.Contains(target))
        {
            registeredTargets.Add(target);
        }
    }

    private static void Unregister(EnemyProximityAssistTarget target)
    {
        if (target == null)
        {
            return;
        }

        registeredTargets.Remove(target);
        PruneDestroyedTargets();
    }

    private static void PruneDestroyedTargets()
    {
        for (int i = registeredTargets.Count - 1; i >= 0; i--)
        {
            if (registeredTargets[i] == null)
            {
                registeredTargets.RemoveAt(i);
            }
        }
    }

    private void ResolveReferencesIfNeeded()
    {
        if (cachedCollider != null
            && cachedMovement != null
            && (roomOverride != null || cachedOwnerRoom != null))
        {
            return;
        }

        ResolveReferences();
    }

    private void ResolveReferences()
    {
        // Prefab配置と実行時生成の両方で使えるよう、必要参照は親階層から解決する。
        cachedCollider = GetComponent<BoxCollider>();
        cachedMovement = GetComponentInParent<HandChaserMovement>();
        cachedOwnerRoom = roomOverride != null ? roomOverride : GetComponentInParent<Room>();
    }
}
