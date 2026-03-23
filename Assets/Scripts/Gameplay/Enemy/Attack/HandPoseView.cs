using UnityEngine;

public sealed class HandPoseView : MonoBehaviour
{
    [System.Serializable]
    public sealed class HandPoseFrame
    {
        public Sprite armSprite;
        public Sprite handSprite;

        public Vector3 armLocalPosition;
        public Vector3 handLocalPosition;

        public Vector3 armLocalScale = Vector3.one;
        public Vector3 handLocalScale = Vector3.one;

        public Vector3 armLocalEulerAngles;
        public Vector3 handLocalEulerAngles;
    }

    [System.Serializable]
    public sealed class HandPoseClip
    {
        public HandPoseFrame[] frames;
        public float frameRate = 12.0f;
        public bool loop = true;
    }

    [Header("References")]
    [SerializeField] private SpriteRenderer armSpriteRenderer;
    [SerializeField] private SpriteRenderer handSpriteRenderer;

    private HandPoseClip currentClip;
    private float frameTimer = 0.0f;
    private int currentFrameIndex = 0;
    private bool isPlaying = false;

    private void Awake()
    {
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
    }

    private void Update()
    {
        if (!isPlaying || currentClip == null || currentClip.frames == null || currentClip.frames.Length == 0)
        {
            return;
        }

        if (currentClip.frameRate <= 0.0f)
        {
            return;
        }

        frameTimer += Time.deltaTime;
        float frameDuration = 1.0f / currentClip.frameRate;

        while (frameTimer >= frameDuration)
        {
            frameTimer -= frameDuration;
            AdvanceFrame();
        }
    }

    public void PlayClip(HandPoseClip clip, bool restart = true)
    {
        if (clip == null)
        {
            return;
        }

        if (!restart && currentClip == clip)
        {
            return;
        }

        currentClip = clip;
        currentFrameIndex = 0;
        frameTimer = 0.0f;
        isPlaying = true;

        ApplyCurrentFrame();
    }

    public void Stop()
    {
        isPlaying = false;
    }

    private void AdvanceFrame()
    {
        if (currentClip == null || currentClip.frames == null || currentClip.frames.Length == 0)
        {
            return;
        }

        if (currentClip.loop)
        {
            currentFrameIndex = (currentFrameIndex + 1) % currentClip.frames.Length;
        }
        else
        {
            if (currentFrameIndex < currentClip.frames.Length - 1)
            {
                currentFrameIndex++;
            }
        }

        ApplyCurrentFrame();
    }

    private void ApplyCurrentFrame()
    {
        if (currentClip == null || currentClip.frames == null || currentClip.frames.Length == 0)
        {
            return;
        }

        HandPoseFrame frame = currentClip.frames[currentFrameIndex];
        if (frame == null)
        {
            return;
        }

        ApplyToRenderer(
            armSpriteRenderer,
            frame.armSprite,
            frame.armLocalPosition,
            frame.armLocalEulerAngles,
            frame.armLocalScale
        );

        ApplyToRenderer(
            handSpriteRenderer,
            frame.handSprite,
            frame.handLocalPosition,
            frame.handLocalEulerAngles,
            frame.handLocalScale
        );
    }

    private static void ApplyToRenderer(
        SpriteRenderer target,
        Sprite sprite,
        Vector3 localPosition,
        Vector3 localEulerAngles,
        Vector3 localScale
    )
    {
        if (target == null)
        {
            return;
        }

        target.sprite = sprite;
        target.transform.localPosition = localPosition;
        target.transform.localEulerAngles = localEulerAngles;
        target.transform.localScale = localScale;
    }
}