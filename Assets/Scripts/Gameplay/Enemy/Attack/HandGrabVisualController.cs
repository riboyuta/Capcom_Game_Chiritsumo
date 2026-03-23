using UnityEngine;

public sealed class HandGrabVisualController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private HandPoseView poseView;
    [SerializeField] private SpriteRenderer armSpriteRenderer;
    [SerializeField] private SpriteRenderer handSpriteRenderer;

    [Header("State Clips")]
    [SerializeField] private HandPoseView.HandPoseClip approachNearClip;
    [SerializeField] private HandPoseView.HandPoseClip trackBeforeGrabClip;
    [SerializeField] private HandPoseView.HandPoseClip grabStartClip;
    [SerializeField] private HandPoseView.HandPoseClip holdPlayerClip;
    [SerializeField] private HandPoseView.HandPoseClip missPauseClip;
    [SerializeField] private HandPoseView.HandPoseClip endClip;

    [Header("Sorting")]
    [SerializeField] private string defaultSortingLayerName = "Default";
    [SerializeField] private int defaultSortingOrder = 0;
    [SerializeField] private string grabbedSortingLayerName = "Default";
    [SerializeField] private int grabbedSortingOrder = 10;
    [SerializeField] private string endSortingLayerName = "Default";
    [SerializeField] private int endSortingOrder = -10;

    private void Awake()
    {
        if (poseView == null)
        {
            poseView = GetComponent<HandPoseView>();
        }

        if (armSpriteRenderer == null)
        {
            Transform arm = transform.Find("ArmRenderer");
            if (arm != null)
            {
                armSpriteRenderer = arm.GetComponent<SpriteRenderer>();
            }
        }

        if (handSpriteRenderer == null)
        {
            Transform hand = transform.Find("HandRenderer");
            if (hand != null)
            {
                handSpriteRenderer = hand.GetComponent<SpriteRenderer>();
            }
        }

        SetDefaultSorting();
    }

    public void PlayApproachNear()
    {
        if (poseView != null) poseView.PlayClip(approachNearClip);
    }

    public void PlayTrackBeforeGrab()
    {
        if (poseView != null) poseView.PlayClip(trackBeforeGrabClip);
    }

    public void PlayGrabStart()
    {
        if (poseView != null) poseView.PlayClip(grabStartClip);
    }

    public void PlayHoldPlayer()
    {
        if (poseView != null) poseView.PlayClip(holdPlayerClip);
    }

    public void PlayMissPause()
    {
        if (poseView != null) poseView.PlayClip(missPauseClip);
    }

    public void PlayEnd()
    {
        if (poseView != null) poseView.PlayClip(endClip);
    }

    public void SetDefaultSorting()
    {
        ApplySorting(defaultSortingLayerName, defaultSortingOrder);
    }

    public void SetGrabbedSorting()
    {
        ApplySorting(grabbedSortingLayerName, grabbedSortingOrder);
    }

    public void SetEndSorting()
    {
        ApplySorting(endSortingLayerName, endSortingOrder);
    }

    private void ApplySorting(string sortingLayerName, int sortingOrder)
    {
        if (armSpriteRenderer != null)
        {
            armSpriteRenderer.sortingLayerName = sortingLayerName;
            armSpriteRenderer.sortingOrder = sortingOrder;
        }

        if (handSpriteRenderer != null)
        {
            handSpriteRenderer.sortingLayerName = sortingLayerName;
            handSpriteRenderer.sortingOrder = sortingOrder;
        }
    }
}