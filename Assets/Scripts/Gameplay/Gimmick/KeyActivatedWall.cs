using UnityEngine;

[DisallowMultipleComponent]
public class KeyActivatedWall : MonoBehaviour, IRespawnResettable
{
    [Header("Target")]
    [SerializeField] private KeyManager targetKeyManager;

    [Header("Slide")]
    [SerializeField] private Vector3 slideLocalDirection = Vector3.right;
    [SerializeField, Min(0f)] private float slideDistance = 3.0f;
    [SerializeField, Min(0.1f)] private float slideSpeed = 2.0f;

    private Vector3 initialLocalPosition;
    private float currentDistance;
    private bool hasCapturedInitialState;

    private Vector3 capturedInitialLocalPosition;
    private float capturedInitialDistance;

    private void Awake()
    {
        initialLocalPosition = transform.localPosition;
    }

    private void Update()
    {
        if (targetKeyManager == null)
        {
            return;
        }

        bool shouldOpen = targetKeyManager.IsCompleted;
        float targetDistance = shouldOpen ? slideDistance : 0f;
        float nextDistance = Mathf.MoveTowards(currentDistance, targetDistance, slideSpeed * Time.deltaTime);

        if (currentDistance == 0f && nextDistance > 0f)
        {
            AudioEvent.Emit(this, "OpenStart");
        }

        currentDistance = nextDistance;
        transform.localPosition = initialLocalPosition + (slideLocalDirection.normalized * currentDistance);
    }

    private void CaptureWallInitialState()
    {
        capturedInitialLocalPosition = transform.localPosition;
        capturedInitialDistance = currentDistance;
        initialLocalPosition = capturedInitialLocalPosition - (slideLocalDirection.normalized * capturedInitialDistance);
    }

    private void RestoreWallInitialState()
    {
        currentDistance = capturedInitialDistance;
        initialLocalPosition = capturedInitialLocalPosition - (slideLocalDirection.normalized * capturedInitialDistance);
        transform.localPosition = capturedInitialLocalPosition;
    }

    public void CaptureInitialState()
    {
        if (hasCapturedInitialState)
        {
            return;
        }

        CaptureWallInitialState();
        hasCapturedInitialState = true;
    }

    public void ResetToRespawnState()
    {
        if (!hasCapturedInitialState)
        {
            CaptureInitialState();
        }

        RestoreWallInitialState();
    }
}
