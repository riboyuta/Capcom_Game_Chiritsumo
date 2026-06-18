using UnityEngine;

[DisallowMultipleComponent]
public class SlideGimmick : MonoBehaviour, IRespawnResettable
{
    [Header("Target")]
    [SerializeField] private SwitchGimmick targetSwitch;

    [Header("Slide")]
    [SerializeField] private Vector3 slideLocalDirection = Vector3.right;
    [SerializeField, Min(0f)] private float slideDistance = 3.0f;
    [SerializeField, Min(0.1f)] private float slideSpeed = 2.0f;

    private Vector3 initialLocalPosition;
    private Vector3 initialResetLocalPosition;
    private float currentDistance;
    private bool isBlocked;
    private Collider myCollider;
    private bool hasCapturedInitialState;
    private float initialCurrentDistance;
    private bool initialIsBlocked;
    private bool initialColliderEnabled;
    private Renderer[] visualRenderers;
    private bool[] initialRendererEnabledStates;

    public void SetSwitch(SwitchGimmick sw)
    {
        targetSwitch = sw;
    }

    private void Awake()
    {
        initialLocalPosition = transform.localPosition;
        initialResetLocalPosition = transform.localPosition;
        myCollider = GetComponentInChildren<Collider>();
        visualRenderers = GetComponentsInChildren<Renderer>(true);

    }

    private void Update()
    {
        if (targetSwitch == null)
        {
            return;
        }

        UpdateBlockedState();

        bool shouldOpen = targetSwitch.IsPressed;
        float targetDistance = shouldOpen ? slideDistance : 0f;
        float nextDistance = Mathf.MoveTowards(currentDistance, targetDistance, slideSpeed * Time.deltaTime);

        if (shouldOpen && isBlocked && nextDistance > currentDistance)
        {
            nextDistance = currentDistance;
        }

        if (currentDistance == 0f && nextDistance > 0f)
        {
            AudioEvent.Emit(this, "MoveStart");
        }

        currentDistance = nextDistance;
        transform.localPosition = initialLocalPosition + (slideLocalDirection.normalized * currentDistance);
    }

    private void UpdateBlockedState()
    {
        isBlocked = false;
        if (myCollider == null)
        {
            return;
        }

        Collider[] overlaps = Physics.OverlapBox(
            myCollider.bounds.center,
            myCollider.bounds.extents,
            myCollider.transform.rotation);

        foreach (Collider overlap in overlaps)
        {
            if (overlap.GetComponentInParent<SlideStopper>() != null)
            {
                isBlocked = true;
                break;
            }
        }
    }

    public void CaptureInitialState()
    {
        if (hasCapturedInitialState)
        {
            return;
        }

        initialCurrentDistance = currentDistance;
        initialIsBlocked = isBlocked;
        initialLocalPosition = transform.localPosition - (slideLocalDirection.normalized * initialCurrentDistance);
        initialResetLocalPosition = transform.localPosition;
        initialColliderEnabled = myCollider != null && myCollider.enabled;
        CaptureRendererInitialStates();

        hasCapturedInitialState = true;
    }

    public void ResetToRespawnState()
    {
        if (!hasCapturedInitialState)
        {
            CaptureInitialState();
        }

        currentDistance = initialCurrentDistance;
        isBlocked = initialIsBlocked;
        transform.localPosition = initialResetLocalPosition;

        if (myCollider != null)
        {
            myCollider.enabled = initialColliderEnabled;
        }

        RestoreRendererInitialStates();
    }

    private void CaptureRendererInitialStates()
    {
        if (visualRenderers == null)
        {
            initialRendererEnabledStates = null;
            return;
        }

        initialRendererEnabledStates = new bool[visualRenderers.Length];
        for (int i = 0; i < visualRenderers.Length; i++)
        {
            initialRendererEnabledStates[i] = visualRenderers[i] != null && visualRenderers[i].enabled;
        }
    }

    private void RestoreRendererInitialStates()
    {
        if (visualRenderers == null || initialRendererEnabledStates == null)
        {
            return;
        }

        int count = Mathf.Min(visualRenderers.Length, initialRendererEnabledStates.Length);
        for (int i = 0; i < count; i++)
        {
            if (visualRenderers[i] != null)
            {
                visualRenderers[i].enabled = initialRendererEnabledStates[i];
            }
        }
    }

}
