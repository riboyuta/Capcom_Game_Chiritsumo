using UnityEngine;

public sealed class HandSmashVisualController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private HandPoseView poseView;

    [Header("State Clips")]
    [SerializeField] private HandPoseView.HandPoseClip riseClip;
    [SerializeField] private HandPoseView.HandPoseClip holdClip;
    [SerializeField] private HandPoseView.HandPoseClip smashClip;
    [SerializeField] private HandPoseView.HandPoseClip endClip;

    private void Awake()
    {
        if (poseView == null)
        {
            poseView = GetComponent<HandPoseView>();
        }
    }

    public void PlayRise()
    {
        if (poseView != null) poseView.PlayClip(riseClip);
    }

    public void PlayHold()
    {
        if (poseView != null) poseView.PlayClip(holdClip);
    }

    public void PlaySmash()
    {
        if (poseView != null) poseView.PlayClip(smashClip);
    }

    public void PlayEnd()
    {
        if (poseView != null) poseView.PlayClip(endClip);
    }
}