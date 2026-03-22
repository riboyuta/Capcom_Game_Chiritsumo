using UnityEngine;

public sealed class HandGrabView : MonoBehaviour
{
    private enum AnimState
    {
        None,
        ApproachNear,
        TrackBeforeGrab,
        GrabStart,
        HoldPlayer,
        MissPause,
        End
    }

    [Header("References")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("Animation Frames")]
    [SerializeField] private Sprite[] approachNearFrames;
    [SerializeField] private Sprite[] trackBeforeGrabFrames;
    [SerializeField] private Sprite[] grabStartFrames;
    [SerializeField] private Sprite[] holdPlayerFrames;
    [SerializeField] private Sprite[] missPauseFrames;
    [SerializeField] private Sprite[] endFrames;

    [Header("Animation Speed")]
    [SerializeField] private float approachNearFrameRate = 12.0f;
    [SerializeField] private float trackBeforeGrabFrameRate = 10.0f;
    [SerializeField] private float grabStartFrameRate = 14.0f;
    [SerializeField] private float holdPlayerFrameRate = 10.0f;
    [SerializeField] private float missPauseFrameRate = 8.0f;
    [SerializeField] private float endFrameRate = 10.0f;

    [Header("Loop")]
    [SerializeField] private bool loopApproachNear = true;
    [SerializeField] private bool loopTrackBeforeGrab = true;
    [SerializeField] private bool loopGrabStart = false;
    [SerializeField] private bool loopHoldPlayer = true;
    [SerializeField] private bool loopMissPause = false;
    [SerializeField] private bool loopEnd = false;

    [Header("Sorting")]
    [SerializeField] private int defaultSortingOrder = 0;
    [SerializeField] private int grabbedSortingOrder = 10;

    private AnimState currentState = AnimState.None;
    private Sprite[] currentFrames;
    private float currentFrameRate = 12.0f;
    private bool currentLoop = false;

    private float frameTimer = 0.0f;
    private int currentFrameIndex = 0;

    private void Awake()
    {
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        SetDefaultSorting();
    }

    private void Update()
    {
        if (spriteRenderer == null)
        {
            return;
        }

        if (currentFrames == null || currentFrames.Length == 0)
        {
            return;
        }

        if (currentFrameRate <= 0.0f)
        {
            return;
        }

        frameTimer += Time.deltaTime;
        float frameDuration = 1.0f / currentFrameRate;

        while (frameTimer >= frameDuration)
        {
            frameTimer -= frameDuration;
            AdvanceFrame();
        }
    }

    public void PlayApproachNear()
    {
        SetAnimation(AnimState.ApproachNear, approachNearFrames, approachNearFrameRate, loopApproachNear);
    }

    public void PlayTrackBeforeGrab()
    {
        SetAnimation(AnimState.TrackBeforeGrab, trackBeforeGrabFrames, trackBeforeGrabFrameRate, loopTrackBeforeGrab);
    }

    public void PlayGrabStart()
    {
        SetAnimation(AnimState.GrabStart, grabStartFrames, grabStartFrameRate, loopGrabStart);
    }

    public void PlayHoldPlayer()
    {
        SetAnimation(AnimState.HoldPlayer, holdPlayerFrames, holdPlayerFrameRate, loopHoldPlayer);
    }

    public void PlayMissPause()
    {
        SetAnimation(AnimState.MissPause, missPauseFrames, missPauseFrameRate, loopMissPause);
    }

    public void PlayEnd()
    {
        SetAnimation(AnimState.End, endFrames, endFrameRate, loopEnd);
    }

    public void SetDefaultSorting()
    {
        if (spriteRenderer == null)
        {
            return;
        }

        spriteRenderer.sortingOrder = defaultSortingOrder;
    }

    public void SetGrabbedSorting()
    {
        if (spriteRenderer == null)
        {
            return;
        }

        spriteRenderer.sortingOrder = grabbedSortingOrder;
    }

    private void SetAnimation(AnimState nextState, Sprite[] nextFrames, float nextFrameRate, bool nextLoop)
    {
        if (currentState == nextState)
        {
            return;
        }

        currentState = nextState;
        currentFrames = nextFrames;
        currentFrameRate = nextFrameRate;
        currentLoop = nextLoop;
        currentFrameIndex = 0;
        frameTimer = 0.0f;

        if (spriteRenderer != null && currentFrames != null && currentFrames.Length > 0)
        {
            spriteRenderer.sprite = currentFrames[0];
        }
    }

    private void AdvanceFrame()
    {
        if (currentFrames == null || currentFrames.Length == 0)
        {
            return;
        }

        if (currentLoop)
        {
            currentFrameIndex = (currentFrameIndex + 1) % currentFrames.Length;
        }
        else
        {
            if (currentFrameIndex < currentFrames.Length - 1)
            {
                currentFrameIndex++;
            }
        }

        spriteRenderer.sprite = currentFrames[currentFrameIndex];
    }
}