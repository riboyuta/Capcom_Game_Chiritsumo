using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class KeyCollectible : MonoBehaviour, IRespawnResettable
{
    [Header("Visual")]
    [SerializeField] private Transform visualTransform;

    private Collider myCollider;
    private Renderer[] visualRenderers;
    private KeyManager manager;
    private bool isCollected;
    private bool hasCapturedInitialState;

    private bool initialIsCollected;
    private bool initialColliderEnabled;
    private bool[] initialRendererEnabledStates;

    public void Initialize(KeyManager m)
    {
        manager = m;
    }

    private void Awake()
    {
        myCollider = GetComponent<Collider>();
        if (myCollider != null)
        {
            myCollider.isTrigger = true;
        }

        if (visualTransform == null)
        {
            visualTransform = transform.childCount > 0 ? transform.GetChild(0) : transform;
        }

        visualRenderers = visualTransform != null
            ? visualTransform.GetComponentsInChildren<Renderer>(true)
            : new Renderer[0];

        if (visualTransform == null)
        {
            Debug.LogWarning($"[{nameof(KeyCollectible)}] visualTransform could not be resolved: {name}", this);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isCollected) return;
        if (!other.CompareTag("Player")) return;

        GetCollected();
    }

    private void GetCollected()
    {
        isCollected = true;

        if (myCollider != null)
        {
            myCollider.enabled = false;
        }

        for (int i = 0; i < visualRenderers.Length; i++)
        {
            if (visualRenderers[i] != null)
            {
                visualRenderers[i].enabled = false;
            }
        }

        if (manager != null)
        {
            manager.NotifyKeyCollected();
        }
        else
        {
            Debug.LogWarning($"[{nameof(KeyCollectible)}] KeyManager is not assigned: {name}", this);
        }

        AudioEvent.Emit(this, "Collected");
    }

    public void CaptureInitialState()
    {
        if (hasCapturedInitialState)
        {
            return;
        }

        initialIsCollected = isCollected;
        initialColliderEnabled = myCollider != null && myCollider.enabled;

        initialRendererEnabledStates = new bool[visualRenderers.Length];
        for (int i = 0; i < visualRenderers.Length; i++)
        {
            initialRendererEnabledStates[i] = visualRenderers[i] != null && visualRenderers[i].enabled;
        }

        hasCapturedInitialState = true;
    }

    public void ResetToRespawnState()
    {
        if (!hasCapturedInitialState)
        {
            CaptureInitialState();
        }

        isCollected = initialIsCollected;

        if (myCollider != null)
        {
            myCollider.enabled = initialColliderEnabled;
        }

        int restoreCount = Mathf.Min(
            visualRenderers.Length,
            initialRendererEnabledStates != null ? initialRendererEnabledStates.Length : 0);

        for (int i = 0; i < restoreCount; i++)
        {
            if (visualRenderers[i] != null)
            {
                visualRenderers[i].enabled = initialRendererEnabledStates[i];
            }
        }
    }

}
