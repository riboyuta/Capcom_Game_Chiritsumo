using UnityEngine;

public sealed class HandSmashView : MonoBehaviour
{
    private enum AnimState
    {
        None,
        Rise,
        Hold,
        Smash,
        End
    }

    [Header("References")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("Animation Frames")]
    [SerializeField] private Sprite[] riseFrames;
    [SerializeField] private Sprite[] holdFrames;
    [SerializeField] private Sprite[] smashFrames;
    [SerializeField] private Sprite[] endFrames;

    [Header("Animation Speed")]
    [SerializeField] private float riseFrameRate = 12.0f;
    [SerializeField] private float holdFrameRate = 12.0f;
    [SerializeField] private float smashFrameRate = 16.0f;
    [SerializeField] private float endFrameRate = 12.0f;

    [Header("Loop")]
    [SerializeField] private bool loopRise = true;
    [SerializeField] private bool loopHold = true;
    [SerializeField] private bool loopSmash = false;
    [SerializeField] private bool loopEnd = false;

    private AnimState currentState = AnimState.None;
    private Sprite[] currentFrames;
    private float currentFrameRate = 12.0f;
    private bool currentLoop = false;

    private float frameTimer = 0.0f;
    private int currentFrameIndex = 0;

    private string currentPlayingAudioId = null;

    private void Awake()
    {
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }
    }

    private void OnDestroy()
    {
        StopCurrentAudio();
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

    public void PlayRise()
    {
        SetAnimation(AnimState.Rise, riseFrames, riseFrameRate, loopRise);
        PlayAudio("SFX_boss_attack_approach");
        PlayAudio("SFX_boss_slam_approach");
    }

    public void PlayHold()
    {
        SetAnimation(AnimState.Hold, holdFrames, holdFrameRate, loopHold);
        PlayAudio("SFX_boss_slam_hold");
    }

    public void PlaySmash()
    {
        SetAnimation(AnimState.Smash, smashFrames, smashFrameRate, loopSmash);
        PlayAudio("SFX_boss_slam");
    }

    public void PlayEnd()
    {
        SetAnimation(AnimState.End, endFrames, endFrameRate, loopEnd);
    }

    private void SetAnimation(AnimState nextState, Sprite[] nextFrames, float nextFrameRate, bool nextLoop)
    {
        if (currentState == nextState)
        {
            return;
        }

        StopCurrentAudio();

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

    private void PlayAudio(string audioId)
    {
        if (string.IsNullOrEmpty(audioId) || AudioManager.Instance == null)
        {
            return;
        }

        StopCurrentAudio();
        AudioManager.Instance.Play(audioId);
        currentPlayingAudioId = audioId;
    }

    private void StopCurrentAudio()
    {
        if (!string.IsNullOrEmpty(currentPlayingAudioId) && AudioManager.Instance != null)
        {
            AudioManager.Instance.Stop(currentPlayingAudioId);
            currentPlayingAudioId = null;
        }
    }
}