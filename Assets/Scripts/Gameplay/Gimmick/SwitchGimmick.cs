using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class SwitchGimmick : MonoBehaviour, IRespawnResettable
{
    public enum SwitchType
    {
        OneShot,
        Continuous
    }

    [Header("Switch")]
    [SerializeField] private SwitchType switchType = SwitchType.Continuous;
    [SerializeField] private Vector3 pushLocalDirection = Vector3.down;
    [SerializeField, Min(0f)] private float pressDepth = 0.2f;
    [SerializeField, Min(0.1f)] private float pressSpeed = 1.0f;
    [SerializeField, Min(0.1f)] private float releaseSpeed = 1.0f;
    [SerializeField, Range(0f, 1f)] private float activateThreshold = 0.9f;

    [Header("Feedback")]
    [SerializeField] private Animator anim;

    private Vector3 initialLocalPosition;
    private float currentPressDistance;
    private bool isPushedThisFrame;
    private bool hasCapturedInitialState;

    public bool IsPressed { get; private set; }

    private void Awake()
    {
        initialLocalPosition = transform.localPosition;

    }

    public void CaptureInitialState()
    {
        if (hasCapturedInitialState)
        {
            return;
        }

        initialLocalPosition = transform.localPosition;
        hasCapturedInitialState = true;
    }

    public void ResetToRespawnState()
    {
        IsPressed = false;
        currentPressDistance = 0f;
        isPushedThisFrame = false;
        transform.localPosition = initialLocalPosition;
    }

    private void OnTriggerStay(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        TryPush(other.attachedRigidbody);
    }

    private void OnCollisionStay(Collision collision)
    {
        if (!collision.collider.CompareTag("Player")) return;
        TryPush(collision.rigidbody);
    }

    private void TryPush(Rigidbody targetRb)
    {
        if (targetRb == null) return;
        isPushedThisFrame = true;
    }

    private void Update()
    {
        if (isPushedThisFrame || (switchType == SwitchType.OneShot && IsPressed))
        {
            currentPressDistance += pressSpeed * Time.deltaTime;

            if (anim != null)
            {
                anim.SetTrigger("Interacted");
            }
        }
        else if (switchType == SwitchType.Continuous)
        {
            currentPressDistance -= releaseSpeed * Time.deltaTime;
        }

        currentPressDistance = Mathf.Clamp(currentPressDistance, 0f, pressDepth);

        bool wasPressed = IsPressed;
        IsPressed = currentPressDistance >= pressDepth * activateThreshold;

        if (!wasPressed && IsPressed)
        {
            AudioEvent.Emit(this, "Pressed");
        }

        transform.localPosition = initialLocalPosition + (pushLocalDirection.normalized * currentPressDistance);
        isPushedThisFrame = false;
    }

}
